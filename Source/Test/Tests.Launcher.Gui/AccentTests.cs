// COPYRIGHT 2026 by the Riel project.
// This file is part of Riel, a fork of Open Rails, under the GPL version 3 or later.

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;

using Riel.Launcher.Gui;
using Riel.Models.Settings;

using Xunit;

namespace Tests.Launcher.Gui
{
    public partial class MainWindowTests
    {
        [AvaloniaTheory]
        [InlineData("amber", false)]
        [InlineData("blue", false)]
        [InlineData("teal", false)]
        [InlineData("green", false)]
        [InlineData("violet", false)]
        [InlineData("rose", false)]
        [InlineData("orange", false)]
        [InlineData("amber", true)]
        [InlineData("blue", true)]
        [InlineData("teal", true)]
        [InlineData("green", true)]
        [InlineData("violet", true)]
        [InlineData("rose", true)]
        [InlineData("orange", true)]
        public void EveryAccentRendersTheChosenColorsAndReadablePlayStates(string id, bool light)
        {
            using AppearanceScope appearance = new AppearanceScope();
            AppearanceCall("Set", !light, appearance.Folder);
            AppearanceCall("PreviewAccent", id);
            object option = AccentOptions().Single(item => OptionId(item) == id);
            object palette = option.GetType().GetProperty(light ? "LightPalette" : "DarkPalette",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(option);
            ThemeVariant themeVariant = light ? ThemeVariant.Light : ThemeVariant.Dark;
            Assert.Equal(PaletteColor(palette, "Accent"),
                Application.Current.Styles.OfType<FluentTheme>().Single().Palettes[themeVariant].Accent);

            RenderFixtureWindow window = new RenderFixtureWindow { RequestedThemeVariant = themeVariant };
            SeedVisualFixture(window, 1, false);
            window.Show();
            string theme = light ? "light" : "dark";
            try
            {
                Button play = Control<Button>(window, "PlayButton");
                Capture(window, $"accent-{id}-{theme}-normal");
                AssertPlayPalette(play, palette, "Accent");

                ListBoxItem selectedRoute = Assert.IsAssignableFrom<ListBoxItem>(
                    Control<ListBox>(window, "RouteList").ContainerFromIndex(0));
                Assert.True(selectedRoute.IsSelected);
                ContentPresenter routePresenter = Assert.Single(selectedRoute.GetVisualDescendants().OfType<ContentPresenter>()
                    .Where(presenter => presenter.Name == "PART_ContentPresenter"));
                Color selectedBackground = Assert.IsAssignableFrom<ISolidColorBrush>(routePresenter.Background).Color;
                Color selectedForeground = Assert.IsAssignableFrom<ISolidColorBrush>(selectedRoute.GetVisualDescendants()
                    .OfType<TextBlock>().First(label => label.Text == "Great Northern Coast").Foreground).Color;
                Assert.Equal(PaletteColor(palette, "Selected"), selectedBackground);
                Assert.True(Contrast(selectedForeground, selectedBackground) >= 4.5,
                    "Selection text must stay readable after changing the accent.");

                Point center = play.TranslatePoint(new Point(play.Bounds.Width / 2, play.Bounds.Height / 2), window).Value;
                window.MouseMove(center);
                Capture(window, $"accent-{id}-{theme}-hover");
                Assert.True(play.IsPointerOver);
                AssertPlayPalette(play, palette, "Hover");

                window.MouseDown(center, MouseButton.Left);
                Capture(window, $"accent-{id}-{theme}-pressed");
                AssertPlayPalette(play, palette, "Pressed");
                window.MouseMove(new Point(1, 1));
                window.MouseUp(new Point(1, 1), MouseButton.Left);

                Assert.True(play.Focus(NavigationMethod.Tab));
                Capture(window, $"accent-{id}-{theme}-focus");
                Assert.True(play.IsFocused);
                AssertPlayPalette(play, palette, "Accent");

                Set(window, "routeLoading", true);
                Call(window, "UpdateButtons");
                Capture(window, $"accent-{id}-{theme}-disabled");
                Assert.False(play.IsEnabled);
                AssertPlayPalette(play, palette, "Disabled");
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void SavedAccentSurvivesRestartAndBothThemeSwitches()
        {
            using AppearanceScope appearance = new AppearanceScope();
            AppearanceCall("SetAccent", "violet", appearance.Folder);
            AppearanceCall("Set", false, appearance.Folder);
            AppearanceCall("PreviewAccent", "rose");
            AppearanceCall("Load", appearance.Folder);
            Assert.Equal("violet", AppearanceValue<string>("AccentId"));
            Assert.False(AppearanceValue<bool>("Dark"));

            AppearanceCall("Set", true, appearance.Folder);
            AppearanceCall("PreviewAccent", "blue");
            AppearanceCall("Load", appearance.Folder);
            Assert.Equal("violet", AppearanceValue<string>("AccentId"));
            Assert.True(AppearanceValue<bool>("Dark"));
            Assert.Equal("violet", File.ReadAllText(Path.Combine(appearance.Folder, "launcher-accent")));
        }

        [AvaloniaFact]
        public void MissingInvalidAndMixedCaseAccentSettingsLoadSafely()
        {
            using AppearanceScope appearance = new AppearanceScope();
            AppearanceCall("PreviewAccent", "blue");
            AppearanceCall("Load", appearance.Folder);
            Assert.Equal("amber", AppearanceValue<string>("AccentId"));
            Assert.True(AppearanceValue<bool>("Dark"));

            string file = Path.Combine(appearance.Folder, "launcher-accent");
            Directory.CreateDirectory(appearance.Folder);
            foreach (string invalid in new[] { "", "unknown-color", "#FFFFFF" })
            {
                File.WriteAllText(file, invalid);
                AppearanceCall("Load", appearance.Folder);
                Assert.Equal("amber", AppearanceValue<string>("AccentId"));
            }

            File.WriteAllText(file, "  BlUe \n");
            AppearanceCall("Load", appearance.Folder);
            Assert.Equal("blue", AppearanceValue<string>("AccentId"));
        }

        [AvaloniaFact]
        public void AConfigurationWriteFailureStillAppliesTheColorAndTheme()
        {
            using AppearanceScope appearance = new AppearanceScope();
            Directory.CreateDirectory(appearance.Folder);
            string blockedFolder = Path.Combine(appearance.Folder, "ordinary-file");
            File.WriteAllText(blockedFolder, "This path cannot become a settings directory.");

            // A file in place of the settings directory fails on every OS, even when the
            // test runner has permission to ignore ordinary read-only file attributes.
            AppearanceCall("SetAccent", "teal", blockedFolder);
            AppearanceCall("Set", false, blockedFolder);
            Assert.Equal("teal", AppearanceValue<string>("AccentId"));
            Assert.False(AppearanceValue<bool>("Dark"));
            Assert.Equal(ThemeVariant.Light, Application.Current.RequestedThemeVariant);
            AppearanceCall("Load", blockedFolder);
            Assert.Equal("amber", AppearanceValue<string>("AccentId"));
        }

        [AvaloniaTheory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void SettingsPreviewsAllColorsAndCancelOrCloseRestoresTheSavedChoice(bool light, bool closeWindow)
        {
            using AppearanceScope appearance = new AppearanceScope();
            AppearanceCall("SetAccent", "green", appearance.Folder);
            AppearanceCall("Set", !light, appearance.Folder);
            ThemeVariant themeVariant = light ? ThemeVariant.Light : ThemeVariant.Dark;
            RenderFixtureWindow launcher = new RenderFixtureWindow { RequestedThemeVariant = themeVariant };
            SeedVisualFixture(launcher, 1, false);
            launcher.Show();
            RenderFixtureSettingsWindow settings = new RenderFixtureSettingsWindow
            {
                RequestedThemeVariant = themeVariant,
            };
            ComboBox choice = Assert.IsAssignableFrom<ComboBox>(settings.FindControl<ComboBox>("AccentColorBox"));
            Popup colorPopup = null;
            choice.TemplateApplied += (_, e) => colorPopup = e.NameScope.Find("PART_Popup") as Popup;
            settings.Show();
            try
            {
                // Profile loading is covered separately. Enable editing without touching a
                // real profile, then exercise the actual selector and cancel event handlers.
                typeof(SettingsWindow).GetMethod("SetEditingEnabled", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(settings, new object[] { true });
                object[] options = choice.ItemsSource.Cast<object>().ToArray();
                Assert.Equal(new[] { "amber", "blue", "teal", "green", "violet", "rose", "orange" },
                    options.Select(OptionId).ToArray());
                Assert.Equal("green", OptionId(choice.SelectedItem));
                Assert.NotNull(choice.ItemTemplate);
                Button play = Control<Button>(launcher, "PlayButton");
                Color original = PlayBackgroundColor(play);

                choice.SelectedItem = options.Single(option => OptionId(option) == "blue");
                Assert.Equal("blue", AppearanceValue<string>("AccentId"));
                string theme = light ? "light" : "dark";
                Capture(launcher, $"accent-settings-{theme}-launcher-preview");
                Capture(settings, $"accent-settings-{theme}-preview");
                Assert.NotEqual(original, PlayBackgroundColor(play));
                AssertPlayTextContrast(play);
                Assert.Equal("green", File.ReadAllText(Path.Combine(appearance.Folder, "launcher-accent")));

                choice.IsDropDownOpen = true;
                Capture(settings, $"accent-settings-{theme}-color-options");
                Assert.NotNull(colorPopup);
                Capture(Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(colorPopup.Child)),
                    $"accent-settings-{theme}-color-menu");
                choice.IsDropDownOpen = false;

                if (closeWindow)
                    settings.Close();
                else
                {
                    Button cancel = Assert.IsAssignableFrom<Button>(settings.FindControl<Button>("CancelButton"));
                    Point center = cancel.TranslatePoint(new Point(cancel.Bounds.Width / 2, cancel.Bounds.Height / 2), settings).Value;
                    settings.MouseDown(center, MouseButton.Left);
                    settings.MouseUp(center, MouseButton.Left);
                }

                Assert.False(settings.IsVisible);
                Assert.Equal("green", AppearanceValue<string>("AccentId"));
                Capture(launcher, $"accent-settings-{theme}-{(closeWindow ? "closed" : "cancelled")}");
                Assert.Equal(original, PlayBackgroundColor(play));
                AppearanceCall("PreviewAccent", "rose");
                AppearanceCall("Load", appearance.Folder);
                Assert.Equal("green", AppearanceValue<string>("AccentId"));
            }
            finally
            {
                settings.Close();
                launcher.Close();
            }
        }

        private static void AssertPlayPalette(Button play, object palette, string colorName)
        {
            Assert.Equal(PaletteColor(palette, colorName), PlayBackgroundColor(play));
            AssertPlayTextContrast(play);
        }

        private static Color PlayBackgroundColor(Button play) => Assert.IsAssignableFrom<ISolidColorBrush>(
            Assert.Single(play.GetVisualDescendants().OfType<ContentPresenter>()
                .Where(presenter => presenter.Name == "PART_ContentPresenter")).Background).Color;

        private static Color PaletteColor(object palette, string name) =>
            (Color)palette.GetType().GetProperty(name).GetValue(palette);

        private static Type AppearanceType => typeof(App).Assembly.GetType("Riel.Launcher.Gui.Appearance", throwOnError: true);

        private static void AppearanceCall(string name, params object[] arguments)
        {
            MethodInfo method = AppearanceType.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, arguments.Select(argument => argument.GetType()).ToArray(), null);
            Assert.NotNull(method);
            method.Invoke(null, arguments);
        }

        private static T AppearanceValue<T>(string name) => (T)AppearanceType.GetProperty(name).GetValue(null);

        private static object[] AccentOptions() => ((IEnumerable)AppearanceValue<object>("AccentOptions")).Cast<object>().ToArray();

        private static string OptionId(object option) => (string)option.GetType().GetProperty("Id").GetValue(option);

        private sealed class AppearanceScope : IDisposable
        {
            private readonly bool originalDark = AppearanceValue<bool>("Dark");
            private readonly string originalAccent = AppearanceValue<string>("AccentId");

            public string Folder { get; } = Path.Combine(Path.GetTempPath(), "riel-accent-tests-" + Guid.NewGuid().ToString("N"));

            public void Dispose()
            {
                try
                {
                    AppearanceCall("PreviewAccent", originalAccent);
                    AppearanceCall("Set", originalDark, Folder);
                }
                finally
                {
                    if (Directory.Exists(Folder))
                        Directory.Delete(Folder, recursive: true);
                }
            }
        }

        private sealed class RenderFixtureSettingsWindow : SettingsWindow
        {
            public RenderFixtureSettingsWindow() : base(new ProfileModel("accent-test"))
            {
            }

            protected override void OnOpened(EventArgs e)
            {
                // Keep real settings controls and event handlers without loading/writing
                // the current user's simulator profile or starting asynchronous work.
            }
        }
    }
}
