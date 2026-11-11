using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AgentForge.Infrastructure.Services;

namespace AgentForge.Api.Middleware;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found", "The requested resource was not found."),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid Request", "One or more request values are invalid."),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Request Conflict", "The request cannot be completed in the current state."),
            ModelProviderConfigurationException => (StatusCodes.Status503ServiceUnavailable, "Model Provider Not Configured", "The selected model provider is not configured."),
            HttpRequestException => (StatusCodes.Status502BadGateway, "Model Provider Error", "The configured model provider is unavailable."),
            TimeoutException => (StatusCodes.Status504GatewayTimeout, "Model Provider Timeout", "The configured model provider did not respond in time."),
            _ => (0, string.Empty, string.Empty)
        };

        if (statusCode == 0)
        {
            logger.LogError(exception, "Unhandled API request failure for {Path}", httpContext.Request.Path);
            return false;
        }

        logger.LogWarning(exception, "API request failed with {StatusCode} for {Path}", statusCode, httpContext.Request.Path);
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        }, cancellationToken);
        return true;
    }
}
