// COPYRIGHT 2026 by the Riel project.
// This file is part of Riel, a fork of Open Rails, under the GPL version 3 or later.

using Avalonia;
using Avalonia.Headless;

using Riel.Launcher.Gui;

using Xunit;

[assembly: AvaloniaTestApplication(typeof(Tests.Launcher.Gui.HeadlessSetup))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Tests.Launcher.Gui
{
    public static class HeadlessSetup
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions());
        }
    }
}
