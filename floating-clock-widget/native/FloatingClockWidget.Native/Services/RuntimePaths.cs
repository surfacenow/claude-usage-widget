using System;
using System.IO;

namespace FloatingClockWidget.Native.Services;

public sealed record RuntimePaths(
    string RootDir,
    string ThemesDir,
    string ImportsTmpDir,
    string ImportsFailedDir,
    string LogsDir)
{
    public static RuntimePaths Resolve()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = Path.Combine(localAppData, "FloatingClockWidget");

        return new RuntimePaths(
            RootDir: root,
            ThemesDir: Path.Combine(root, "themes"),
            ImportsTmpDir: Path.Combine(root, "imports", "tmp"),
            ImportsFailedDir: Path.Combine(root, "imports", "failed"),
            LogsDir: Path.Combine(root, "logs"));
    }
}
