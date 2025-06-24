using System.Text.Json.Serialization;

namespace LlmAgent.OpenRouter.Models;

internal sealed class GetApiKeyResponse
{
    [JsonPropertyName("data")]
    public required ApiKeyInfo Data { get; init; }
}

public sealed class ApiKeyInfo
{
    [JsonPropertyName("label")]
    public required string Label { get; init; }
    [JsonPropertyName("usage")]
    public required double Usage { get; init; }
    [JsonPropertyName("is_free_tier")]
    public required bool IsFreeTier { get; init; }
    [JsonPropertyName("is_provisioning_key")]
    public required bool IsProvisioningKey { get; init; }
    [JsonPropertyName("limit")]
    public required double? Limit { get; init; }
}
