using System.ComponentModel;
using Ookii.CommandLine;

namespace LlmAgent;

[GeneratedParser]
[ParseOptions(IsPosix = true, AutoVersionArgument = true)]
internal sealed partial class Arguments
{
    [CommandLineArgument(DefaultValue = "https://openrouter.ai/api/v1")]
    [Description("The API URL to use. Defaults to OpenRouter.")]
    public Uri ApiUrl { get; set; } = new("https://openrouter.ai/api/v1");

    [CommandLineArgument(ShortName = 'k')]
    [Description("The API key to use for requests")]
    public required string ApiKey { get; set; }

    [CommandLineArgument(ShortName = 'm')]
    [Description("The model to use")]
    public required string Model { get; set; }

    [CommandLineArgument(IsPositional = true)]
    [Description("The prompt to pass to the LLM.")]
    public string? Prompt { get; set; }
}
