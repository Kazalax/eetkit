using System.IO.Compression;
using System.Text;
using EetKit;
using EetKit.Extensions.DependencyInjection;

// Sample: sends a test sale to the EET playground with the official test certificate.
// Never point this sample at production; it has no real certificate and no real sales.
var builder = WebApplication.CreateBuilder(args);

const string DefaultEic = "CZ00000019";
const int SampleIdJednotky = 303;
const string SampleIdPokl = "EetKit/Ukazka";

var eic = builder.Configuration["Sample:Eic"] ?? DefaultEic;
var archive = builder.Configuration["Sample:PlaygroundCertificates"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "..", "..", "docs", "spec", "CAEET_Playground_2026_v1.zip");
var (p12, password) = PlaygroundCertificates.Load(archive, eic);

builder.Services.AddEet(options =>
{
    options.Environment = EetEnvironment.Playground;
    options.CertificateBytes = p12;
    options.CertificatePassword = password;
});

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/trzba", async (SaleRequest request, EetClient client, CancellationToken cancellationToken) =>
{
    var trzba = new Trzba(eic, SampleIdJednotky, SampleIdPokl, request.PoradCis ?? string.Empty, DateTimeOffset.Now, request.CelkTrzba);
    try
    {
        var result = await client.SendAsync(trzba, new SendOptions(request.PrvniZaslani, request.Overeni), cancellationToken);
        return Results.Ok(SaleResponse.From(result, eic));
    }
    catch (EetValidationException ex)
    {
        return Results.ValidationProblem(
            ex.Errors.GroupBy(e => e.Field).ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray()),
            title: "Data tržby nejsou platná");
    }
});

app.Run();

internal sealed record SaleRequest(decimal CelkTrzba, string? PoradCis, bool PrvniZaslani = true, bool Overeni = false);

internal sealed record SaleResponse(
    string Status,
    string? Pok,
    int? ErrorCode,
    string? ErrorText,
    IReadOnlyList<EetWarning> Warnings,
    IReadOnlyList<string> LocalWarnings,
    Guid MessageUuid,
    DateTimeOffset? DatPrij,
    bool IsTest,
    bool IsRetryable,
    string? GlobalTransactionId,
    string Eic)
{
    public static SaleResponse From(EetResult result, string eic) => new(
        result.Status.ToString(),
        result.Pok,
        result.ErrorCode,
        result.ErrorText,
        result.Warnings,
        result.LocalWarnings,
        result.MessageUuid,
        result.DatPrij,
        result.IsTest,
        result.IsRetryable,
        result.GlobalTransactionId,
        eic);
}

/// <summary>Reads a playground .p12 and its password from the official archive in docs/spec.</summary>
internal static class PlaygroundCertificates
{
    private const string PasswordEntry = "password_pokladni_cert_playground.txt";

    public static (byte[] P12, string Password) Load(string archivePath, string eic)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        return (Read(archive, $"CA_EET-Playground-{eic}.p12"), Encoding.ASCII.GetString(Read(archive, PasswordEntry)).TrimEnd('\r', '\n'));
    }

    private static byte[] Read(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new FileNotFoundException($"{name} is missing in the playground archive.");
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
