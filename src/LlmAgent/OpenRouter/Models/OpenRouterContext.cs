using System.Text.Json;
using System.Text.Json.Serialization;

namespace LlmAgent.OpenRouter.Models;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    GenerationMode = JsonSourceGenerationMode.Default,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    AllowOutOfOrderMetadataProperties = true,
    RespectNullableAnnotations = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    UseStringEnumConverter = true,
    PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate)]
[JsonSerializable(typeof(GetModelResponse))]
[JsonSerializable(typeof(GetApiKeyResponse))]
[JsonSerializable(typeof(ErrorModel))]
internal sealed partial class OpenRouterContext : JsonSerializerContext;
