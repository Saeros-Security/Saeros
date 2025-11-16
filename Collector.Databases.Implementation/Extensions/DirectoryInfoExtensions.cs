namespace Collector.Databases.Implementation.Extensions;

public static class DirectoryInfoExtensions
{
    public static long GetDirectorySize(this DirectoryInfo? directoryInfo, bool recursive = true)
    {
        var startDirectorySize = 0L;
        if (directoryInfo is not { Exists: true })
            return startDirectorySize;

        foreach (var fileInfo in directoryInfo.GetFiles())
            Interlocked.Add(ref startDirectorySize, fileInfo.Length);

        if (recursive)
            Parallel.ForEach(directoryInfo.GetDirectories(), (subDirectory) =>
                Interlocked.Add(ref startDirectorySize, GetDirectorySize(subDirectory, recursive)));

        return startDirectorySize;
    }
}