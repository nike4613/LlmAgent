using System.Buffers;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

namespace LlmAgent;

internal sealed class PowershellEvaluator : IDisposable
{
    private readonly Process powershellProcess;
    private readonly NamedPipeServerStream commStream;
    private readonly CancellationTokenSource processDiedCts;

    private PowershellEvaluator(Process process, NamedPipeServerStream stream)
    {
        powershellProcess = process;
        commStream = stream;

        process.EnableRaisingEvents = true;
        processDiedCts = new();
        process.Exited += (s, e) => processDiedCts.Cancel();
    }

    public void Dispose()
    {
        try
        {
            commStream.Close();
            powershellProcess.EnableRaisingEvents = false;
            powershellProcess.Kill(true);
            processDiedCts.Dispose();
        }
        catch
        {
        }
    }

    public static async Task<PowershellEvaluator> CreateAsync(CancellationToken cancellationToken)
    {
        var pipeName = $"llmagent-pwsh-control-{Guid.NewGuid()}";

        var pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1);

        var controlScript = "& { " + ControlScript.Replace("{{PIPE_NAME}}", pipeName, StringComparison.Ordinal) + " }";
        var psi = new ProcessStartInfo()
        {
            FileName = "pwsh",
            Arguments = $"-NoProfile -NoLogo -ExecutionPolicy Bypass -EncodedCommand \"{Convert.ToBase64String(Encoding.Unicode.GetBytes(controlScript))}\"",
            UseShellExecute = true,
            //CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory,
        };

        var proc = Process.Start(psi);
        if (proc is null || proc.HasExited)
        {
            throw new InvalidOperationException("Could not create PowerShell child process. Is there a `pwsh` binary available on PATH?");
        }

        try
        {
            // attempt to connect to child process, with timeout ofc
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                cts.CancelAfter(10000);
                await pipeServer.WaitForConnectionAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException oce) when (oce.CancellationToken == cts.Token)
            {
                // timeout; throw a different exception
                throw new InvalidOperationException("Connection to child PowerShell process timed out", oce);
            }
            cts = null;

            // we're successfully connected, we can return
            return new(proc, pipeServer);
        }
        catch
        {
            try
            {
                proc.Kill(true);
            }
            catch
            {
            }
            throw;
        }
    }

    private const string ControlScript = """
        $ErrorActionPreference = "Stop";

        $Private:PipeName = '{{PIPE_NAME}}';

        $Private:PipeClient = [System.IO.Pipes.NamedPipeClientStream]::new(".", $PipeName, [System.IO.Pipes.PipeDirection]::InOut, [System.IO.Pipes.PipeOptions]::None);
        $PipeClient.Connect();

        function Read-RemoteString($PipeClient)
        {
            $Private:len = $PipeClient.ReadByte();
            $len = $len -bor ($PipeClient.ReadByte() -shl 8);
            $len = $len -bor ($PipeClient.ReadByte() -shl 16);
            $len = $len -bor ($PipeClient.ReadByte() -shl 24);
            $Private:buffer = [byte[]]::new($len);
            $PipeClient.ReadExactly($buffer, 0, $len);
            return [System.Text.Encoding]::Unicode.GetString($buffer);
        }

        function Write-RemoteString($PipeClient, $text)
        {
            $Private:buffer = [System.Text.Encoding]::Unicode.GetBytes($text);
            $Private:len = $buffer.Length;

            $PipeClient.WriteByte($len -band 255);
            $PipeClient.WriteByte(($len -shr 8) -band 255);
            $PipeClient.WriteByte(($len -shr 16) -band 255);
            $PipeClient.WriteByte(($len -shr 24) -band 255);
            $PipeClient.Write($buffer, 0, $buffer.Length);
            $PipeClient.Flush();
        }

        try
        {
            while ($true)
            {
                $ErrorActionPreference = "Stop";
                try
                {
                    $cmd = Read-RemoteString $PipeClient;

                    $LastExitCode = 0;
                    $Private:resultObj = @{};
                    try
                    {
                        $Private:str = & { Invoke-Expression -Command $cmd; } | Out-String;
                        $resultObj.exitcode = $LastExitCode;
                        $resultObj.output = $str;
                    }
                    catch
                    {
                        $resultObj.error = "$_";
                    }

                    Write-RemoteString $PipeClient (ConvertTo-Json -Compress -EnumsAsStrings -Depth 5 -InputObject $resultObj);
                }
                catch
                {
                }
            }
        }
        finally
        {
            $PipeClient.Close();
        }
        """;

    private async ValueTask WriteString(string text, CancellationToken cancellationToken)
    {
        var arr = ArrayPool<byte>.Shared.Rent(text.Length * 2);
        var wrote = Encoding.Unicode.GetBytes(text, arr);

        commStream.WriteByte((byte)(wrote & 0xff));
        commStream.WriteByte((byte)((wrote >> 8) & 0xff));
        commStream.WriteByte((byte)((wrote >> 16) & 0xff));
        commStream.WriteByte((byte)((wrote >> 24) & 0xff));
        await commStream.WriteAsync(arr.AsMemory(0, wrote), cancellationToken).ConfigureAwait(false);
        await commStream.FlushAsync(cancellationToken).ConfigureAwait(false);

        ArrayPool<byte>.Shared.Return(arr);
    }

    private async ValueTask<string> ReadString(CancellationToken cancellationToken)
    {
        var arr = ArrayPool<byte>.Shared.Rent(4);
        await commStream.ReadExactlyAsync(arr.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);
        var len = arr[0] | (arr[1] << 8) | (arr[2] << 16) | (arr[3] << 24);
        ArrayPool<byte>.Shared.Return(arr);

        arr = ArrayPool<byte>.Shared.Rent(len);
        await commStream.ReadExactlyAsync(arr.AsMemory(0, len), cancellationToken).ConfigureAwait(false);
        var result = Encoding.Unicode.GetString(arr.AsSpan(0, len));
        ArrayPool<byte>.Shared.Return(arr);
        return result;
    }

    public void RegisterTool(AgentLoop agent, JsonContext ctx)
    {
        agent.AddTool(
            "powershell_eval", """
            Evaluates a PowerShell command.

            Some useful commands:
            - "Get-ChildItem dir/" - Gets the content of `dir/` (supports wildcards)
            - "Get-ChildItem -LiteralPath 'funky[name]/'" - Same as above, but suppresses wildcards
            - "Get-Content path/to/file.txt" - Gets the content of the provided file (with wildcards, -LiteralPath supported)
            - "Where-Object { <# some predicate #> }" - Filters the input pipeline to only items matching the predicate
                - "Get-ChildItem -Recurse | Where-Object { $_.Name.Contains('Test') }" - Gets all files and directories recursively from the current directory whose name (not path!) contains 'Test'
            - "Get-Location" - Gets the current working directory
            - "Set-Location" - Sets the current working directory. If none are specified, changes to user profile. DO NOT MOVE TO THE USER PROFILE EVER!
            - "$variable = # value..." - Sets a variable
                - Variables are not shared between invocations unless declared and used as `$Global:variable`
                - Variables can be used anywhere, including in strings like so: `"this is text before the var, [$variable] <- and that is the VALUE of the variable"`
                - Strings can also embed more complex expressions: `"some text $($variable.Property)"`
            """,
            ctx.CommandArgs,
            async (cmd, cancellationToken) =>
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, processDiedCts.Token);
                await WriteString(cmd.Command, cts.Token).ConfigureAwait(false);
                return await ReadString(cts.Token).ConfigureAwait(false);
            });
    }
}
