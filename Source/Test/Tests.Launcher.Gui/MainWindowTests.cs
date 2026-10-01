// COPYRIGHT 2026 by the Riel project.
// This file is part of Riel, a fork of Open Rails, under the GPL version 3 or later.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

using Riel.Common;
using Riel.Common.Position;
using Riel.Launcher.Gui;
using Riel.Models.Content;

using Xunit;

namespace Tests.Launcher.Gui
{
    public class MainWindowTests
    {
        // Construct the actual launcher and load its compiled XAML. Showing the window would
        // fire Opened and rescan the user's content, so these tests leave it unopened.
        [AvaloniaFact]
        public void ANewLauncherExplainsWhyPlayingIsUnavailable()
        {
            MainWindow window = new MainWindow();

            Assert.False(Control<Button>(window, "PlayButton").IsEnabled);
            Assert.False(Control<Button>(window, "ResumeButton").IsEnabled);
            Assert.False(Control<Border>(window, "SelectionDetailsPanel").IsVisible);
            Assert.False(Control<Border>(window, "BusyOverlay").IsVisible);
            Assert.False(string.IsNullOrWhiteSpace(Control<TextBlock>(window, "SelectionHint").Text));
        }

        [AvaloniaFact]
        public void FilteringOutTheSelectedRouteClearsItsPlayableContent()
        {
            MainWindow window = new MainWindow();
            RouteItem route = SeedPlayableActivity(window);
            Assert.True(Control<Button>(window, "PlayButton").IsEnabled);

            Control<TextBox>(window, "RouteFilter").Text = "no matching route";
            Call(window, "ApplyRouteFilter");

            Assert.Empty(Control<ListBox>(window, "RouteList").ItemsSource.Cast<RouteItem>());
            Assert.Null(Control<ListBox>(window, "RouteList").SelectedItem);
            Assert.Null(Control<ListBox>(window, "ActivityList").ItemsSource);
            Assert.Null(Control<ComboBox>(window, "PathBox").ItemsSource);
            Assert.Null(Control<ListBox>(window, "ConsistList").ItemsSource);
            Assert.Null(Control<ComboBox>(window, "TimetableWeatherFileBox").ItemsSource);
            Assert.False(Control<Button>(window, "PlayButton").IsEnabled);
            Assert.False(Control<Border>(window, "SelectionDetailsPanel").IsVisible);
            Assert.True(Control<TextBlock>(window, "NoRoutes").IsVisible);

            Control<TextBox>(window, "RouteFilter").Text = string.Empty;
            Call(window, "ApplyRouteFilter");

            Assert.Same(route, Assert.Single(Control<ListBox>(window, "RouteList").ItemsSource.Cast<RouteItem>()));
            Assert.Null(Control<ListBox>(window, "RouteList").SelectedItem);
            Assert.False(Control<TextBlock>(window, "NoRoutes").IsVisible);
            Assert.False(Control<Button>(window, "PlayButton").IsEnabled);
        }

        [AvaloniaFact]
        public void FilteringByTheContentFolderPreservesAVisibleSelection()
        {
            MainWindow window = new MainWindow();
            RouteItem route = SeedPlayableActivity(window);

            Control<TextBox>(window, "RouteFilter").Text = "  aRGENTINA  ";
            Call(window, "ApplyRouteFilter");

            Assert.Same(route, Assert.Single(Control<ListBox>(window, "RouteList").ItemsSource.Cast<RouteItem>()));
            Assert.Same(route, Control<ListBox>(window, "RouteList").SelectedItem);
            Assert.True(Control<Button>(window, "PlayButton").IsEnabled);
            Assert.False(Control<TextBlock>(window, "NoRoutes").IsVisible);
        }

        [AvaloniaTheory]
        [InlineData("running")]
        [InlineData("launching")]
        [InlineData("busyDepth")]
        public async Task PlayingWhileAnotherOperationIsActiveReturnsBeforeOpeningADialog(string field)
        {
            MainWindow window = new MainWindow();
            Set(window, field, field == "busyDepth" ? (object)1 : true);

            // There is deliberately no selection. If the guard is bypassed, Play attempts to
            // show its missing-selection dialog on an unopened parent and the test fails.
            Task play = (Task)Call(window, "Play");
            Assert.True(play.IsCompletedSuccessfully);
            await play;
        }

        [AvaloniaFact]
        public void BusyOperationsKeepActionsDisabledUntilTheLastOperationFinishes()
        {
            MainWindow window = new MainWindow();
            SeedPlayableActivity(window);
            Set(window, "hasSave", true);
            Call(window, "UpdateButtons");
            AssertActionsEnabled(window, true);

            IDisposable first = (IDisposable)Call(window, "Busy", "First operation");
            IDisposable second = (IDisposable)Call(window, "Busy", "Second operation");
            try
            {
                AssertActionsEnabled(window, false);
                Assert.True(Control<Border>(window, "BusyOverlay").IsVisible);

                // Completion order can differ from start order. One completion must not
                // remove the overlay or allow launching while the other task is active.
                first.Dispose();
                first.Dispose();
                AssertActionsEnabled(window, false);
                Assert.True(Control<Border>(window, "BusyOverlay").IsVisible);

                second.Dispose();
                AssertActionsEnabled(window, true);
                Assert.False(Control<Border>(window, "BusyOverlay").IsVisible);
            }
            finally
            {
                first.Dispose();
                second.Dispose();
            }
        }

        [AvaloniaFact]
        public void LoadingARoutePreventsPlayingPreviouslySelectedContent()
        {
            MainWindow window = new MainWindow();
            SeedPlayableActivity(window);
            Assert.True(Control<Button>(window, "PlayButton").IsEnabled);

            Set(window, "routeLoading", true);
            Call(window, "UpdateButtons");
            Assert.False(Control<Button>(window, "PlayButton").IsEnabled);

            Set(window, "routeLoading", false);
            Call(window, "UpdateButtons");
            Assert.True(Control<Button>(window, "PlayButton").IsEnabled);
        }

        [AvaloniaTheory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void SelectedContentInformationIsAvailableWithoutExpandingIt(int mode)
        {
            RenderFixtureWindow window = new RenderFixtureWindow();
            SeedVisualFixture(window, mode, false);
            window.Show();
            try
            {
                Capture(window, $"information-mode-{mode}");
                Assert.True(Control<Border>(window, "RouteInformationPanel").IsEffectivelyVisible);
                Assert.Contains("coastal railway", Control<TextBlock>(window, "RouteDescription").Text);

                Border details = Control<Border>(window, "SelectionDetailsPanel");
                Assert.True(details.IsEffectivelyVisible);
                Assert.DoesNotContain(details.GetVisualDescendants(), visual => visual is Expander);
                AssertVisibleInside(window, Control<TextBlock>(window, "DetailsTitle"));
                AssertVisibleInside(window, Control<TextBlock>(window, "DetailsSummary"));
                Assert.Contains("Northport", Control<TextBlock>(window, "DetailsRoute").Text);
                Assert.Contains("EMD GP38-2", Control<TextBlock>(window, "DetailsCars").Text);
                Assert.Contains("2,000 hp diesel-electric locomotive", Control<TextBlock>(window, "DetailsCars").Text);
                if (mode == 0)
                {
                    Assert.Contains("Stop at every station", Control<TextBlock>(window, "DetailsDescription").Text);
                    Assert.Contains("Keep to the timetable", Control<TextBlock>(window, "DetailsDescription").Text);
                }
                else if (mode == 2)
                {
                    Assert.Contains("Morning commuter service", Control<TextBlock>(window, "DetailsDescription").Text);
                }
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ChangingModeOrLoadingAnotherRouteDoesNotKeepPreviousBriefingOrTrainDetails()
        {
            MainWindow window = new MainWindow();
            SeedVisualFixture(window, 0, false);
            Assert.Contains("Stop at every station", Control<TextBlock>(window, "DetailsDescription").Text);

            Control<TabControl>(window, "ModeTabs").SelectedIndex = 1;
            Assert.True(string.IsNullOrEmpty(Control<TextBlock>(window, "DetailsDescription").Text));
            Assert.Contains("EMD GP38-2", Control<TextBlock>(window, "DetailsCars").Text);

            Set(window, "routeLoading", true);
            Call(window, "UpdateButtons");
            Assert.True(string.IsNullOrEmpty(Control<TextBlock>(window, "DetailsCars").Text));
            Assert.True(string.IsNullOrEmpty(Control<TextBlock>(window, "DetailsRoute").Text));
            Assert.True(string.IsNullOrEmpty(Control<TextBlock>(window, "DetailsDescription").Text));
            Assert.True(Control<Border>(window, "RouteInformationPanel").IsVisible);
            Assert.Contains("coastal railway", Control<TextBlock>(window, "RouteDescription").Text);
        }

        [AvaloniaTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void PlayRemainsReadableThroughItsRealTemplateInEveryInteractionState(bool light)
        {
            RenderFixtureWindow window = new RenderFixtureWindow
            {
                RequestedThemeVariant = light ? ThemeVariant.Light : ThemeVariant.Dark,
            };
            SeedVisualFixture(window, 1, false);
            window.Show();
            string theme = light ? "light" : "dark";
            try
            {
                Button play = Control<Button>(window, "PlayButton");
                Capture(window, $"play-{theme}-normal");
                Assert.True(play.IsEnabled);
                AssertPlayTextContrast(play);

                Point center = play.TranslatePoint(new Point(play.Bounds.Width / 2, play.Bounds.Height / 2), window).Value;
                window.MouseMove(center);
                Capture(window, $"play-{theme}-hover");
                Assert.True(play.IsPointerOver);
                AssertPlayTextContrast(play);

                window.MouseDown(center, MouseButton.Left);
                Capture(window, $"play-{theme}-pressed");
                AssertPlayTextContrast(play);
                // Release outside the action: this verifies the pressed style without launching.
                window.MouseMove(new Point(1, 1));
                window.MouseUp(new Point(1, 1), MouseButton.Left);

                Assert.True(play.Focus(NavigationMethod.Tab));
                Capture(window, $"play-{theme}-focus");
                Assert.True(play.IsFocused);
                AssertPlayTextContrast(play);

                Set(window, "routeLoading", true);
                Call(window, "UpdateButtons");
                Capture(window, $"play-{theme}-disabled");
                Assert.False(play.IsEnabled);
                AssertPlayTextContrast(play);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaTheory]
        [InlineData(false, 0, false)]
        [InlineData(false, 1, false)]
        [InlineData(false, 2, false)]
        [InlineData(true, 0, false)]
        [InlineData(true, 1, false)]
        [InlineData(true, 2, false)]
        [InlineData(false, 0, true)]
        [InlineData(false, 1, true)]
        [InlineData(false, 2, true)]
        [InlineData(true, 0, true)]
        [InlineData(true, 1, true)]
        [InlineData(true, 2, true)]
        public void LauncherKeepsItsActionsAndInformationOnScreenAtBothSupportedSizes(bool light, int mode, bool minimum)
        {
            RenderFixtureWindow window = new RenderFixtureWindow
            {
                RequestedThemeVariant = light ? ThemeVariant.Light : ThemeVariant.Dark,
                Width = minimum ? 880 : 1180,
                Height = minimum ? 600 : 740,
            };
            SeedVisualFixture(window, mode, minimum);
            window.Show();
            try
            {
                string modeName = new[] { "activity", "explore", "timetable" }[mode];
                string name = $"launcher-{(light ? "light" : "dark")}-{modeName}-{(minimum ? "minimum" : "default")}";
                Capture(window, name);
                foreach (string control in new[] { "PlayButton", "ResumeButton", "ContentButton", "ToolsButton", "SettingsButton" })
                    AssertVisibleInside(window, Control<Button>(window, control));
                AssertVisibleInside(window, Control<Border>(window, "SelectionDetailsPanel"));
                AssertVisibleInside(window, Control<Border>(window, "RouteInformationPanel"));
                AssertVisibleInside(window, Control<TextBlock>(window, "DetailsTitle"));
                AssertVisibleInside(window, Control<TextBlock>(window, "SelectionHint"));
                AssertVisibleInside(window, Control<TabControl>(window, "ModeTabs"));
                Assert.True(Control<TabControl>(window, "ModeTabs").Bounds.Height >= 100,
                    "Selected content must retain a usable viewport when metadata is visible.");

                if (mode == 1)
                {
                    // On small displays the weather controls can require scrolling. They must
                    // remain reachable rather than being clipped outside a fixed panel.
                    ComboBox weather = Control<ComboBox>(window, "WeatherBox");
                    weather.BringIntoView();
                    Capture(window, name + "-scrolled");
                    AssertVisibleInside(window, weather);
                }
                if (mode == 1 && !minimum)
                {
                    Button tools = Control<Button>(window, "ToolsButton");
                    tools.ContextMenu.Open(tools);
                    Capture(window, name + "-tools");
                    Assert.True(tools.ContextMenu.IsOpen);
                    tools.ContextMenu.Close();
                }
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ResizingKeepsInformationVisibleBesideOrBelowTheForm()
        {
            RenderFixtureWindow window = new RenderFixtureWindow { Width = 880, Height = 600 };
            SeedVisualFixture(window, 1, true);
            window.Show();
            try
            {
                foreach (bool compact in new[] { true, false, true })
                {
                    window.Width = compact ? 880 : 1180;
                    window.Height = compact ? 600 : 740;
                    Capture(window, compact ? "launcher-resize-compact" : "launcher-resize-wide");
                    Assert.Equal(compact, window.Classes.Contains("compact"));
                    Border information = Control<Border>(window, "SelectionDetailsPanel");
                    TabControl modes = Control<TabControl>(window, "ModeTabs");
                    AssertVisibleInside(window, information);
                    AssertVisibleInside(window, modes);
                    Point informationOrigin = information.TranslatePoint(default, window).Value;
                    Point modesOrigin = modes.TranslatePoint(default, window).Value;
                    Assert.True(compact
                        ? informationOrigin.Y >= modesOrigin.Y + modes.Bounds.Height
                        : informationOrigin.X >= modesOrigin.X + modes.Bounds.Width,
                        "Information must follow the form's available space when the window changes size.");
                }
            }
            finally
            {
                window.Close();
            }
        }

        private static void AssertPlayTextContrast(Button play)
        {
            // Check the applied Fluent template, not only the Button properties: its state
            // setters can replace the background/foreground after application styles run.
            ContentPresenter presenter = Assert.Single(play.GetVisualDescendants().OfType<ContentPresenter>()
                .Where(control => control.Name == "PART_ContentPresenter"));
            TextBlock label = Assert.Single(play.GetVisualDescendants().OfType<TextBlock>()
                .Where(control => string.Equals(control.Text, "Play", StringComparison.Ordinal)));
            Color background = Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color;
            Color foreground = Assert.IsAssignableFrom<ISolidColorBrush>(label.Foreground).Color;
            Assert.True(background.A == 255, "The primary action must have an opaque background.");
            double contrast = Contrast(foreground, background);
            Assert.True(contrast >= 4.5, $"Play text contrast is {contrast:F2}:1; expected at least 4.5:1.");
            Avalonia.Controls.Shapes.Path icon = Assert.Single(play.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>());
            Color iconForeground = Assert.IsAssignableFrom<ISolidColorBrush>(icon.Fill).Color;
            Assert.True(Contrast(iconForeground, background) >= 4.5,
                "The Play icon must remain readable against the applied button background.");
        }

        private static double Contrast(Color foreground, Color background)
        {
            double opacity = foreground.A / 255.0;
            double red = foreground.R * opacity + background.R * (1 - opacity);
            double green = foreground.G * opacity + background.G * (1 - opacity);
            double blue = foreground.B * opacity + background.B * (1 - opacity);
            double first = Luminance(red, green, blue);
            double second = Luminance(background.R, background.G, background.B);
            return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
        }

        private static double Luminance(double red, double green, double blue)
        {
            static double Linear(double channel)
            {
                channel /= 255;
                return channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Linear(red) + 0.7152 * Linear(green) + 0.0722 * Linear(blue);
        }

        private static void AssertVisibleInside(MainWindow window, Control control)
        {
            Assert.True(control.IsEffectivelyVisible, $"{control.Name} must be visible.");
            Assert.True(control.Bounds.Width > 0 && control.Bounds.Height > 0, $"{control.Name} must be laid out.");
            Point origin = control.TranslatePoint(default, window).Value;
            Rect bounds = new Rect(origin, control.Bounds.Size);
            Assert.True(bounds.X >= -1 && bounds.Y >= -1
                && bounds.Right <= window.ClientSize.Width + 1 && bounds.Bottom <= window.ClientSize.Height + 1,
                $"{control.Name} bounds {bounds} must fit inside the {window.ClientSize} client area.");
            foreach (ScrollViewer scroll in control.GetVisualAncestors().OfType<ScrollViewer>())
            {
                Point relative = control.TranslatePoint(default, scroll).Value;
                Rect viewport = new Rect(scroll.Bounds.Size);
                Assert.True(viewport.Intersects(new Rect(relative, control.Bounds.Size)),
                    $"{control.Name} must intersect its scroll viewport.");
            }
        }

        private static void Capture(MainWindow window, string name)
        {
            using var bitmap = window.CaptureRenderedFrame();
            Assert.NotNull(bitmap);
            string directory = Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP") ?? Path.GetTempPath(), "riel-launcher-previews");
            Directory.CreateDirectory(directory);
            bitmap.Save(Path.Combine(directory, name + ".png"));
        }

        private static void SeedVisualFixture(MainWindow window, int mode, bool longNames)
        {
            FolderModel folder = new FolderModel("Coastal collection", "/test-content", null);
            RouteItem route = new RouteItem(folder, new FixtureRoute(
                longNames ? "Great Northern Coast — Northport to Bayview and the Old Harbour branch" : "Great Northern Coast", "coast")
            {
                Description = "A coastal railway linking Northport with Bayview. Drive commuter trains past the harbour, seaside towns and freight yards.",
            });
            List<RouteItem> routes = new List<RouteItem>
            {
                route,
                new RouteItem(folder, new FixtureRoute("Alpine Valley", "alpine")),
                new RouteItem(folder, new FixtureRoute("Western Main Line", "western")),
            };
            Set(window, "routes", routes);
            Set(window, "suppressRouteChanged", true);
            try
            {
                Control<ListBox>(window, "RouteList").ItemsSource = routes;
                Control<ListBox>(window, "RouteList").SelectedItem = route;
            }
            finally
            {
                Set(window, "suppressRouteChanged", false);
            }

            PathModelHeader path = new PathModelHeader
            {
                Id = "northport-bayview", Name = "Northport to Bayview", Start = "Northport", End = "Bayview", PlayerPath = true,
            };
            Set(window, "routePaths", new[] { path });
            Control<ComboBox>(window, "PathBox").ItemsSource = new[] { new PathItem(path) };
            Control<ComboBox>(window, "PathBox").SelectedIndex = 0;

            WagonReferenceModel engine = new WagonReferenceModel
            {
                Name = longNames ? "EMD GP38-2 — Great Northern Railway extended cab diesel locomotive" : "EMD GP38-2",
                Reference = "gp38\\gp38", TrainCarType = TrainCarType.Engine,
                Description = "A 2,000 hp diesel-electric locomotive for passenger and freight services.",
            };
            WagonSetModel train = new WagonSetModel
            {
                Id = "coastal-passenger", Name = "Coastal passenger — 4 coaches",
                TrainCars = ImmutableArray.Create(engine,
                    new WagonReferenceModel { Name = "Passenger coach", TrainCarType = TrainCarType.Wagon },
                    new WagonReferenceModel { Name = "Passenger coach", TrainCarType = TrainCarType.Wagon },
                    new WagonReferenceModel { Name = "Passenger coach", TrainCarType = TrainCarType.Wagon },
                    new WagonReferenceModel { Name = "Passenger coach", TrainCarType = TrainCarType.Wagon }),
            };
            List<ConsistItem> consists = new List<ConsistItem>
            {
                new ConsistItem(train),
                new ConsistItem(train with { Id = "coastal-freight", Name = "Local freight — 4 cars" }),
            };
            Set(window, "consists", consists);
            Set(window, "consistsFolder", folder);
            Control<ComboBox>(window, "LocomotiveBox").ItemsSource = new[]
            {
                new Choice<string>(null, "All locomotives"),
                new Choice<string>(consists[0].LocomotiveKey, consists[0].LocomotiveName),
            };
            Control<ComboBox>(window, "LocomotiveBox").SelectedIndex = 1;
            Control<ListBox>(window, "ConsistList").SelectedItem = consists[0];

            ActivityItem morning = new ActivityItem(new ActivityModelHeader
            {
                Id = "morning-passenger", Name = "Morning coastal passenger", ActivityType = ActivityType.Activity,
                StartTime = new TimeOnly(8, 30), Season = SeasonType.Summer, Weather = WeatherType.Clear,
                Difficulty = Difficulty.Easy, Duration = TimeSpan.FromMinutes(45),
                PathId = path.Id, ConsistId = train.Id,
                Description = "Stop at every station between Northport and Bayview.", Briefing = "Keep to the timetable and respect the signals.",
            });
            Control<ListBox>(window, "ActivityList").ItemsSource = new[]
            {
                morning,
                new ActivityItem(morning.Activity with { Id = "afternoon-express", Name = "Afternoon express", StartTime = new TimeOnly(15, 0) }),
                new ActivityItem(morning.Activity with { Id = "evening-passenger", Name = "Evening coastal passenger", StartTime = new TimeOnly(18, 15) }),
            };
            Control<ListBox>(window, "ActivityList").SelectedIndex = 0;
            Call(window, "SetTimePresets", new[] { morning });

            TimetableTrainModel service = new TimetableTrainModel
            {
                Id = "0830", Name = "08:30 Northport — Bayview", Group = "Weekday services",
                StartTime = new TimeOnly(8, 30), Path = path.Id, WagonSet = train.Id,
                Briefing = "Morning commuter service. Call at every station to Bayview.",
            };
            TimetableModel timetable = new TimetableModel
            {
                Id = "weekday", Name = "Coastal weekday timetable", TimetableTrains = ImmutableArray.Create(service),
            };
            Control<ComboBox>(window, "TimetableBox").ItemsSource = new[] { new TimetableItem(timetable) };
            Control<ComboBox>(window, "TimetableBox").SelectedIndex = 0;
            Control<ComboBox>(window, "TimetableWeatherFileBox").ItemsSource = new[] { new WeatherFileItem(null) };
            Control<ComboBox>(window, "TimetableWeatherFileBox").SelectedIndex = 0;
            Control<TabControl>(window, "ModeTabs").SelectedIndex = mode;
            Set(window, "hasSave", true);
            Call(window, "UpdateButtons");
        }

        private static RouteItem SeedPlayableActivity(MainWindow window)
        {
            FolderModel folder = new FolderModel("Argentina", "/test-content", null);
            RouteItem route = new RouteItem(folder, new FixtureRoute("Coastal railway", "coast"));
            List<RouteItem> routes = new List<RouteItem> { route };
            Set(window, "routes", routes);
            Set(window, "suppressRouteChanged", true);
            try
            {
                Control<ListBox>(window, "RouteList").ItemsSource = routes;
                Control<ListBox>(window, "RouteList").SelectedItem = route;
            }
            finally
            {
                Set(window, "suppressRouteChanged", false);
            }

            ActivityItem activity = new ActivityItem(new ActivityModelHeader
            {
                Id = "morning-passenger",
                Name = "Morning passenger",
                ActivityType = ActivityType.Activity,
                StartTime = new TimeOnly(8, 30),
            });
            Control<TabControl>(window, "ModeTabs").SelectedIndex = 0;
            Control<ListBox>(window, "ActivityList").ItemsSource = new[] { activity };
            Control<ListBox>(window, "ActivityList").SelectedItem = activity;
            Call(window, "UpdateButtons");
            return route;
        }

        private static void AssertActionsEnabled(MainWindow window, bool enabled)
        {
            foreach (string name in new[] { "PlayButton", "ResumeButton", "ContentButton", "SettingsButton", "ToolsButton" })
                Assert.Equal(enabled, Control<Button>(window, name).IsEnabled);
            Assert.Equal(enabled, Control<Grid>(window, "MainArea").IsEnabled);
        }

        private static T Control<T>(MainWindow window, string name) where T : Control
        {
            return Assert.IsAssignableFrom<T>(window.FindControl<T>(name));
        }

        private static void Set(MainWindow window, string name, object value)
        {
            FieldInfo field = typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(window, value);
        }

        private static object Call(MainWindow window, string name, params object[] arguments)
        {
            MethodInfo method = typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            return method.Invoke(window, arguments);
        }

        private sealed record FixtureRoute : RouteModelHeader
        {
            public FixtureRoute(string name, string id) : base(default(WorldLocation))
            {
                Name = name;
                Id = id;
            }
        }

        private sealed class RenderFixtureWindow : MainWindow
        {
            // Render the real launcher while leaving filesystem rescans, network update
            // checks and profile loading to their existing integration tests.
            protected override void OnOpened(EventArgs e)
            {
            }
        }
    }
}
