namespace MiniClaudeCode.Avalonia.Services;

/// <summary>
/// Creates backup copies of files before saving, stored in %LocalAppData%/MiniClaudeCode/backups/.
/// Keeps the last 5 backups per file.
/// </summary>
public class FileBackupService
{
    private readonly string _backupDir;
    private const int MaxBackupsPerFile = 5;

    public FileBackupService()
    {
        _backupDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MiniClaudeCode", "backups");
        Directory.CreateDirectory(_backupDir);
    }

    /// <summary>
    /// Create a backup of the given file before it is overwritten.
    /// </summary>
    public void CreateBackup(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return;

            var safeFileName = SanitizeFileName(filePath);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var backupName = $"{safeFileName}_{timestamp}{Path.GetExtension(filePath)}";
            var backupPath = Path.Combine(_backupDir, backupName);

            File.Copy(filePath, backupPath, overwrite: true);
            PruneOldBackups(safeFileName, Path.GetExtension(filePath));
        }
        catch
        {
            // Backup failure should never prevent saving
        }
    }

    /// <summary>
    /// Restore the most recent backup for a file.
    /// </summary>
    public bool Restore(string filePath)
    {
        try
        {
            var safeFileName = SanitizeFileName(filePath);
            var ext = Path.GetExtension(filePath);
            var pattern = $"{safeFileName}_*{ext}";

            var latest = Directory.GetFiles(_backupDir, pattern)
                .OrderByDescending(f => f)
                .FirstOrDefault();

            if (latest == null) return false;

            File.Copy(latest, filePath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void PruneOldBackups(string safeFileName, string ext)
    {
        try
        {
            var pattern = $"{safeFileName}_*{ext}";
            var files = Directory.GetFiles(_backupDir, pattern)
                .OrderByDescending(f => f)
                .Skip(MaxBackupsPerFile)
                .ToList();

            foreach (var file in files)
            {
                try { File.Delete(file); } catch { /* ignore */ }
            }
        }
        catch { /* ignore */ }
    }

    private static string SanitizeFileName(string filePath)
    {
        // Replace path separators and other invalid chars with underscores
        var name = filePath.Replace('\\', '_').Replace('/', '_').Replace(':', '_');
        // Truncate if too long
        if (name.Length > 200) name = name[^200..];
        return name;
    }
}
