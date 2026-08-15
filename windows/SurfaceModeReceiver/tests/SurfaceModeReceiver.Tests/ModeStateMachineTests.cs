namespace SurfaceModeReceiver.Tests;

public class ModeStateMachineTests
{
    [Fact]
    public void TabletThenTabletIsIdempotent()
    {
        var machine = new ModeStateMachine(TimeSpan.FromMinutes(5));

        var first = machine.Apply(ModeCommand.Tablet, Guid.NewGuid().ToString("N"), "test");
        var second = machine.Apply(ModeCommand.Tablet, Guid.NewGuid().ToString("N"), "test");

        Assert.True(first.Ok);
        Assert.True(first.Changed);
        Assert.True(second.Ok);
        Assert.False(second.Changed);
        Assert.Equal(SurfaceMode.Tablet, machine.CurrentMode);
    }

    [Fact]
    public void LaptopThenLaptopIsIdempotent()
    {
        var machine = new ModeStateMachine(TimeSpan.FromMinutes(5));

        var first = machine.Apply(ModeCommand.Laptop, Guid.NewGuid().ToString("N"), "test");
        var second = machine.Apply(ModeCommand.Laptop, Guid.NewGuid().ToString("N"), "test");

        Assert.True(first.Ok);
        Assert.True(first.Changed);
        Assert.True(second.Ok);
        Assert.False(second.Changed);
        Assert.Equal(SurfaceMode.Laptop, machine.CurrentMode);
    }

    [Fact]
    public void DuplicateNonceIsRejected()
    {
        var machine = new ModeStateMachine(TimeSpan.FromMinutes(5));
        var nonce = Guid.NewGuid().ToString("N");

        var first = machine.Apply(ModeCommand.Tablet, nonce, "test");
        var second = machine.Apply(ModeCommand.Laptop, nonce, "test");

        Assert.True(first.Ok);
        Assert.False(second.Ok);
        Assert.Equal(SurfaceMode.Tablet, machine.CurrentMode);
    }
}
