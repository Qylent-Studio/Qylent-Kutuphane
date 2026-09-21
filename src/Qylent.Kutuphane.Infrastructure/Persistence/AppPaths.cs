namespace Qylent.Kutuphane.Infrastructure.Persistence;

public sealed class AppPaths
{
    public AppPaths(string? dataDirectory = null)
    {
        DataDirectory = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QylentStudio",
            "Kutuphane");
        Directory.CreateDirectory(DataDirectory);
    }

    public string DataDirectory { get; }
    public string DatabasePath => Path.Combine(DataDirectory, "kutuphane.db");
    public string KeyPath => Path.Combine(DataDirectory, "sensitive-fields.key");
    public string DefaultBackupDirectory => Path.Combine(DataDirectory, "Backups");
    public string AutomaticBackupDirectory => Path.Combine(DefaultBackupDirectory, "Otomatik");
    public string AutomaticBackupKeyPath => Path.Combine(DataDirectory, "automatic-backup.key");
}
