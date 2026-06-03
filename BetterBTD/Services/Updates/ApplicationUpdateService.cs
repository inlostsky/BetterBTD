using System.Diagnostics;
using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace BetterBTD.Services.Updates;

public enum ApplicationUpdateState
{
    NotSupported,
    NoUpdateAvailable,
    UpdateReadyToApply,
    UpdateDownloaded
}

public sealed record ApplicationUpdateResult(
    ApplicationUpdateState State,
    string Message,
    VelopackAsset? Release = null);

public sealed class ApplicationUpdateService
{
    private const string RepositoryUrl = "https://github.com/ZiyaoZh/BetterBTD";
    private static readonly Lazy<ApplicationUpdateService> InstanceHolder = new(() => new ApplicationUpdateService());

    private ApplicationUpdateService()
    {
    }

    public static ApplicationUpdateService Instance => InstanceHolder.Value;

    public string CurrentVersion
    {
        get
        {
            var manager = CreateUpdateManager(includePrerelease: false);
            var version = manager.CurrentVersion?.ToString();
            return string.IsNullOrWhiteSpace(version) ? GetFallbackVersion() : version;
        }
    }

    public async Task<ApplicationUpdateResult> CheckForUpdatesAsync(bool includePrerelease, CancellationToken cancellationToken = default)
    {
        var manager = CreateUpdateManager(includePrerelease);

        if (!manager.IsInstalled && !manager.IsPortable)
        {
            return new ApplicationUpdateResult(
                ApplicationUpdateState.NotSupported,
                "Update checks are available only from an installed BetterBTD release.");
        }

        if (manager.UpdatePendingRestart is { } pendingRelease)
        {
            return new ApplicationUpdateResult(
                ApplicationUpdateState.UpdateReadyToApply,
                $"Update {pendingRelease.Version} is ready to apply. Restart BetterBTD to finish the update.",
                pendingRelease);
        }

        var update = await manager.CheckForUpdatesAsync();
        if (update is null)
        {
            return new ApplicationUpdateResult(
                ApplicationUpdateState.NoUpdateAvailable,
                $"BetterBTD {CurrentVersion} is already up to date.");
        }

        await manager.DownloadUpdatesAsync(update, null, cancellationToken);

        return new ApplicationUpdateResult(
            ApplicationUpdateState.UpdateDownloaded,
            $"Update {update.TargetFullRelease.Version} has been downloaded. Restart BetterBTD to apply it.",
            update.TargetFullRelease);
    }

    public void ApplyUpdatesAndRestart(VelopackAsset? release = null)
    {
        var manager = CreateUpdateManager(includePrerelease: false);
        manager.ApplyUpdatesAndRestart(release);
    }

    public void OpenProjectHomePage()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = RepositoryUrl,
            UseShellExecute = true
        });
    }

    private static UpdateManager CreateUpdateManager(bool includePrerelease)
    {
        return new UpdateManager(new GithubSource(RepositoryUrl, accessToken: null, prerelease: includePrerelease));
    }

    private static string GetFallbackVersion()
    {
        var informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "dev";
    }
}
