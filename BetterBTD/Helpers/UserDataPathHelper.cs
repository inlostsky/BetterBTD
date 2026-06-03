using System.IO;

namespace BetterBTD.Helpers;

internal static class UserDataPathHelper
{
    private static readonly string DefaultUserRootDirectory = Path.Combine(AppContext.BaseDirectory, "User");

    public static string ResolveUserDataDirectory(params string[] relativeSegments)
    {
        return ResolveDirectory(DefaultUserRootDirectory, relativeSegments);
    }

    public static string ResolveUserDataFilePath(params string[] relativeSegments)
    {
        return ResolveFilePath(DefaultUserRootDirectory, relativeSegments);
    }

    internal static string ResolveDirectory(string userRootDirectory, params string[] relativeSegments)
    {
        var targetDirectoryPath = CombinePath(userRootDirectory, relativeSegments);
        Directory.CreateDirectory(targetDirectoryPath);
        return targetDirectoryPath;
    }

    internal static string ResolveFilePath(string userRootDirectory, params string[] relativeSegments)
    {
        var targetFilePath = CombinePath(userRootDirectory, relativeSegments);
        var targetDirectoryPath = Path.GetDirectoryName(targetFilePath);

        if (!string.IsNullOrWhiteSpace(targetDirectoryPath))
        {
            Directory.CreateDirectory(targetDirectoryPath);
        }

        return targetFilePath;
    }

    private static string CombinePath(string rootDirectory, IReadOnlyList<string> relativeSegments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(relativeSegments);

        var path = rootDirectory;
        foreach (var segment in relativeSegments)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(segment);
            path = Path.Combine(path, segment.Trim());
        }

        return path;
    }
}
