namespace SurfaceModeReceiver.Tests;

public class SurfacePostureControllerTests
{
    [Fact]
    public void ApplyingTheCurrentModeIsANoOp()
    {
        var controller = new SurfacePostureController(new ReceiverLog());

        var result = controller.Apply(controller.CurrentMode);

        Assert.True(result.Ok);
        Assert.False(result.Changed);
        Assert.Equal("no-op", result.Path);
    }
}
