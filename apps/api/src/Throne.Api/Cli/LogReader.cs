using System.Text;

namespace Throne.Api.Cli;

/// <summary>
/// Plain-file tail for the daemon log. <see cref="LastLines"/> backs <c>logs</c> and
/// the startup-failure dump; <see cref="FollowAsync"/> backs <c>logs -f</c>, polling
/// for appended bytes (the writer holds <c>FileShare.ReadWrite</c>, so a concurrent
/// tail is safe).
/// </summary>
internal static class LogReader
{
    public static IReadOnlyList<string> LastLines(string path, int count)
    {
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var buffer = new LinkedList<string>();
            while (reader.ReadLine() is { } line)
            {
                buffer.AddLast(line);
                if (buffer.Count > count)
                {
                    buffer.RemoveFirst();
                }
            }

            return [.. buffer];
        }
        catch (IOException)
        {
            return [];
        }
    }

    public static async Task FollowAsync(string path, CancellationToken ct)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(0, SeekOrigin.End);
        var reader = new StreamReader(stream, Encoding.UTF8);

        while (!ct.IsCancellationRequested)
        {
            var chunk = await reader.ReadToEndAsync(ct);
            if (chunk.Length > 0)
            {
                await Console.Out.WriteAsync(chunk);
            }
            else
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300), ct);
            }
        }
    }
}
