namespace ScrapGo.Core.Shared.Kernel.Audit;

/// <summary>Who or what caused an audit event.</summary>
public enum AuditActorType
{
    /// <summary>A human user.</summary>
    Human,

    /// <summary>A service principal.</summary>
    Service,

    /// <summary>No actor at all: a system or scheduled process.</summary>
    System,
}
