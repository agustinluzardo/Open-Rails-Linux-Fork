// COPYRIGHT 2026 by the Riel project.
// This file is part of Riel, a fork of Open Rails, under the GPL version 3 or later.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using Riel.Common;
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
            public FixtureRoute(string name, string id) : base(default)
            {
                Name = name;
                Id = id;
            }
        }
    }
}
