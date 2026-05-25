using System.Diagnostics;
using System.Text;

namespace Throne.Infrastructure.Git;

/// <summary>
/// Captures stdout / stderr from a <see cref="Process"/> into two
/// <see cref="StringBuilder"/>s.
/// </summary>
internal sealed class ProcessOutputCollector
{
    private readonly StringBuilder _stdout = new();
    private readonly StringBuilder _stderr = new();

    public string StandardOutput => _stdout.ToString();

    public string StandardError => _stderr.ToString();

    public void Attach(Process process)
    {
        process.OutputDataReceived += OnStdout;
        process.ErrorDataReceived += OnStderr;
    }

    private void OnStdout(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null)
        {
            _stdout.AppendLine(e.Data);
        }
    }

    private void OnStderr(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null)
        {
            _stderr.AppendLine(e.Data);
        }
    }
}
