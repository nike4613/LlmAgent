using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LlmAgent.OpenRouter.Models;

internal sealed class ErrorResponse
{
    [JsonPropertyName("error")]
    public required ErrorModel Error { get; init; }
}

internal sealed class ErrorModel
{
    [JsonPropertyName("code")]
    public int Code { get; init; }
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}
