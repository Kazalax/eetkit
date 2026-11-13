namespace EetKit.Internal;

/// <summary>Service addresses from the access and operational information documents (DNS names, never IPs).</summary>
internal static class EetEndpoints
{
    private static readonly Uri Playground = new("https://pg.trzbyeet.gov.cz/eet/services/EETServiceSOAP/v4");
    private static readonly Uri Production = new("https://trzbyeet.gov.cz/eet/services/EETServiceSOAP/v4");

    public static Uri For(EetEnvironment environment) => environment switch
    {
        EetEnvironment.Playground => Playground,
        EetEnvironment.Production => Production,
        _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, "Unknown EET environment."),
    };
}
