namespace EetKit;

/// <summary>Certifikát poplatníka se nepodařilo načíst nebo nejde použít k podpisu.</summary>
public sealed class EetCertificateException : Exception
{
    /// <summary>Vytvoří výjimku se zprávou.</summary>
    public EetCertificateException(string message)
        : base(message)
    {
    }

    /// <summary>Vytvoří výjimku se zprávou a původní příčinou.</summary>
    public EetCertificateException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
