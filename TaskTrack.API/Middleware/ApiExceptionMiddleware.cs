using Microsoft.AspNetCore.Mvc;
using TaskTrack.Service.Exceptions;

namespace TaskTrack.API.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (AuthenticationException ex) { await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "Authentication required", ex.Message); }
        catch (ConflictException ex) { await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Conflict", ex.Message); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" or "23503" })
        { await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Conflict", "The record conflicts with existing or related data."); }
        catch (ResourceNotFoundException ex) { await WriteProblemAsync(context, StatusCodes.Status404NotFound, "Resource not found", ex.Message); }
        catch (ServiceValidationException ex)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new ValidationProblemDetails(ex.Errors.ToDictionary(x => x.Key, x => x.Value)) { Status = StatusCodes.Status400BadRequest, Title = "Validation failed" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled API error");
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "Server error", "An unexpected error occurred.");
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title, Detail = detail });
    }
}
