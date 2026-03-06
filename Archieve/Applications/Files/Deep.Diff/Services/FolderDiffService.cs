using DeepDiff.Models;

namespace DeepDiff.Services;

public class FolderDiffService
{
    public record FolderDiffResult(
        List<FolderDiffItem> Items,
        int SameCount, int DiffCount, int LeftOnlyCount, int RightOnlyCount);

    /// <summary>현재 폴더 수준(비재귀)으로 비교</summary>
    public FolderDiffResult Compare(string leftPath, string rightPath,
        string filter = "*", bool recurse = false)
    {
        var items = new List<FolderDiffItem>();
        CompareDirectory(leftPath, rightPath, "", filter, recurse, items, 0);

        int same = 0, diff = 0, lo = 0, ro = 0;
        foreach (var it in items)
            switch (it.Status)
            {
                case DiffStatus.Same:       same++; break;
                case DiffStatus.Different:  diff++; break;
                case DiffStatus.LeftOnly:   lo++;   break;
                case DiffStatus.RightOnly:  ro++;   break;
            }

        return new(items, same, diff, lo, ro);
    }

    private void CompareDirectory(string leftDir, string rightDir, string relBase,
        string filter, bool recurse, List<FolderDiffItem> items, int depth)
    {
        var leftEntries  = GetEntries(leftDir,  filter);
        var rightEntries = GetEntries(rightDir, filter);

        var allNames = leftEntries.Keys.Union(rightEntries.Keys,
            StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

        foreach (var name in allNames)
        {
            bool inLeft  = leftEntries.TryGetValue(name,  out var leftInfo);
            bool inRight = rightEntries.TryGetValue(name, out var rightInfo);
            bool isDir   = (inLeft && leftInfo!.IsDirectory) || (inRight && rightInfo!.IsDirectory);

            string relPath = relBase.Length > 0 ? $"{relBase}\\{name}" : name;

            var item = new FolderDiffItem
            {
                RelPath    = relPath,
                IsDirectory= isDir,
                Depth      = depth,
                LeftName   = inLeft  ? name : null,
                RightName  = inRight ? name : null,
                LeftSize   = inLeft  && !leftInfo!.IsDirectory  ? leftInfo!.Size     : null,
                RightSize  = inRight && !rightInfo!.IsDirectory ? rightInfo!.Size    : null,
                LeftModified  = inLeft  ? leftInfo!.Modified  : null,
                RightModified = inRight ? rightInfo!.Modified : null,
                LeftFullPath  = inLeft  ? leftInfo!.FullPath  : null,
                RightFullPath = inRight ? rightInfo!.FullPath : null,
            };

            if (!inLeft)       item.Status = DiffStatus.RightOnly;
            else if (!inRight) item.Status = DiffStatus.LeftOnly;
            else if (isDir)    item.Status = DiffStatus.Same; // 폴더는 내용 기반 판단
            else               item.Status = CompareFiles(leftInfo!.FullPath, rightInfo!.FullPath);

            items.Add(item);

            if (isDir && recurse && inLeft && inRight)
                CompareDirectory(leftInfo!.FullPath, rightInfo!.FullPath,
                    relPath, filter, recurse, items, depth + 1);
        }
    }

    private static DiffStatus CompareFiles(string left, string right)
    {
        var li = new FileInfo(left);
        var ri = new FileInfo(right);

        if (li.Length != ri.Length) return DiffStatus.Different;
        if (Math.Abs((li.LastWriteTimeUtc - ri.LastWriteTimeUtc).TotalSeconds) > 2)
            return DiffStatus.Different;
        return DiffStatus.Same;
    }

    /// <summary>두 파일의 내용을 실제로 바이트 비교</summary>
    public static bool ContentEqual(string left, string right)
    {
        if (!File.Exists(left) || !File.Exists(right)) return false;
        var li = new FileInfo(left);
        var ri = new FileInfo(right);
        if (li.Length != ri.Length) return false;

        using var fs1 = File.OpenRead(left);
        using var fs2 = File.OpenRead(right);
        var buf1 = new byte[8192];
        var buf2 = new byte[8192];
        int n;
        while ((n = fs1.Read(buf1, 0, buf1.Length)) > 0)
        {
            fs2.Read(buf2, 0, n);
            for (int i = 0; i < n; i++)
                if (buf1[i] != buf2[i]) return false;
        }
        return true;
    }

    private static Dictionary<string, EntryInfo> GetEntries(string dir, string filter)
    {
        var dict = new Dictionary<string, EntryInfo>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(dir)) return dict;

        foreach (var d in Directory.GetDirectories(dir))
        {
            var di = new DirectoryInfo(d);
            dict[di.Name] = new(di.Name, di.FullName, 0, di.LastWriteTime, true);
        }

        foreach (var pattern in filter.Split(';', ',').Select(p => p.Trim()))
        {
            foreach (var f in Directory.GetFiles(dir, pattern))
            {
                var fi = new FileInfo(f);
                if (!dict.ContainsKey(fi.Name))
                    dict[fi.Name] = new(fi.Name, fi.FullName, fi.Length, fi.LastWriteTime, false);
            }
        }
        return dict;
    }

    private record EntryInfo(string Name, string FullPath, long Size, DateTime Modified, bool IsDirectory);
}
