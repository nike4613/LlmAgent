using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace LlmAgent.OpenRouter.Models;

internal sealed class GetModelResponse
{
    [JsonPropertyName("data")]
    public ImmutableArray<ModelData> Data { get; init; } = [];
}

public sealed class ModelData
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    [JsonPropertyName("created")]
    public required double Created { get; init; }
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("context_length")]
    public double? ContextLength { get; init; }
    [JsonPropertyName("hugging_face_id")]
    public string? HuggingFaceId { get; init; }

    [JsonPropertyName("supported_parameters")]
    public ImmutableArray<string>? SupportedPrameters { get; init; }

    [JsonPropertyName("per_request_limits")]
    public ImmutableDictionary<string, object?>? PerRequestLimits { get; init; } = ImmutableDictionary<string, object?>.Empty;

    [JsonPropertyName("architecture")]
    public required ModelArchitecture Architecture { get; init; }

    public sealed class ModelArchitecture
    {
        [JsonPropertyName("input_modalities")]
        public ImmutableArray<ModelModality> InputModalities { get; init; } = [];
        [JsonPropertyName("output_modalities")]
        public ImmutableArray<ModelModality> OutputModalities { get; init; } = [];
        [JsonPropertyName("tokenizer")]
        public required string Tokenizer { get; init; }
        [JsonPropertyName("instruct_type")]
        public string? InstructType { get; init; }
    }

    [JsonPropertyName("top_provider")]
    public required TopProviderInfo TopProvider { get; init; }

    public sealed class TopProviderInfo
    {
        [JsonPropertyName("is_moderated")]
        public bool IsModerated { get; init; }
        [JsonPropertyName("context_length")]
        public double? ContextLength { get; init; }
        [JsonPropertyName("max_completion_tokens")]
        public double? MaxCompletionTokens { get; init; }
    }

    [JsonPropertyName("pricing")]
    public PricingInfo Pricing{ get; init; } = new();

    public sealed class PricingInfo
    {
        [JsonPropertyName("prompt")]
        public string Prompt { get; init; } = "";
        [JsonPropertyName("completion")]
        public string Completion { get; init; } = "";
        [JsonPropertyName("image")]
        public string Image { get; init; } = "";
        [JsonPropertyName("request")]
        public string Request { get; init; } = "";
        [JsonPropertyName("input_cache_read")]
        public string InputCacheRead { get; init; } = "";
        [JsonPropertyName("input_cache_write")]
        public string InputCacheWrite { get; init; } = "";
        [JsonPropertyName("web_search")]
        public string WebSearch { get; init; } = "";
        [JsonPropertyName("internal_reasoning")]
        public string InternalReasoning { get; init; } = "";
    }
}

public enum ModelModality
{
    [JsonStringEnumMemberName("text")]
    Text,
    [JsonStringEnumMemberName("file")]
    File,
    [JsonStringEnumMemberName("image")]
    Image
}
