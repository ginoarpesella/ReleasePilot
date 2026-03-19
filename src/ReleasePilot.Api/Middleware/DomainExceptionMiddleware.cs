using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReleasePilot.Domain.Exceptions;

namespace ReleasePilot.Api.Middleware;

public class DomainExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public DomainExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";

            var error = new
            {
                type = "ConcurrencyConflict",
                code = "CONCURRENCY_CONFLICT",
                message = "The promotion was modified by another request. Please retry."
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(error));
        }
        catch (DomainException ex)
        {
            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            context.Response.ContentType = "application/json";

            var error = new
            {
                type = "DomainError",
                code = ex.Code,
                message = ex.Message
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(error));
        }
    }
}
