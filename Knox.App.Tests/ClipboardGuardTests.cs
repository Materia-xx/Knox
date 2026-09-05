using Knox.App.Logic.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Knox.App.Tests;

[TestClass]
public sealed class ClipboardGuardTests
{
    [TestMethod]
    public void ShouldClear_WhenClipboardStillHoldsTrackedSecret_ReturnsTrue()
    {
        var guard = new ClipboardGuard();
        guard.Track("s3cr3t");

        Assert.IsTrue(guard.HasTrackedValue);
        Assert.IsTrue(guard.ShouldClear("s3cr3t"));
    }

    [TestMethod]
    public void ShouldClear_WhenUserCopiedSomethingElse_ReturnsFalse()
    {
        var guard = new ClipboardGuard();
        guard.Track("s3cr3t");

        Assert.IsFalse(guard.ShouldClear("a grocery list"),
            "must not wipe clipboard content the user copied after the secret");
    }

    [TestMethod]
    public void ShouldClear_WhenNothingTracked_ReturnsFalse()
    {
        var guard = new ClipboardGuard();
        Assert.IsFalse(guard.HasTrackedValue);
        Assert.IsFalse(guard.ShouldClear("anything"));
    }

    [TestMethod]
    public void ShouldClear_WhenClipboardEmptied_ReturnsFalse()
    {
        var guard = new ClipboardGuard();
        guard.Track("s3cr3t");
        Assert.IsFalse(guard.ShouldClear(null));
    }

    [TestMethod]
    public void Reset_StopsTracking()
    {
        var guard = new ClipboardGuard();
        guard.Track("s3cr3t");
        guard.Reset();

        Assert.IsFalse(guard.HasTrackedValue);
        Assert.IsFalse(guard.ShouldClear("s3cr3t"));
    }
}
