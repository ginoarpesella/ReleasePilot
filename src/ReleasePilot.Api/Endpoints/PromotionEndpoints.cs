using MediatR;
using ReleasePilot.Application.Commands;
using ReleasePilot.Application.Queries;
using ReleasePilot.Domain.Enums;

namespace ReleasePilot.Api.Endpoints;

public static class PromotionEndpoints
{
    public static void MapPromotionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/promotions").WithTags("Promotions");

        group.MapPost("/", RequestPromotion);
        group.MapPost("/{id:guid}/approve", ApprovePromotion);
        group.MapPost("/{id:guid}/deploy", StartDeployment);
        group.MapPost("/{id:guid}/complete", CompletePromotion);
        group.MapPost("/{id:guid}/rollback", RollbackPromotion);
        group.MapPost("/{id:guid}/cancel", CancelPromotion);

        group.MapGet("/{id:guid}", GetPromotionById);
        group.MapGet("/application/{applicationName}", ListPromotionsByApplication);
        group.MapGet("/application/{applicationName}/status", GetEnvironmentStatus);
    }

    private static async Task<IResult> RequestPromotion(
        RequestPromotionRequest request,
        IMediator mediator)
    {
        var command = new RequestPromotionCommand(
            request.ApplicationName,
            request.Version,
            request.SourceEnvironment,
            request.TargetEnvironment,
            request.RequestedBy,
            request.WorkItemReferences);

        var id = await mediator.Send(command);
        return Results.Created($"/api/promotions/{id}", new { id });
    }

    private static async Task<IResult> ApprovePromotion(
        Guid id,
        ApprovePromotionRequest request,
        IMediator mediator)
    {
        await mediator.Send(new ApprovePromotionCommand(id, request.ApprovedBy, request.IsApprover));
        return Results.Ok(new { message = "Promotion approved." });
    }

    private static async Task<IResult> StartDeployment(
        Guid id,
        StartDeploymentRequest request,
        IMediator mediator)
    {
        await mediator.Send(new StartDeploymentCommand(id, request.StartedBy));
        return Results.Ok(new { message = "Deployment started." });
    }

    private static async Task<IResult> CompletePromotion(
        Guid id,
        IMediator mediator)
    {
        await mediator.Send(new CompletePromotionCommand(id));
        return Results.Ok(new { message = "Promotion completed." });
    }

    private static async Task<IResult> RollbackPromotion(
        Guid id,
        RollbackPromotionRequest request,
        IMediator mediator)
    {
        await mediator.Send(new RollbackPromotionCommand(id, request.Reason, request.RolledBackBy));
        return Results.Ok(new { message = "Promotion rolled back." });
    }

    private static async Task<IResult> CancelPromotion(
        Guid id,
        CancelPromotionRequest request,
        IMediator mediator)
    {
        await mediator.Send(new CancelPromotionCommand(id, request.CancelledBy));
        return Results.Ok(new { message = "Promotion cancelled." });
    }

    private static async Task<IResult> GetPromotionById(
        Guid id,
        IMediator mediator)
    {
        var result = await mediator.Send(new GetPromotionByIdQuery(id));
        return Results.Ok(result);
    }

    private static async Task<IResult> ListPromotionsByApplication(
        string applicationName,
        int page,
        int pageSize,
        IMediator mediator)
    {
        var result = await mediator.Send(new ListPromotionsByApplicationQuery(applicationName, page, pageSize));
        return Results.Ok(result);
    }

    private static async Task<IResult> GetEnvironmentStatus(
        string applicationName,
        IMediator mediator)
    {
        var result = await mediator.Send(new GetEnvironmentStatusQuery(applicationName));
        return Results.Ok(result);
    }
}

// Request DTOs
public record RequestPromotionRequest(
    string ApplicationName,
    string Version,
    DeploymentEnvironment SourceEnvironment,
    DeploymentEnvironment TargetEnvironment,
    string RequestedBy,
    List<string>? WorkItemReferences = null);

public record ApprovePromotionRequest(string ApprovedBy, bool IsApprover);
public record StartDeploymentRequest(string StartedBy);
public record RollbackPromotionRequest(string Reason, string RolledBackBy);
public record CancelPromotionRequest(string CancelledBy);
