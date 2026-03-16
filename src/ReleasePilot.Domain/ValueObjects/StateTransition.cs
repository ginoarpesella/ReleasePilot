using ReleasePilot.Domain.Enums;

namespace ReleasePilot.Domain.ValueObjects;

public record StateTransition(
    PromotionStatus FromStatus,
    PromotionStatus ToStatus,
    string ActingUser,
    DateTime TransitionedAt,
    string? Reason = null);
