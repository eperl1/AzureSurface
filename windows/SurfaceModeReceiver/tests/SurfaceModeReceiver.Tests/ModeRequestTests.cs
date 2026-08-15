using System.Text.Json;

namespace SurfaceModeReceiver.Tests;

public class ModeRequestTests
{
    [Fact]
    public void DeserializesValidRequest()
    {
        var json = """
        {"command":"TABLET","timestampUtc":"2026-08-15T00:00:00Z","nonce":"abc","source":"iPad"}
        """;

        var request = JsonSerializer.Deserialize<ModeRequest>(json, JsonOptions.Default);

        Assert.NotNull(request);
        Assert.Equal("TABLET", request!.Command);
        Assert.Equal("abc", request.Nonce);
    }

    [Fact]
    public void RejectsMalformedJson()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ModeRequest>("{not-json}", JsonOptions.Default));
    }

    [Theory]
    [InlineData("2026-08-15T00:00:00Z", true)]
    [InlineData("2000-01-01T00:00:00Z", false)]
    public void ValidatesTimestampFreshness(string timestamp, bool expected)
    {
        var tolerance = TimeSpan.FromDays(365);
        Assert.Equal(expected, TimestampValidator.IsRecent(timestamp, tolerance));
    }
}
