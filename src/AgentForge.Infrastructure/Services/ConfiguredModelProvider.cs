using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentForge.Application.Contracts;
using Microsoft.Extensions.Configuration;

namespace AgentForge.Infrastructure.Services;

public sealed class ConfiguredModelProvider(
    string name,
    HttpClient httpClient,
    IConfiguration configuration) : IModelProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name { get; } = name;
    public string DefaultModel => configuration[$"Models:{Name}:Model"] ?? Name switch
    {
        "Gemini" => "gemini-2.5-flash",
        "OpenAI" => "gpt-4o-mini",
        "Ollama" => "llama3.2",
        _ => throw new InvalidOperationException($"Model provider '{Name}' is not supported.")
    };

    public async Task<string> CompleteAsync(ModelCompletionRequest request, CancellationToken cancellationToken)
    {
        var body = Name switch
        {
            "Gemini" => BuildGeminiRequest(request),
            "OpenAI" => BuildOpenAiRequest(request),
            "Ollama" => BuildOllamaRequest(request),
            _ => throw new InvalidOperationException($"Model provider '{Name}' is not supported.")
        };
        var endpoint = GetEndpoint(request.Model);

        for (var attempt = 0; ; attempt++)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };
            if (Name == "OpenAI")
            {
                var apiKey = configuration["Models:OpenAI:ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new ModelProviderConfigurationException("OpenAI credentials are not configured.");
                }

                message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            }
            else if (Name == "Gemini")
            {
                var apiKey = configuration["Models:Gemini:ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new ModelProviderConfigurationException("Gemini credentials are not configured.");
                }

                message.Headers.Add("x-goog-api-key", apiKey);
            }

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"{Name} model request timed out.", exception);
            }

            using (response)
            {
                if (IsRetryable(response.StatusCode) && attempt < 2)
                {
                    var retryDelay = response.Headers.RetryAfter?.Delta
                        ?? TimeSpan.FromMilliseconds(250 * (attempt + 1));
                    var delay = TimeSpan.FromMilliseconds(Math.Clamp(retryDelay.TotalMilliseconds, 0, 5000));
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"{Name} model request failed with HTTP {(int)response.StatusCode}.",
                        null,
                        response.StatusCode);
                }

                try
                {
                    using var result = await JsonDocument.ParseAsync(
                        await response.Content.ReadAsStreamAsync(cancellationToken),
                        cancellationToken: cancellationToken);
                    return ExtractText(result.RootElement);
                }
                catch (JsonException exception)
                {
                    throw new HttpRequestException($"{Name} returned an invalid response.", exception);
                }
            }
        }
    }

    private object BuildGeminiRequest(ModelCompletionRequest request)
    {
        var apiKey = configuration["Models:Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Gemini is selected but Models:Gemini:ApiKey is not configured.");
        }

        return new
        {
            systemInstruction = new { parts = new[] { new { text = request.SystemPrompt } } },
            contents = request.Messages.Select(message => new
            {
                role = message.Role == "assistant" ? "model" : "user",
                parts = new[] { new { text = message.Content } }
            }),
            generationConfig = request.RequireJson ? new { responseMimeType = "application/json" } : null
        };
    }

    private static object BuildOpenAiRequest(ModelCompletionRequest request)
    {
        var messages = new List<object> { new { role = "system", content = request.SystemPrompt } };
        messages.AddRange(request.Messages.Select(message => (object)new { message.Role, message.Content }));
        return new
        {
            model = request.Model,
            messages,
            response_format = request.RequireJson ? new { type = "json_object" } : null
        };
    }

    private static object BuildOllamaRequest(ModelCompletionRequest request)
    {
        var messages = new List<object> { new { role = "system", content = request.SystemPrompt } };
        messages.AddRange(request.Messages.Select(message => (object)new { message.Role, message.Content }));
        return new
        {
            model = request.Model,
            messages,
            stream = false,
            format = request.RequireJson ? "json" : null
        };
    }

    private string GetEndpoint(string model)
    {
        return Name switch
        {
            "Gemini" => $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent",
            "OpenAI" => configuration["Models:OpenAI:Endpoint"] ?? "https://api.openai.com/v1/chat/completions",
            "Ollama" => $"{(configuration["Models:Ollama:BaseUrl"] ?? "http://localhost:11434").TrimEnd('/')}/api/chat",
            _ => throw new InvalidOperationException($"Model provider '{Name}' is not supported.")
        };
    }

    private string ExtractText(JsonElement root)
    {
        try
        {
            var text = Name switch
            {
                "Gemini" => root.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString(),
                "OpenAI" => root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString(),
                "Ollama" => root.GetProperty("message").GetProperty("content").GetString(),
                _ => null
            };

            return !string.IsNullOrWhiteSpace(text)
                ? text
                : throw new HttpRequestException($"{Name} returned no text content.");
        }
        catch (Exception exception) when (exception is KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
        {
            throw new HttpRequestException($"{Name} returned an invalid response.", exception);
        }
    }

    private static bool IsRetryable(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
}
