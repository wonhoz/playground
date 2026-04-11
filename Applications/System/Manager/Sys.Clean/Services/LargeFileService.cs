using SysClean.Models;

namespace SysClean.Services;

public class LargeFileService
{
    public async Task<List<LargeFileEntry>> ScanAsync(
        string rootPath, long minSizeBytes,
        IProgress<string>? progress, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var result = new List<LargeFileEntry>();

            try
            {
                ScanDirectory(rootPath, minSizeBytes, result, progress, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch { /* root access denied */ }

            result.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
            return result;

        }, ct);
    }

    private static void ScanDirectory(
        string path, long minSize, List<LargeFileEntry> result,
        IProgress<string>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            foreach (var file in Directory.EnumerateFiles(path))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(file);
                    if (info.Length >= minSize)
                    {
                        result.Add(new LargeFileEntry
                        {
                            FullPath = file,
                            SizeBytes = info.Length,
                            LastModified = info.LastWriteTime
                        });
                    }
                }
                catch { /* access denied */ }
            }

            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(dir);

                try
                {
                    ScanDirectory(dir, minSize, result, progress, ct);
                }
                catch (OperationCanceledException) { throw; }
                catch { /* access denied */ }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* access denied */ }
    }
}
