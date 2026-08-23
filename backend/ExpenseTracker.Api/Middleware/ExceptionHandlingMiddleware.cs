using ExpenseTracker.Api.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = ex.StatusCode;
            var problem = new ProblemDetails
            {
                Status = ex.StatusCode,
                Title = ex.Message,
            };
            await context.Response.WriteAsJsonAsync(problem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = 500;
            var problem = new ProblemDetails
            {
                Status = 500,
                Title = "An unexpected error occurred.",
            };
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
