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
