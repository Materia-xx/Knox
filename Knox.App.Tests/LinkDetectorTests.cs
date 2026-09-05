using Knox.App.Logic.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Knox.App.Tests;

[TestClass]
public sealed class LinkDetectorTests
{
    [TestMethod]
    [DataRow("http://example.com")]
    [DataRow("https://portal.azure.com/foo?bar=1")]
    [DataRow("  https://trimmed.example.com  ")]
    public void IsHttpLink_TrueForAbsoluteHttpUrls(string value)
    {
        Assert.IsTrue(LinkDetector.IsHttpLink(value));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    [DataRow("just a value")]
    [DataRow("ftp://example.com")]
    [DataRow("mailto:someone@example.com")]
    [DataRow("example.com")]
    public void IsHttpLink_FalseForNonHttpValues(string? value)
    {
        Assert.IsFalse(LinkDetector.IsHttpLink(value));
    }
}
