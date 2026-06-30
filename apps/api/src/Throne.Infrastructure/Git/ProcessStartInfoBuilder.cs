using System.Diagnostics;
using System.Text;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Git;

/// <summary>
/// Builds <see cref="ProcessStartInfo"/> from a <see cref="ProcessRunRequest"/>.
/// </summary>
internal static class ProcessStartInfoBuilder
{
    public static ProcessStartInfo Build(ProcessRunRequest request)
    {
        var psi = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory ?? string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = request.StandardInput is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (request.StandardInput is not null)
        {
            // Without an explicit encoding the redirected writer falls back to the console
            // input codepage; on a non-UTF-8 locale that mangles multibyte payloads (e.g.
            // Cyrillic piped to `tmux load-buffer -`). Pin UTF-8 so bytes match the source.
            // Must NOT emit a BOM: the shared Encoding.UTF8 singleton prepends EF BB BF,
            // which corrupts JSON bodies piped to `glab api --input -` (GitLab rejects with
            // "Invalid JSON format" / HTTP 400).
            psi.StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        ApplyArguments(psi, request.Arguments);
        ApplyEnvironment(psi, request.Environment);
        return psi;
    }

    private static void ApplyArguments(ProcessStartInfo psi, IReadOnlyList<string> arguments)
    {
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }
    }

    private static void ApplyEnvironment(ProcessStartInfo psi, IReadOnlyDictionary<string, string?>? environment)
    {
        if (environment is null)
        {
            return;
        }

        foreach (var (key, value) in environment)
        {
            psi.Environment[key] = value;
        }
    }
}
