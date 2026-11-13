namespace EetKit.Tests.Fakes;

/// <summary>A clock that always shows the same moment.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}
