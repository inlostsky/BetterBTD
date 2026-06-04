using BetterBTD.Helpers;
using BetterBTD.Models;

namespace BetterBTD.Services.Start.Capture;

public sealed class GameWindowInfoService
{
    private static readonly string[] DefaultTargetWindowTitles =
    [
        "BloonsTD6",
        "BloonsTD6-Epic"
    ];

    private static readonly Lazy<GameWindowInfoService> InstanceHolder = new(() => new GameWindowInfoService());

    private GameWindowInfoService()
    {
    }

    public static GameWindowInfoService Instance => InstanceHolder.Value;

    public string TargetWindowTitle => ConfigurationService.Instance.Current.MaskWindowTargetTitle;

    public IReadOnlyList<string> PreferredTargetWindowTitles => ResolvePreferredTargetWindowTitles();

    public bool TryGetTargetWindowInfo(out GameWindowInfo windowInfo)
    {
        foreach (var title in ResolvePreferredTargetWindowTitles())
        {
            var targetHandle = NativeWindowHelper.FindTopLevelWindow(title);
            if (TryGetWindowInfo(targetHandle, out windowInfo))
            {
                return true;
            }
        }

        windowInfo = default;
        return false;
    }

    public bool TryGetWindowInfo(nint handle, out GameWindowInfo windowInfo)
    {
        windowInfo = default;

        if (handle == nint.Zero ||
            !NativeWindowHelper.TryGetWindowBounds(handle, out var windowBounds) ||
            !NativeWindowHelper.TryGetClientBounds(handle, out var clientBounds))
        {
            return false;
        }

        windowInfo = new GameWindowInfo(
            handle,
            NativeWindowHelper.GetWindowTitle(handle),
            windowBounds,
            clientBounds,
            NativeWindowHelper.GetWindowScaleFactor(handle));
        return true;
    }

    private IReadOnlyList<string> ResolvePreferredTargetWindowTitles()
    {
        var configuredTitle = TargetWindowTitle?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(configuredTitle))
        {
            return DefaultTargetWindowTitles;
        }

        var titles = new List<string> { configuredTitle };
        if (DefaultTargetWindowTitles.Contains(configuredTitle, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var defaultTitle in DefaultTargetWindowTitles)
            {
                if (string.Equals(defaultTitle, configuredTitle, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                titles.Add(defaultTitle);
            }
        }

        return titles;
    }
}

