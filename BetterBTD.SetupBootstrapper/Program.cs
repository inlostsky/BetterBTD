using System.Diagnostics;
using System.Text;
using Microsoft.Win32;
using System.Windows.Forms;

namespace BetterBTD.SetupBootstrapper;

internal static class Program
{
    private const string InstallerExecutableName = "BetterBTD.Install.exe";
    private const string RuntimeDownloadUrl = "https://dotnet.microsoft.com/en-us/download/dotnet/8.0";
    private const string RuntimeDisplayName = ".NET 8 Desktop Runtime";

    [STAThread]
    private static int Main(string[] args)
    {
        var installerPath = Path.Combine(AppContext.BaseDirectory, InstallerExecutableName);
        if (!File.Exists(installerPath))
        {
            MessageBox.Show(
                $"{InstallerExecutableName} was not found next to this bootstrapper.",
                "BetterBTD Setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        if (!HasDesktopRuntime8())
        {
            var result = MessageBox.Show(
                $"BetterBTD requires {RuntimeDisplayName}.{Environment.NewLine}{Environment.NewLine}" +
                "Click Yes to open the official Microsoft download page. After installing the runtime, run this setup again.",
                "BetterBTD Setup",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = RuntimeDownloadUrl,
                    UseShellExecute = true
                });
            }

            return 2;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = BuildArguments(args),
            UseShellExecute = true,
            WorkingDirectory = AppContext.BaseDirectory
        });
        return 0;
    }

    private static bool HasDesktopRuntime8()
    {
        return HasDesktopRuntime8FromRegistry() || HasDesktopRuntime8FromDotnet();
    }

    private static bool HasDesktopRuntime8FromRegistry()
    {
        return HasDesktopRuntime8FromRegistryView(RegistryView.Registry64) ||
               HasDesktopRuntime8FromRegistryView(RegistryView.Registry32);
    }

    private static bool HasDesktopRuntime8FromRegistryView(RegistryView view)
    {
        string[] subKeyPaths =
        [
            @"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App",
            @"SOFTWARE\dotnet\Setup\InstalledVersions\x86\sharedfx\Microsoft.WindowsDesktop.App",
            @"SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App"
        ];

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            foreach (var subKeyPath in subKeyPaths)
            {
                using var subKey = baseKey.OpenSubKey(subKeyPath);
                if (subKey is null)
                {
                    continue;
                }

                foreach (var valueName in subKey.GetValueNames())
                {
                    if (Version.TryParse(valueName, out var version) && version.Major == 8)
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool HasDesktopRuntime8FromDotnet()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--list-runtimes",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            });

            if (process is null)
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            return output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Any(line => line.StartsWith("Microsoft.WindowsDesktop.App 8.", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static string BuildArguments(IEnumerable<string> args)
    {
        return string.Join(" ", args.Select(QuoteArgument));
    }

    private static string QuoteArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
        {
            return "\"\"";
        }

        if (!arg.Any(ch => char.IsWhiteSpace(ch) || ch == '"'))
        {
            return arg;
        }

        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
