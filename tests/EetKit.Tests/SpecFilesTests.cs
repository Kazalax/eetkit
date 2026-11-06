using Xunit;

namespace EetKit.Tests;

public sealed class SpecFilesTests
{
    [Fact]
    public void TestCertificate_ReturnsP12AndPassword()
    {
        var (p12, password) = SpecFiles.TestCertificate("CZ00000019");

        Assert.NotEmpty(p12);
        Assert.False(string.IsNullOrWhiteSpace(password));
    }

    [Fact]
    public void Path_PointsToCopiedSpecFile()
    {
        Assert.True(File.Exists(SpecFiles.Path("EETXMLSchema.xsd")));
    }
}
