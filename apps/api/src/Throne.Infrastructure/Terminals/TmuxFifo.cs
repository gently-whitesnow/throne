using Microsoft.Extensions.Options;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Owns the per-bridge FIFO file that <c>tmux pipe-pane</c> writes into. Creation
/// goes through <c>mkfifo(1)</c> (the simplest POSIX path that works on macOS and
/// Linux without P/Invoke); the bridge is responsible for opening + tailing the
/// file and for calling <see cref="Cleanup"/> on shutdown.
///
/// The FIFO root directory lives in <see cref="TmuxOptions.PipeRootDirectory"/> and
/// is created lazily. Each fifo file name is unique (<c>{intent_id}-{rand}.fifo</c>),
/// so multiple concurrent attaches to the same session never collide.
/// </summary>
internal sealed class TmuxFifo : IAsyncDisposable
{
    private readonly string _path;
    private bool _disposed;

    private TmuxFifo(string path) => _path = path;

    public string Path => _path;

    public static async Task<TmuxFifo> CreateAsync(
        IOptions<TmuxOptions> options,
        string intentId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);

        var root = options.Value.PipeRootDirectory;
        Directory.CreateDirectory(root);

        var name = $"{intentId}-{Guid.NewGuid():N}.fifo";
        var path = System.IO.Path.Combine(root, name);

        // Use mkfifo(1) — present on every POSIX host Throne targets in Slice 2.
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "mkfifo",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(path);

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new IOException("mkfifo: could not start process");

        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            throw new IOException($"mkfifo {path}: exit {process.ExitCode} ({stderr.Trim()})");
        }

        return new TmuxFifo(path);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }
        _disposed = true;

        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch (IOException)
        {
            // FIFO may have been removed by the tmux pipe-pane consumer; ignore.
        }
        catch (UnauthorizedAccessException)
        {
            // Permissions changed between create and delete — ignore.
        }

        return ValueTask.CompletedTask;
    }
}
