using System.Collections.Immutable;
using System.Text.Json;
using LlmAgent.OpenRouter.Models;

namespace LlmAgent.OpenRouter;

public sealed class OpenRouterApi
{
    private readonly OpenRouterContext jsonContext;
    private readonly HttpClient httpClient;

    public OpenRouterApi(string apiKey)
        : this(new("https://openrouter.ai/api/v1/"), apiKey)
    {
    }

    public OpenRouterApi(Uri apiUri, string apiKey)
        : this(apiUri, apiKey, new())
    {
    }

    internal OpenRouterApi(Uri apiUri, string apiKey, OpenRouterContext jsonContext)
    {
        ArgumentNullException.ThrowIfNull(apiUri);
        ArgumentNullException.ThrowIfNull(apiKey);
        ArgumentNullException.ThrowIfNull(jsonContext);

        if (!apiUri.AbsolutePath.EndsWith('/'))
        {
            apiUri = new Uri(apiUri.ToString() + '/');
        }

        this.jsonContext = jsonContext;
        httpClient = new()
        {
            BaseAddress = apiUri,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
            DefaultRequestHeaders =
            {
                Authorization = new("Bearer", apiKey),
                Accept =
                {
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
                }
            }
        };
    }

    private async Task<Exception> CreateExceptionForErrorResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var errorMode = await JsonSerializer.DeserializeAsync(stream, jsonContext.ErrorModel, cancellationToken).ConfigureAwait(false);
        return new OpenRouterApiException((int)response.StatusCode, response.ReasonPhrase, errorMode?.Message);
    }

    private static readonly Uri modelsUriStub = new("models", UriKind.Relative);

    public async Task<ImmutableArray<ModelData>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(modelsUriStub, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var responseModel = await JsonSerializer.DeserializeAsync(responseStream, jsonContext.GetModelResponse, cancellationToken).ConfigureAwait(false);
            return responseModel?.Data ?? [];
        }
        else
        {
            throw await CreateExceptionForErrorResponseAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

    private static readonly Uri apiKeyUriStub = new("key", UriKind.Relative);

    public async Task<ApiKeyInfo> GetApiKeyInfoAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(apiKeyUriStub, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var responseModel = await JsonSerializer.DeserializeAsync(responseStream, jsonContext.GetApiKeyResponse, cancellationToken).ConfigureAwait(false);
            return responseModel!.Data;
        }
        else
        {
            throw await CreateExceptionForErrorResponseAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }
}
