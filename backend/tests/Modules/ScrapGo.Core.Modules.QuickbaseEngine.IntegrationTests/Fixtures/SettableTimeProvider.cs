namespace ScrapGo.Core.Modules.QuickbaseEngine.IntegrationTests.Fixtures;

/// <summary>A clock the specs move by hand, so TTL expiry is tested without sleeping.</summary>
public sealed class SettableTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
