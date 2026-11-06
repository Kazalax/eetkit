namespace EetKit;

/// <summary>Data tržby nejsou platná. Taková tržba se neodešle.</summary>
public sealed class EetValidationException : Exception
{
    /// <summary>Vytvoří výjimku se seznamem chyb.</summary>
    /// <param name="errors">Chyby jednotlivých položek.</param>
    public EetValidationException(IReadOnlyList<EetValidationError> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    /// <summary>Chyby jednotlivých položek.</summary>
    public IReadOnlyList<EetValidationError> Errors { get; }

    private static string BuildMessage(IReadOnlyList<EetValidationError> errors) =>
        "Data tržby nejsou platná: " + string.Join(", ", (errors ?? []).Select(e => e.Field)) + ".";
}
