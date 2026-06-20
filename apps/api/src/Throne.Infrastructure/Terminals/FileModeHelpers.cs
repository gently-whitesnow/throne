using System.Text;

namespace Throne.Infrastructure.Terminals;

internal static class FileModeHelpers
{
    // Executable scripts must start with a bare `#!` shebang. `Encoding.UTF8` would prepend a BOM,
    // which makes the kernel reject the shebang (ENOEXEC) and the shell fall back to /bin/sh —
    // breaking bash-only scripts on dash-based systems. Always write scripts without a BOM.
    public static readonly Encoding ScriptEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static void MakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }
}
