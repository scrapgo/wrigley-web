namespace ScrapGo.Core.Modules.Identity.Domain.Features;

/// <summary>
/// A unit of functionality that owns permissions and has a toggle. Locked
/// features (<see cref="IsToggleable"/> false, e.g. the admin features) are
/// always <see cref="FeatureState.On"/>; the database enforces it.
/// </summary>
public class Feature
{
    private Feature()
    {
    }

    public int Id { get; private set; }

    /// <summary>Unique, e.g. <c>supplier_edit</c>.</summary>
    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public FeatureState State { get; private set; } = FeatureState.On;

    public bool IsToggleable { get; private set; } = true;

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Who last changed the state; null for the seeded state.</summary>
    public int? UpdatedByUserId { get; private set; }

    public static Feature Create(string key, string name, string? description, FeatureState state, bool isToggleable, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!isToggleable && state != FeatureState.On)
        {
            throw new ArgumentException("A locked feature is always On.", nameof(state));
        }

        return new Feature
        {
            Key = key,
            Name = name,
            Description = description ?? string.Empty,
            State = state,
            IsToggleable = isToggleable,
            UpdatedAt = now,
        };
    }

    /// <summary>Changes the toggle. Callers refuse a locked feature first (409 <c>feature_locked</c>).</summary>
    public void ChangeState(FeatureState state, int changedByUserId, DateTimeOffset now)
    {
        if (!IsToggleable)
        {
            throw new InvalidOperationException($"Feature '{Key}' is locked.");
        }

        State = state;
        UpdatedByUserId = changedByUserId;
        UpdatedAt = now;
    }
}
