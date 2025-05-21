using System.Text.Json.Serialization;
using Json.Schema;

namespace LlmAgent;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Default,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(JsonSchema))]
[JsonSerializable(typeof(EvaluationResults))]
[JsonSerializable(typeof(ArithmeticArgs))]
[JsonSerializable(typeof(CommandArgs))]
internal sealed partial class JsonContext : JsonSerializerContext
{
}

internal readonly record struct ArithmeticArgs(int Lhs, int Rhs);
internal readonly record struct CommandArgs(string Command);
