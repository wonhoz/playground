namespace DeepDiff.Services;

public class FileOperationService
{
    public record OpResult(bool Success, string? Error);

    public OpResult CopyFile(string source, string dest, bool overwrite = true)
    {
        try
        {
            var dir = Path.GetDirectoryName(dest)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.Copy(source, dest, overwrite);
            return new(true, null);
        }
        catch (Exception ex) { return new(false, ex.Message); }
    }

    public OpResult MoveFile(string source, string dest)
    {
        try
        {
            var dir = Path.GetDirectoryName(dest)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.Move(source, dest, true);
            return new(true, null);
        }
        catch (Exception ex) { return new(false, ex.Message); }
    }

    public OpResult DeleteFile(string path)
    {
        try
        {
            File.Delete(path);
            return new(true, null);
        }
        catch (Exception ex) { return new(false, ex.Message); }
    }

    public OpResult DeleteFolder(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
            return new(true, null);
        }
        catch (Exception ex) { return new(false, ex.Message); }
    }

    public OpResult CopyFolder(string source, string dest)
    {
        try
        {
            CopyDirRecursive(source, dest);
            return new(true, null);
        }
        catch (Exception ex) { return new(false, ex.Message); }
    }

    public OpResult MoveFolder(string source, string dest)
    {
        try
        {
            CopyDirRecursive(source, dest);
            Directory.Delete(source, recursive: true);
            return new(true, null);
        }
        catch (Exception ex) { return new(false, ex.Message); }
    }

    private static void CopyDirRecursive(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(src))
            CopyDirRecursive(dir, Path.Combine(dst, Path.GetFileName(dir)));
    }

    public OpResult SaveText(string path, string text, System.Text.Encoding? encoding = null)
    {
        try
        {
            File.WriteAllText(path, text, encoding ?? System.Text.Encoding.UTF8);
            return new(true, null);
        }
        catch (Exception ex) { return new(false, ex.Message); }
    }

    public OpResult OpenInExplorer(string path)
    {
        try
        {
            var target = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{path}\"";
            System.Diagnostics.Process.Start("explorer.exe", target);
            return new(true, null);
        }
        catch (Exception ex) { return new(false, ex.Message); }
    }

    public OpResult OpenFile(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            return new(true, null);
        }
        catch (Exception ex) { return new(false, ex.Message); }
    }
}
