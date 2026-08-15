using System.Security.Cryptography;
using System.Text.Json;

namespace SurfaceModeReceiver;

internal sealed class ReceiverConfig
{
    public string ListenAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 47889;
    public string TokenProtected { get; set; } = string.Empty;
    public int AllowedClockSkewSeconds { get; set; } = 120;
    public int NonceRetentionSeconds { get; set; } = 300;

    public string GetToken() => SecretProtector.Unprotect(TokenProtected);
}

internal static class ReceiverConfigStore
{
    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SurfaceModeReceiver");

    public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public static ReceiverConfig LoadOrCreate(ReceiverLog log)
    {
        Directory.CreateDirectory(ConfigDirectory);

        if (!File.Exists(ConfigPath))
        {
            var generated = new ReceiverConfig
            {
                TokenProtected = SecretProtector.Protect(TokenGenerator.CreateToken())
            };
            Save(generated);
            log.Info("config", "created", "unknown", "unknown", true, "Generated new receiver config.");
            return generated;
        }

        ReceiverConfig config;
        try
        {
            var json = File.ReadAllText(ConfigPath);
            config = JsonSerializer.Deserialize<ReceiverConfig>(json, JsonOptions.Default) ?? new ReceiverConfig();
        }
        catch (Exception ex)
        {
            log.Error("config", "reload_failed", "unknown", "unknown", false, ex.Message);
            config = new ReceiverConfig();
        }

        if (string.IsNullOrWhiteSpace(config.TokenProtected))
        {
            config.TokenProtected = SecretProtector.Protect(TokenGenerator.CreateToken());
            Save(config);
        }

        if (string.IsNullOrWhiteSpace(config.GetToken()))
        {
            config.TokenProtected = SecretProtector.Protect(TokenGenerator.CreateToken());
            Save(config);
        }

        return config;
    }

    public static void Save(ReceiverConfig config)
    {
        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(config, JsonOptions.Default);
        File.WriteAllText(ConfigPath, json);
    }
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}

internal static class TokenGenerator
{
    public static string CreateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

internal static class SecretProtector
{
    private static readonly byte[] Entropy = "SurfaceModeReceiver:v1"u8.ToArray();

    public static string Protect(string secret)
    {
        var data = ProtectedData.Protect(
            System.Text.Encoding.UTF8.GetBytes(secret),
            Entropy,
            DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(data);
    }

    public static string Unprotect(string protectedValue)
    {
        try
        {
            var data = Convert.FromBase64String(protectedValue);
            var clear = ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(clear);
        }
        catch
        {
            return string.Empty;
        }
    }
}
