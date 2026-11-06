namespace EetKit.Internal;

/// <summary>Argument checks that compile on every target framework.</summary>
internal static class Guard
{
    public static void NotNull(object? value, string paramName)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value, paramName);
#else
        if (value is null)
        {
            throw new ArgumentNullException(paramName);
        }
#endif
    }
}
