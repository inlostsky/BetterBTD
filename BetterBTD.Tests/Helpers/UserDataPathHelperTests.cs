using BetterBTD.Helpers;

namespace BetterBTD.Tests.Helpers;

public sealed class UserDataPathHelperTests
{
    [Fact]
    public void ResolveDirectory_CreatesDirectoryUnderUserRoot()
    {
        var tempDirectory = CreateTempDirectory();
        var userRootDirectory = Path.Combine(tempDirectory, "User");

        try
        {
            var resolvedDirectoryPath = UserDataPathHelper.ResolveDirectory(userRootDirectory, "MyScripts");

            Assert.Equal(Path.Combine(userRootDirectory, "MyScripts"), resolvedDirectoryPath);
            Assert.True(Directory.Exists(resolvedDirectoryPath));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ResolveFilePath_CreatesParentDirectoryUnderUserRoot()
    {
        var tempDirectory = CreateTempDirectory();
        var userRootDirectory = Path.Combine(tempDirectory, "User");

        try
        {
            var resolvedFilePath = UserDataPathHelper.ResolveFilePath(
                userRootDirectory,
                "AutoTasks",
                "game_ui_detection_rules.json");

            Assert.Equal(Path.Combine(userRootDirectory, "AutoTasks", "game_ui_detection_rules.json"), resolvedFilePath);
            Assert.True(Directory.Exists(Path.GetDirectoryName(resolvedFilePath)!));
            Assert.False(File.Exists(resolvedFilePath));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "BetterBTD.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }
}
