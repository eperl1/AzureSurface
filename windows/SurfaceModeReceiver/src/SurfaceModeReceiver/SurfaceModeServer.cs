using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;

namespace SurfaceModeReceiver;

internal sealed class SurfaceModeServer : IDisposable
{
    private readonly ReceiverConfig _config;
    private readonly ReceiverLog _log;
    private readonly ModeStateMachine _stateMachine;
    private readonly TouchKeyboardController _keyboardController;
    private readonly IPostureController _postureController;
    private WebApplication? _app;
    private Task? _runTask;
    private readonly object _gate = new();

    public event EventHandler<ReceiverStateSnapshot>? StateChanged;

    public SurfaceModeServer(ReceiverConfig config, ReceiverLog log)
        : this(config, log, new TouchKeyboardController(), new SurfacePostureController(log))
    {
    }

    internal SurfaceModeServer(ReceiverConfig config, ReceiverLog log, TouchKeyboardController keyboardController, IPostureController postureController)
    {
        _config = config;
        _log = log;
        _keyboardController = keyboardController;
        _postureController = postureController;
        _stateMachine = new ModeStateMachine(TimeSpan.FromSeconds(Math.Max(30, config.NonceRetentionSeconds)));
    }

    public ReceiverStateSnapshot CurrentState => new(_stateMachine.CurrentMode, DescribeEndpoint(), "stopped");

    public string ListenDescription => DescribeEndpoint();

    public async Task StartAsync()
    {
        lock (_gate)
        {
            if (_app is not null)
            {
                return;
            }
        }

        var bindAddress = NetworkBindingResolver.ResolveBindAddress(_config.ListenAddress);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(bindAddress, _config.Port, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1;
            });
        });

        var app = builder.Build();
        app.MapPost("/api/mode", (Func<HttpContext, Task<IResult>>)HandleModeRequestAsync);

        lock (_gate)
        {
            _app = app;
            _runTask = app.RunAsync();
        }

        await Task.Yield();
    }

    public async Task RestartAsync()
    {
        await StopAsync();
        await StartAsync();
    }

    public async Task StopAsync()
    {
        WebApplication? app;
        Task? runTask;
        lock (_gate)
        {
            app = _app;
            runTask = _runTask;
            _app = null;
            _runTask = null;
        }

        if (app is not null)
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }

        if (runTask is not null)
        {
            try
            {
                await runTask;
            }
            catch
            {
                // ignore shutdown race
            }
        }
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    private string DescribeEndpoint()
    {
        var address = NetworkBindingResolver.ResolveBindAddress(_config.ListenAddress);
        return $"{address}:{_config.Port}";
    }

    private async Task<IResult> HandleModeRequestAsync(HttpContext context)
    {
        ModeRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<ModeRequest>(context.Request.Body, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            _log.Error("request", "malformed", "unknown", "unknown", false, ex.Message);
            return Results.BadRequest(new ModeResponse(false, "Malformed request", _stateMachine.CurrentMode.ToString()));
        }

        if (request is null)
        {
            _log.Error("request", "missing", "unknown", "unknown", false, "Empty body.");
            return Results.BadRequest(new ModeResponse(false, "Empty request body", _stateMachine.CurrentMode.ToString()));
        }

        var token = context.Request.Headers.Authorization.ToString();
        if (!TokenValidator.IsAuthorized(token, _config.GetToken()))
        {
            _log.Error(request.Source ?? "iPad", "auth_failed", _stateMachine.CurrentMode.ToString(),
                _stateMachine.CurrentMode.ToString(), false, "Invalid token.");
            return Results.Unauthorized();
        }

        if (!CommandParser.TryParse(request.Command, out var command))
        {
            _log.Error(request.Source ?? "iPad", "invalid_command", _stateMachine.CurrentMode.ToString(),
                _stateMachine.CurrentMode.ToString(), false, request.Command);
            return Results.BadRequest(new ModeResponse(false, "Unknown command", _stateMachine.CurrentMode.ToString()));
        }

        if (!TimestampValidator.IsRecent(request.TimestampUtc, TimeSpan.FromSeconds(_config.AllowedClockSkewSeconds)))
        {
            _log.Error(request.Source ?? "iPad", "stale_timestamp", _stateMachine.CurrentMode.ToString(),
                _stateMachine.CurrentMode.ToString(), false, request.TimestampUtc);
            return Results.StatusCode(StatusCodes.Status408RequestTimeout);
        }

        var source = string.IsNullOrWhiteSpace(request.Source) ? "iPad" : request.Source!;
        var result = _stateMachine.Apply(command, request.Nonce, source);

        if (!result.Ok && result.Message == "duplicate nonce")
        {
            _log.Error(source, command.ToString().ToUpperInvariant(),
                result.PreviousMode.ToString(),
                result.CurrentMode.ToString(),
                false,
                result.Message);
            return Results.Conflict(new ModeResponse(false, "Duplicate nonce", result.CurrentMode.ToString(),
                result.PreviousMode.ToString(), false));
        }

        if (result.Ok && result.Changed)
        {
            var postureResult = _postureController.Apply(result.CurrentMode);
            if (postureResult.Ok)
            {
                _log.Info(source, $"posture_{postureResult.Path}",
                    result.PreviousMode.ToString(),
                    postureResult.CurrentMode.ToString(),
                    postureResult.Ok,
                    postureResult.Message);
            }
            else
            {
                _log.Error(source, $"posture_{postureResult.Path}",
                    result.PreviousMode.ToString(),
                    result.CurrentMode.ToString(),
                    false,
                    postureResult.Message);
                return Results.Problem(
                    title: "Posture change failed",
                    detail: postureResult.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (command == ModeCommand.Tablet)
            {
                _keyboardController.Show();
            }
            else if (command == ModeCommand.Laptop)
            {
                _keyboardController.Hide();
            }
        }

        _log.Info(source, command.ToString().ToUpperInvariant(),
            result.PreviousMode.ToString(),
            result.CurrentMode.ToString(),
            result.Ok,
            result.Message);

        StateChanged?.Invoke(this, new ReceiverStateSnapshot(result.CurrentMode, DescribeEndpoint(), result.Message));
        return Results.Ok(new ModeResponse(result.Ok, result.Message, result.CurrentMode.ToString(),
            result.PreviousMode.ToString(), result.Changed));
    }
}

internal sealed record ReceiverStateSnapshot(SurfaceMode CurrentMode, string Endpoint, string StatusMessage);

internal static class TokenValidator
{
    public static bool IsAuthorized(string authorizationHeader, string expectedToken)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return false;
        }

        var parts = authorizationHeader.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !parts[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var supplied = System.Text.Encoding.UTF8.GetBytes(parts[1].Trim());
        var expected = System.Text.Encoding.UTF8.GetBytes(expectedToken);
        return supplied.Length == expected.Length &&
               CryptographicOperations.FixedTimeEquals(supplied, expected);
    }
}

internal static class CommandParser
{
    public static bool TryParse(string command, out ModeCommand parsed)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            parsed = default;
            return false;
        }

        switch (command.Trim().ToUpperInvariant())
        {
            case "TABLET":
                parsed = ModeCommand.Tablet;
                return true;
            case "LAPTOP":
                parsed = ModeCommand.Laptop;
                return true;
            case "PING":
                parsed = ModeCommand.Ping;
                return true;
            default:
                parsed = default;
                return false;
        }
    }
}

internal static class TimestampValidator
{
    public static bool IsRecent(string timestampUtc, TimeSpan tolerance)
    {
        if (!DateTimeOffset.TryParse(timestampUtc, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return false;
        }

        var delta = DateTimeOffset.UtcNow - parsed.ToUniversalTime();
        return delta.Duration() <= tolerance;
    }
}
