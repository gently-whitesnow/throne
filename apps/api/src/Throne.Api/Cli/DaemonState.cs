using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Throne.Api.Cli;

/// <summary>
/// The on-disk record of the instance that owns a <see cref="ThroneHome"/>. The
/// plain <c>throne.pid</c> file is the greppable source of truth for liveness/stop;
/// <c>throne.daemon.json</c> carries the extras <c>status</c> needs (url, version,
/// start time) without a live HTTP probe. Both are written together and cleared by
/// <c>stop</c>; a stale file (pid no longer alive) is detected lazily and cleaned.
/// </summary>
internal sealed record DaemonState(int Pid, string Url, string Version, DateTimeOffset StartedAt)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [JsonIgnore]
    public bool IsAlive => IsProcessAlive(Pid);

    public static void Write(ThroneHome home, DaemonState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        home.EnsureDirectory();
        File.WriteAllText(home.PidFile, state.Pid.ToString(CultureInfo.InvariantCulture));
        File.WriteAllText(home.StateFile, JsonSerializer.Serialize(state, JsonOptions));
    }

    public static DaemonState? TryRead(ThroneHome home)
    {
        try
        {
            if (File.Exists(home.StateFile))
            {
                var parsed = JsonSerializer.Deserialize<DaemonState>(File.ReadAllText(home.StateFile));
                if (parsed is { Pid: > 0 })
                {
                    return parsed;
                }
            }

            if (File.Exists(home.PidFile)
                && int.TryParse(File.ReadAllText(home.PidFile).Trim(), out var pid) && pid > 0)
            {
                return new DaemonState(pid, "", "", default);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
        }

        return null;
    }

    public static void Clear(ThroneHome home)
    {
        TryDelete(home.PidFile);
        TryDelete(home.StateFile);
    }

    public static bool IsProcessAlive(int pid)
    {
        if (pid <= 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
