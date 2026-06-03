using System;
using System.Windows;
using Velopack;

namespace BetterBTD;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        VelopackApp.Build()
            .SetAppUserModelId("ZiyaoZh.BetterBTD")
            .SetAutoApplyOnStartup(true)
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
