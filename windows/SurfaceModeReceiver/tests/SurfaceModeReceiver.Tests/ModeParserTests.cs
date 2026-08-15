namespace SurfaceModeReceiver.Tests;

public class ModeParserTests
{
    [Theory]
    [InlineData("TABLET", true)]
    [InlineData("laptop", true)]
    [InlineData("PING", true)]
    [InlineData("noop", false)]
    public void ParsesKnownCommands(string command, bool expected)
    {
        Assert.Equal(expected, CommandParser.TryParse(command, out _));
    }

    [Fact]
    public void RejectsInvalidTokens()
    {
        Assert.False(TokenValidator.IsAuthorized("Bearer wrong", "right"));
        Assert.False(TokenValidator.IsAuthorized("", "right"));
        Assert.False(TokenValidator.IsAuthorized("Basic abc", "right"));
    }
}
