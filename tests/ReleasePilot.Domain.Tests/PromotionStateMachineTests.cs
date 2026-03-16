using FluentAssertions;
using ReleasePilot.Domain.Aggregates;
using ReleasePilot.Domain.Enums;
using ReleasePilot.Domain.Events;
using ReleasePilot.Domain.Exceptions;

namespace ReleasePilot.Domain.Tests;

public class PromotionStateMachineTests
{
    private static Promotion CreateRequestedPromotion(
        DeploymentEnvironment source = DeploymentEnvironment.Dev,
        DeploymentEnvironment target = DeploymentEnvironment.Staging,
        string app = "MyApp",
        string version = "1.0.0")
    {
        return Promotion.Request(app, version, source, target, "user@example.com", ["WI-101", "WI-102"]);
    }

    // ==================== RequestPromotion ====================

    [Fact]
    public void Request_ValidParameters_CreatesPromotionInRequestedState()
    {
        var promotion = CreateRequestedPromotion();

        promotion.Status.Should().Be(PromotionStatus.Requested);
        promotion.ApplicationName.Should().Be("MyApp");
        promotion.Version.Should().Be("1.0.0");
        promotion.SourceEnvironment.Should().Be(DeploymentEnvironment.Dev);
        promotion.TargetEnvironment.Should().Be(DeploymentEnvironment.Staging);
        promotion.RequestedBy.Should().Be("user@example.com");
        promotion.WorkItemReferences.Should().HaveCount(2);
        promotion.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Request_EmitsPromotionRequestedEvent()
    {
        var promotion = CreateRequestedPromotion();

        promotion.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PromotionRequested>();
    }

    [Fact]
    public void Request_RecordsStateTransition()
    {
        var promotion = CreateRequestedPromotion();

        promotion.StateHistory.Should().ContainSingle();
        promotion.StateHistory[0].ToStatus.Should().Be(PromotionStatus.Requested);
    }

    [Theory]
    [InlineData("", "1.0.0", "user")]
    [InlineData("MyApp", "", "user")]
    [InlineData("MyApp", "1.0.0", "")]
    public void Request_MissingRequiredFields_ThrowsDomainException(string app, string version, string user)
    {
        var act = () => Promotion.Request(app, version, DeploymentEnvironment.Dev, DeploymentEnvironment.Staging, user);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Request_SkippingEnvironment_ThrowsDomainException()
    {
        var act = () => Promotion.Request("MyApp", "1.0.0", DeploymentEnvironment.Dev, DeploymentEnvironment.Production, "user");

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot promote*");
    }

    [Fact]
    public void Request_StagingToProduction_Succeeds()
    {
        var promotion = Promotion.Request("MyApp", "1.0.0", DeploymentEnvironment.Staging, DeploymentEnvironment.Production, "user");

        promotion.Status.Should().Be(PromotionStatus.Requested);
        promotion.SourceEnvironment.Should().Be(DeploymentEnvironment.Staging);
        promotion.TargetEnvironment.Should().Be(DeploymentEnvironment.Production);
    }

    // ==================== ApprovePromotion ====================

    [Fact]
    public void Approve_FromRequested_WithApproverRole_TransitionsToApproved()
    {
        var promotion = CreateRequestedPromotion();

        promotion.Approve("approver@example.com", isApprover: true, hasInProgressPromotionForEnvironment: false);

        promotion.Status.Should().Be(PromotionStatus.Approved);
    }

    [Fact]
    public void Approve_EmitsPromotionApprovedEvent()
    {
        var promotion = CreateRequestedPromotion();
        promotion.ClearDomainEvents();

        promotion.Approve("approver@example.com", isApprover: true, hasInProgressPromotionForEnvironment: false);

        promotion.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PromotionApproved>();
    }

    [Fact]
    public void Approve_WithoutApproverRole_ThrowsDomainException()
    {
        var promotion = CreateRequestedPromotion();

        var act = () => promotion.Approve("user@example.com", isApprover: false, hasInProgressPromotionForEnvironment: false);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "UNAUTHORIZED_APPROVER");
    }

    [Fact]
    public void Approve_WhenEnvironmentHasInProgressPromotion_ThrowsDomainException()
    {
        var promotion = CreateRequestedPromotion();

        var act = () => promotion.Approve("approver@example.com", isApprover: true, hasInProgressPromotionForEnvironment: true);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "ENVIRONMENT_LOCKED");
    }

    [Fact]
    public void Approve_FromApprovedState_ThrowsDomainException()
    {
        var promotion = CreateRequestedPromotion();
        promotion.Approve("approver@example.com", isApprover: true, hasInProgressPromotionForEnvironment: false);

        var act = () => promotion.Approve("approver@example.com", isApprover: true, hasInProgressPromotionForEnvironment: false);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_TRANSITION");
    }

    // ==================== StartDeployment ====================

    [Fact]
    public void StartDeployment_FromApproved_TransitionsToInProgress()
    {
        var promotion = CreateRequestedPromotion();
        promotion.Approve("approver@example.com", true, false);

        promotion.StartDeployment("deployer@example.com");

        promotion.Status.Should().Be(PromotionStatus.InProgress);
    }

    [Fact]
    public void StartDeployment_EmitsDeploymentStartedEvent()
    {
        var promotion = CreateRequestedPromotion();
        promotion.Approve("approver@example.com", true, false);
        promotion.ClearDomainEvents();

        promotion.StartDeployment("deployer@example.com");

        promotion.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DeploymentStarted>();
    }

    [Fact]
    public void StartDeployment_FromRequested_ThrowsDomainException()
    {
        var promotion = CreateRequestedPromotion();

        var act = () => promotion.StartDeployment("deployer@example.com");

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_TRANSITION");
    }

    // ==================== CompletePromotion ====================

    [Fact]
    public void Complete_FromInProgress_TransitionsToCompleted()
    {
        var promotion = CreateRequestedPromotion();
        promotion.Approve("approver@example.com", true, false);
        promotion.StartDeployment("deployer@example.com");

        promotion.Complete();

        promotion.Status.Should().Be(PromotionStatus.Completed);
        promotion.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_EmitsPromotionCompletedEvent()
    {
        var promotion = CreateRequestedPromotion();
        promotion.Approve("approver@example.com", true, false);
        promotion.StartDeployment("deployer@example.com");
        promotion.ClearDomainEvents();

        promotion.Complete();

        promotion.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PromotionCompleted>();
    }

    [Fact]
    public void Complete_FromRequested_ThrowsDomainException()
    {
        var promotion = CreateRequestedPromotion();

        var act = () => promotion.Complete();

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_TRANSITION");
    }

    // ==================== RollbackPromotion ====================

    [Fact]
    public void Rollback_FromInProgress_WithReason_TransitionsToRolledBack()
    {
        var promotion = CreateRequestedPromotion();
        promotion.Approve("approver@example.com", true, false);
        promotion.StartDeployment("deployer@example.com");

        promotion.Rollback("Critical bug found in staging", "operator@example.com");

        promotion.Status.Should().Be(PromotionStatus.RolledBack);
        promotion.RollbackReason.Should().Be("Critical bug found in staging");
    }

    [Fact]
    public void Rollback_EmitsPromotionRolledBackEvent()
    {
        var promotion = CreateRequestedPromotion();
        promotion.Approve("approver@example.com", true, false);
        promotion.StartDeployment("deployer@example.com");
        promotion.ClearDomainEvents();

        promotion.Rollback("Reason", "operator@example.com");

        promotion.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PromotionRolledBack>();
    }

    [Fact]
    public void Rollback_WithoutReason_ThrowsDomainException()
    {
        var promotion = CreateRequestedPromotion();
        promotion.Approve("approver@example.com", true, false);
        promotion.StartDeployment("deployer@example.com");

        var act = () => promotion.Rollback("", "operator@example.com");

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "REASON_REQUIRED");
    }

    [Fact]
    public void Rollback_FromRequested_ThrowsDomainException()
    {
        var promotion = CreateRequestedPromotion();

        var act = () => promotion.Rollback("Reason", "operator@example.com");

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_TRANSITION");
    }

    // ==================== CancelPromotion ====================

    [Fact]
    public void Cancel_FromRequested_TransitionsToCancelled()
    {
        var promotion = CreateRequestedPromotion();

        promotion.Cancel("user@example.com");

        promotion.Status.Should().Be(PromotionStatus.Cancelled);
    }

    [Fact]
    public void Cancel_EmitsPromotionCancelledEvent()
    {
        var promotion = CreateRequestedPromotion();
        promotion.ClearDomainEvents();

        promotion.Cancel("user@example.com");

        promotion.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PromotionCancelled>();
    }

    [Fact]
    public void Cancel_FromApproved_ThrowsDomainException()
    {
        var promotion = CreateRequestedPromotion();
        promotion.Approve("approver@example.com", true, false);

        var act = () => promotion.Cancel("user@example.com");

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_TRANSITION");
    }

    [Fact]
    public void Cancel_FromInProgress_ThrowsDomainException()
    {
        var promotion = CreateRequestedPromotion();
        promotion.Approve("approver@example.com", true, false);
        promotion.StartDeployment("deployer@example.com");

        var act = () => promotion.Cancel("user@example.com");

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_TRANSITION");
    }

    // ==================== Immutability ====================

    [Fact]
    public void CompletedPromotion_CannotBeModified()
    {
        var promotion = CreateRequestedPromotion();
        promotion.Approve("approver@example.com", true, false);
        promotion.StartDeployment("deployer@example.com");
        promotion.Complete();

        var act1 = () => promotion.Approve("a", true, false);
        var act2 = () => promotion.StartDeployment("a");
        var act3 = () => promotion.Complete();
        var act4 = () => promotion.Rollback("r", "a");
        var act5 = () => promotion.Cancel("a");

        act1.Should().Throw<DomainException>().Where(e => e.Code == "PROMOTION_IMMUTABLE");
        act2.Should().Throw<DomainException>().Where(e => e.Code == "PROMOTION_IMMUTABLE");
        act3.Should().Throw<DomainException>().Where(e => e.Code == "PROMOTION_IMMUTABLE");
        act4.Should().Throw<DomainException>().Where(e => e.Code == "PROMOTION_IMMUTABLE");
        act5.Should().Throw<DomainException>().Where(e => e.Code == "PROMOTION_IMMUTABLE");
    }

    [Fact]
    public void CancelledPromotion_CannotBeModified()
    {
        var promotion = CreateRequestedPromotion();
        promotion.Cancel("user@example.com");

        var act1 = () => promotion.Approve("a", true, false);
        var act2 = () => promotion.Cancel("a");

        act1.Should().Throw<DomainException>().Where(e => e.Code == "PROMOTION_IMMUTABLE");
        act2.Should().Throw<DomainException>().Where(e => e.Code == "PROMOTION_IMMUTABLE");
    }

    // ==================== State History Tracking ====================

    [Fact]
    public void FullLifecycle_TracksAllStateTransitions()
    {
        var promotion = CreateRequestedPromotion();
        promotion.Approve("approver@example.com", true, false);
        promotion.StartDeployment("deployer@example.com");
        promotion.Complete();

        promotion.StateHistory.Should().HaveCount(4);
        promotion.StateHistory[0].ToStatus.Should().Be(PromotionStatus.Requested);
        promotion.StateHistory[1].ToStatus.Should().Be(PromotionStatus.Approved);
        promotion.StateHistory[2].ToStatus.Should().Be(PromotionStatus.InProgress);
        promotion.StateHistory[3].ToStatus.Should().Be(PromotionStatus.Completed);
    }

    [Fact]
    public void RollbackLifecycle_TracksAllStateTransitions()
    {
        var promotion = CreateRequestedPromotion();
        promotion.Approve("approver@example.com", true, false);
        promotion.StartDeployment("deployer@example.com");
        promotion.Rollback("Issue found", "operator@example.com");

        promotion.StateHistory.Should().HaveCount(4);
        promotion.StateHistory[3].ToStatus.Should().Be(PromotionStatus.RolledBack);
        promotion.StateHistory[3].Reason.Should().Be("Issue found");
    }

    // ==================== Environment Validation ====================

    [Theory]
    [InlineData(DeploymentEnvironment.Dev, DeploymentEnvironment.Staging)]
    [InlineData(DeploymentEnvironment.Staging, DeploymentEnvironment.Production)]
    public void Request_ValidEnvironmentPath_Succeeds(DeploymentEnvironment source, DeploymentEnvironment target)
    {
        var promotion = Promotion.Request("App", "1.0", source, target, "user");
        promotion.Status.Should().Be(PromotionStatus.Requested);
    }

    [Theory]
    [InlineData(DeploymentEnvironment.Dev, DeploymentEnvironment.Production)]
    [InlineData(DeploymentEnvironment.Production, DeploymentEnvironment.Dev)]
    [InlineData(DeploymentEnvironment.Staging, DeploymentEnvironment.Dev)]
    [InlineData(DeploymentEnvironment.Production, DeploymentEnvironment.Staging)]
    public void Request_InvalidEnvironmentPath_ThrowsDomainException(DeploymentEnvironment source, DeploymentEnvironment target)
    {
        var act = () => Promotion.Request("App", "1.0", source, target, "user");
        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_PROMOTION_PATH");
    }

    // ==================== Domain Events Collection ====================

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var promotion = CreateRequestedPromotion();
        promotion.DomainEvents.Should().NotBeEmpty();

        promotion.ClearDomainEvents();

        promotion.DomainEvents.Should().BeEmpty();
    }
}
