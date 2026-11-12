using System.Net;
using AgentForge.Infrastructure.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

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
            ModelProviderConfigurationException => (StatusCodes.Status503ServiceUnavailable, "Model Provider Not Configured", "Configure an API key for Gemini or OpenAI, or make the configured Ollama server reachable from this Codespace."),
            HttpRequestException providerException => (StatusCodes.Status502BadGateway, "Model Provider Error", GetProviderErrorDetail(providerException)),
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

    private static string GetProviderErrorDetail(HttpRequestException exception)
    {
        return exception.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "The provider rejected the API key. Verify that it is valid for the selected provider.",
            HttpStatusCode.Forbidden => "The provider denied access. Verify the API key's project, permissions, API access, and billing or quota settings.",
            HttpStatusCode.BadRequest => "The provider rejected the request. Verify the configured model name and that the model supports the requested response format.",
            HttpStatusCode.NotFound => "The configured model or provider endpoint was not found. Verify the model and endpoint settings.",
            HttpStatusCode.TooManyRequests => "The provider rate limit or quota was reached. Wait and check the provider account's quota before retrying.",
            { } statusCode when (int)statusCode >= 500 => "The provider service is temporarily failing. Try again later.",
            null => "The API could not connect to the provider or received an invalid response. Check the provider endpoint and outbound network access.",
            { } statusCode => $"The provider request failed with HTTP {(int)statusCode}. Check its API key, model, permissions, and quota."
        };
    }
}
