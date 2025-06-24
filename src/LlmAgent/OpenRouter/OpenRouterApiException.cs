namespace LlmAgent.OpenRouter;

public class OpenRouterApiException : IOException
{
    public int HttpResponseCode { get; }
    public string? HttpResponseMessage { get; }
    public string? OpenRouterMessage { get; }

    public OpenRouterApiException(int responseCode, string? responseMessage, string? openrouterMessage)
        : this(responseCode, responseMessage, openrouterMessage, null)
    {
    }

    public OpenRouterApiException(int responseCode, string? responseMessage, string? openrouterMessage, Exception? innerException)
        : base(GetMessage(responseCode, responseMessage, openrouterMessage), innerException)
    {
        HttpResponseCode = responseCode;
        HttpResponseMessage = responseMessage;
        OpenRouterMessage = openrouterMessage;
    }

    private static string GetMessage(int responseCode, string? responseMessage, string? openrouter)
    {
        return $"API responsed with {responseCode}{(responseMessage is { } str ? $" {str}" : "")}{(openrouter is { } str2 ? $": {str2}" : "")}";
    }
}
