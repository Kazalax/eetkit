using Xunit;

namespace EetKit.Tests;

public sealed class EetCertificateTests
{
    private const string Eic = "CZ00000019";

    [Theory]
    [InlineData("CZ00000019")]
    [InlineData("CZ683555118")]
    [InlineData("CZ8551015704")]
    public void Loads_AllThreePlaygroundCertificates(string eic)
    {
        var (p12, password) = SpecFiles.TestCertificate(eic);

        using var certificate = EetCertificate.FromPkcs12(p12, password);

        Assert.Equal(eic, certificate.Eic);
        Assert.NotNull(certificate.PrivateKey);
    }

    [Fact]
    public void NotAfter_MatchesCertificate()
    {
        var (p12, password) = SpecFiles.TestCertificate(Eic);

        using var certificate = EetCertificate.FromPkcs12(p12, password);

        Assert.Equal(new DateTimeOffset(2027, 5, 14, 9, 40, 23, TimeSpan.Zero), certificate.NotAfter);
    }

    [Fact]
    public void FromPkcs12File_LoadsCertificate()
    {
        var (p12, password) = SpecFiles.TestCertificate(Eic);
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"eetkit-{Guid.NewGuid():N}.p12");
        File.WriteAllBytes(path, p12);
        try
        {
            using var certificate = EetCertificate.FromPkcs12File(path, password);

            Assert.Equal(Eic, certificate.Eic);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WrongPassword_ThrowsWithoutPasswordInMessage()
    {
        const string wrongPassword = "spatne-heslo-123";
        var (p12, _) = SpecFiles.TestCertificate(Eic);

        var exception = Assert.Throws<EetCertificateException>(() => EetCertificate.FromPkcs12(p12, wrongPassword));

        Assert.DoesNotContain(wrongPassword, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(wrongPassword, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CorruptedData_Throws()
    {
        var garbage = new byte[512];
        new Random(42).NextBytes(garbage);

        Assert.Throws<EetCertificateException>(() => EetCertificate.FromPkcs12(garbage, "heslo"));
    }

    [Fact]
    public void GetWarnings_EicMismatch()
    {
        var (p12, password) = SpecFiles.TestCertificate(Eic);
        using var certificate = EetCertificate.FromPkcs12(p12, password);

        var warnings = certificate.GetWarnings("CZ683555118", new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero));

        Assert.Single(warnings);
    }

    [Fact]
    public void GetWarnings_Expired()
    {
        var (p12, password) = SpecFiles.TestCertificate(Eic);
        using var certificate = EetCertificate.FromPkcs12(p12, password);

        var warnings = certificate.GetWarnings(Eic, new DateTimeOffset(2027, 5, 15, 0, 0, 0, TimeSpan.Zero));

        Assert.Single(warnings);
    }

    [Fact]
    public void GetWarnings_ValidAndMatching_Empty()
    {
        var (p12, password) = SpecFiles.TestCertificate(Eic);
        using var certificate = EetCertificate.FromPkcs12(p12, password);

        var warnings = certificate.GetWarnings(Eic, new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero));

        Assert.Empty(warnings);
    }
}
