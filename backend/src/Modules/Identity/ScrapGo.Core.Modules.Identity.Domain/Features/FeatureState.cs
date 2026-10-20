namespace ScrapGo.Core.Modules.Identity.Domain.Features;

/// <summary>A feature's toggle. A toggle never grants access; it only exposes a feature to people whose roles already hold its permissions.</summary>
public enum FeatureState
{
    Off,

    On,

    /// <summary>On only for the feature's <see cref="FeatureTarget"/>s: chosen users, or holders of chosen roles.</summary>
    Targeted,
}
