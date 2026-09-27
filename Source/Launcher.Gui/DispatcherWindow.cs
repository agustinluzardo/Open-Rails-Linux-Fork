// COPYRIGHT 2026 by the Riel project.
//
// This file is part of Riel, a fork of Open Rails.
//
// Riel is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Riel.Launcher.Gui
{
    /// <summary>
    /// Standalone Linux dispatcher. The simulator owns all simulation/graphics state and
    /// exposes a loopback-only snapshot; this window only renders that snapshot, so opening
    /// it never creates a second MonoGame GraphicsDevice in the simulator process.
    /// </summary>
    internal sealed class DispatcherWindow : Window
    {
        private readonly HttpClient client;
        private readonly DispatcherTimer timer;
        private readonly DispatcherMapControl map = new DispatcherMapControl();
        private readonly TextBlock status = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        private bool refreshing;
        private bool initialized;
        private readonly StackPanel details = new StackPanel { Spacing = 8, Margin = new Thickness(12) };
        private readonly Dictionary<string, TextBlock> detailRows = new Dictionary<string, TextBlock>();
        private DispatcherSnapshot latest;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
        };

        public DispatcherWindow(string baseUrl)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri))
                throw new ArgumentException("Invalid dispatcher URL.", nameof(baseUrl));

            Title = "Riel — Dispatcher";
            Width = 1200;
            Height = 780;
            MinWidth = 720;
            MinHeight = 480;

            client = new HttpClient
            {
                BaseAddress = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(2),
            };

            map.SelectionChanged += UpdateSelection;
            Button zoomIn = new Button { Content = "+", Margin = new Thickness(2, 8), MinWidth = 36 };
            Button zoomOut = new Button { Content = "−", Margin = new Thickness(2, 8), MinWidth = 36 };
            zoomIn.Click += (_, _) => map.ZoomAtCenter(1.4);
            zoomOut.Click += (_, _) => map.ZoomAtCenter(1 / 1.4);

            Button fit = new Button
            {
                Content = "Fit map",
                Margin = new Thickness(12, 8, 8, 8),
                VerticalAlignment = VerticalAlignment.Center,
            };
            fit.Click += (_, _) => map.Fit();

            TextBlock hint = new TextBlock
            {
                Text = "Click: select   •   Drag: pan   •   Wheel: zoom",
                Opacity = 0.65,
                Margin = new Thickness(12, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };

            status.Text = "Connecting to Riel…";
            status.Margin = new Thickness(8, 0, 12, 0);

            DockPanel toolbar = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(fit, Dock.Left);
            DockPanel.SetDock(hint, Dock.Right);
            toolbar.Children.Add(fit);
            DockPanel.SetDock(zoomIn, Dock.Left);
            DockPanel.SetDock(zoomOut, Dock.Left);
            toolbar.Children.Add(zoomIn);
            toolbar.Children.Add(zoomOut);
            toolbar.Children.Add(hint);
            toolbar.Children.Add(status);

            Border header = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(4, 0),
                Child = toolbar,
            };

            DockPanel root = new DockPanel();
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            Grid body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,280") };
            Grid.SetColumn(map, 0);
            body.Children.Add(map);
            Border sidebar = new Border
            {
                BorderThickness = new Thickness(1, 0, 0, 0),
                Child = new ScrollViewer { Content = details },
            };
            Grid.SetColumn(sidebar, 1);
            body.Children.Add(sidebar);
            root.Children.Add(body);
            Content = root;
            UpdateSelection();

            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += async (_, _) => await Refresh();
            Opened += async (_, _) =>
            {
                timer.Start();
                await Refresh();
            };
            Closed += (_, _) =>
            {
                timer.Stop();
                client.Dispose();
            };
        }

        private async Task Refresh()
        {
            if (refreshing)
                return;

            refreshing = true;
            try
            {
                if (!initialized)
                {
                    MapInit init = await client.GetFromJsonAsync<MapInit>("API/MAP/INIT", JsonOptions);
                    if (init != null)
                    {
                        map.SetMap(init);
                        initialized = true;
                    }
                }

                DispatcherSnapshot snapshot = await client.GetFromJsonAsync<DispatcherSnapshot>("API/DISPATCHER", JsonOptions);
                if (snapshot != null)
                {
                    bool firstSnapshot = latest == null;
                    latest = snapshot;
                    map.SetSnapshot(snapshot);
                    if (firstSnapshot)
                        UpdateSelection();
                    else
                        UpdateLiveDetails();
                    TimeSpan clock = TimeSpan.FromSeconds((snapshot.ClockTime % 86400 + 86400) % 86400);
                    status.Text = $"{clock:hh\\:mm\\:ss}   •   {snapshot.Trains?.Length ?? 0} trains   •   Connected";
                }
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException)
            {
                status.Text = initialized ? "Reconnecting to simulator…" : "Waiting for simulator…";
            }
            finally
            {
                refreshing = false;
            }
        }

        private void UpdateSelection()
        {
            details.Children.Clear();
            detailRows.Clear();
            string kind = map.SelectedKind;
            int index = map.SelectedIndex;
            if (kind == "train")
            {
                DispatcherTrainInfo train = latest?.Trains?.FirstOrDefault(item => item.Number == index);
                if (train == null)
                    return;
                Heading($"Train {train.Number}");
                Row("Service", train.Name);
                Row("Locomotive", string.IsNullOrWhiteSpace(train.Locomotive) ? "—" : train.Locomotive);
                if (!string.IsNullOrWhiteSpace(train.NextStation))
                {
                    Row("Next station", train.NextStation);
                    if (train.NextArrival.HasValue)
                        Row("Arrival", TimeSpan.FromSeconds((train.NextArrival.Value % 86400 + 86400) % 86400).ToString(@"hh\:mm", CultureInfo.CurrentCulture));
                    if (train.NextDeparture.HasValue)
                        Row("Departure", TimeSpan.FromSeconds((train.NextDeparture.Value % 86400 + 86400) % 86400).ToString(@"hh\:mm", CultureInfo.CurrentCulture));
                }
                Row("Type", train.TrainType + (train.IsFreight ? " · freight" : " · passenger"));
                Row("Control", train.ControlMode);
                Row("Speed", $"{Math.Abs(train.SpeedMpS) * 3.6:F1} km/h");
                Row("Direction", train.Direction);
                Row("Cars", train.CarCount.ToString(CultureInfo.CurrentCulture));
                if (train.Cars != null && train.Cars.Length > 0)
                    Row("Consist", string.Join(" · ", train.Cars.Where(car => !string.IsNullOrWhiteSpace(car))));
                Button follow = new Button { Content = "Center on train" };
                follow.Click += (_, _) => map.CenterAt(train.Front);
                details.Children.Add(follow);
            }
            else if (kind == "point")
            {
                MapPoint point = map.SelectedPoint;
                if (point == null)
                    return;
                Heading(point.Category ?? "Location");
                Row("Name", point.Name);
                if (!string.IsNullOrWhiteSpace(point.Detail) && point.Detail != point.Name)
                    Row("Platform / track", point.Detail);
                Row("Location", $"{point.LatLon.Lat:F5}, {point.LatLon.Lon:F5}");
                Button center = new Button { Content = "Center here" };
                center.Click += (_, _) => map.CenterAt(point.LatLon);
                details.Children.Add(center);
            }
            else if (kind == "signal")
            {
                DispatcherSignalInfo signal = latest?.Signals?.FirstOrDefault(item => item.Index == index);
                if (signal == null)
                    return;
                Heading($"Signal {signal.Index}");
                Row("Aspect", signal.Aspect);
                Row("Control", signal.State);
                Row("Train", signal.EnabledTrain?.ToString(CultureInfo.CurrentCulture) ?? "—");
                Heading("Change signal");
                Row("Command", "Choose an aspect.");
                AddCommand("System controlled", "signal", index, "Clear");
                AddCommand("Stop", "signal", index, "Lock");
                if (signal.CanApproach)
                    AddCommand("Approach", "signal", index, "Approach");
                else
                    Row("Approach", "This signal has no approach aspect.");
                AddCommand("Proceed", "signal", index, "Manual");
                if (signal.CallOnEnabled)
                    AddCommand("Call on", "signal", index, "CallOn");
            }
            else if (kind == "switch")
            {
                DispatcherSwitchInfo junction = latest?.Switches?.FirstOrDefault(item => item.NodeIndex == index);
                if (junction == null)
                    return;
                Heading($"Switch {index}");
                Row("Position", junction.Position == 0 ? "Route 0" : "Route 1");
                Row("Command", "Choose a route.");
                AddCommand("Main route", "switch", index, "MainRoute");
                AddCommand("Side route", "switch", index, "SideRoute");
                Row("Note", "Occupied or reserved switches cannot be thrown.");
            }
            else
            {
                Heading("Dispatcher");
                DispatcherTrainInfo player = latest?.Trains?.FirstOrDefault(train =>
                    string.Equals(train.TrainType, "Player", StringComparison.OrdinalIgnoreCase));
                if (player != null)
                {
                    Row("Your train", $"#{player.Number} · {player.Locomotive} · {player.Name}");
                    Button locate = new Button { Content = "Center on my train" };
                    locate.Click += (_, _) => map.CenterAt(player.Front);
                    details.Children.Add(locate);
                }
                Row("Map", "Select a train, signal, station or switch.");
                Row("Navigation", "Drag to pan; use the wheel or +/− to zoom.");
                Row("Trains", (latest?.Trains?.Length ?? 0).ToString(CultureInfo.CurrentCulture));
                Row("Signals", (latest?.Signals?.Length ?? 0).ToString(CultureInfo.CurrentCulture));
            }
        }

        private void Heading(string value) => details.Children.Add(new TextBlock
        {
            Text = value, FontSize = 17, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap,
        });

        private void Row(string label, string value)
        {
            TextBlock row = new TextBlock
            {
                Text = label + ": " + value, FontSize = 13, TextWrapping = TextWrapping.Wrap,
            };
            detailRows[label] = row;
            details.Children.Add(row);
        }

        private void SetRow(string label, string value)
        {
            if (detailRows.TryGetValue(label, out TextBlock row))
                row.Text = label + ": " + value;
        }

        private void UpdateLiveDetails()
        {
            int index = map.SelectedIndex;
            if (map.SelectedKind == "train")
            {
                DispatcherTrainInfo train = latest.Trains?.FirstOrDefault(item => item.Number == index);
                if (train == null)
                {
                    UpdateSelection();
                    return;
                }
                SetRow("Service", train.Name);
                SetRow("Locomotive", string.IsNullOrWhiteSpace(train.Locomotive) ? "—" : train.Locomotive);
                SetRow("Next station", train.NextStation);
                if (train.NextArrival.HasValue)
                    SetRow("Arrival", TimeSpan.FromSeconds((train.NextArrival.Value % 86400 + 86400) % 86400).ToString(@"hh\:mm", CultureInfo.CurrentCulture));
                if (train.NextDeparture.HasValue)
                    SetRow("Departure", TimeSpan.FromSeconds((train.NextDeparture.Value % 86400 + 86400) % 86400).ToString(@"hh\:mm", CultureInfo.CurrentCulture));
                SetRow("Control", train.ControlMode);
                SetRow("Speed", $"{Math.Abs(train.SpeedMpS) * 3.6:F1} km/h");
                SetRow("Direction", train.Direction);
                SetRow("Cars", train.CarCount.ToString(CultureInfo.CurrentCulture));
            }
            else if (map.SelectedKind == "signal")
            {
                DispatcherSignalInfo signal = latest.Signals?.FirstOrDefault(item => item.Index == index);
                if (signal == null)
                {
                    UpdateSelection();
                    return;
                }
                SetRow("Aspect", signal.Aspect);
                SetRow("Control", signal.State);
                SetRow("Train", signal.EnabledTrain?.ToString(CultureInfo.CurrentCulture) ?? "—");
            }
            else if (map.SelectedKind == "switch")
            {
                DispatcherSwitchInfo junction = latest.Switches?.FirstOrDefault(item => item.NodeIndex == index);
                if (junction == null)
                {
                    UpdateSelection();
                    return;
                }
                SetRow("Position", junction.Position == 0 ? "Route 0" : "Route 1");
            }
            else if (map.SelectedKind != "point")
            {
                DispatcherTrainInfo player = latest.Trains?.FirstOrDefault(train =>
                    string.Equals(train.TrainType, "Player", StringComparison.OrdinalIgnoreCase));
                if (player != null && !detailRows.ContainsKey("Your train"))
                {
                    UpdateSelection();
                    return;
                }
                if (player != null)
                    SetRow("Your train", $"#{player.Number} · {player.Locomotive} · {player.Name}");
                SetRow("Trains", (latest.Trains?.Length ?? 0).ToString(CultureInfo.CurrentCulture));
                SetRow("Signals", (latest.Signals?.Length ?? 0).ToString(CultureInfo.CurrentCulture));
            }
        }

        private void AddCommand(string label, string kind, int index, string state)
        {
            Button button = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Stretch };
            button.Click += async (_, _) =>
            {
                button.IsEnabled = false;
                try
                {
                    // StringContent sends an explicit Content-Length. EmbedIO's HttpListener
                    // can otherwise receive an empty chunked JSON body on some runtimes.
                    using var body = new StringContent(
                        JsonSerializer.Serialize(new { Kind = kind, Index = index, State = state }),
                        Encoding.UTF8, "application/json");
                    using HttpResponseMessage response = await client.PostAsync("API/DISPATCHER/COMMAND", body);
                    response.EnsureSuccessStatusCode();
                    DispatcherCommandResult result = await response.Content.ReadFromJsonAsync<DispatcherCommandResult>(JsonOptions);
                    SetRow("Command", result?.Message ?? "No response from simulator.");
                    if (result?.Accepted == true)
                        await Refresh();
                }
                catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException)
                {
                    SetRow("Command", "Could not send command to simulator.");
                }
                finally
                {
                    button.IsEnabled = true;
                }
            };
            details.Children.Add(button);
        }

        private sealed class DispatcherCommandResult
        {
            public bool Accepted { get; set; }
            public string Message { get; set; }
        }

        private sealed class DispatcherMapControl : Control
        {
            private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#11151b"));
            private static readonly IBrush TrackBrush = new SolidColorBrush(Color.Parse("#7d8794"));
            private static readonly IBrush NamedPointBrush = new SolidColorBrush(Color.Parse("#aab5c3"));
            private static readonly IBrush TrainBrush = new SolidColorBrush(Color.Parse("#47a7ff"));
            private static readonly IBrush PlayerBrush = new SolidColorBrush(Color.Parse("#ffd54a"));
            private static readonly IBrush SignalStopBrush = new SolidColorBrush(Color.Parse("#e85151"));
            private static readonly IBrush SwitchMainBrush = new SolidColorBrush(Color.Parse("#4fc778"));
            private static readonly IBrush SwitchSideBrush = new SolidColorBrush(Color.Parse("#ff9f43"));
            private static readonly IBrush LabelBrush = new SolidColorBrush(Color.Parse("#e6edf5"));
            private static readonly Pen TrackPen = new Pen(TrackBrush, 1.25);
            private static readonly Pen TrainPen = new Pen(TrainBrush, 3.5);
            private static readonly Pen PlayerPen = new Pen(PlayerBrush, 4.5);

            private MapInit map;
            private DispatcherSnapshot snapshot;
            private double zoom = 1;
            private Vector pan;
            private bool panning;
            private Point lastPointer;
            private Point pressPointer;
            public event Action SelectionChanged;
            public string SelectedKind { get; private set; }
            public int SelectedIndex { get; private set; } = -1;
            public MapPoint SelectedPoint => SelectedKind == "point" && SelectedIndex >= 0 &&
                SelectedIndex < (map?.PointOnApiMapList?.Count ?? 0)
                ? map.PointOnApiMapList[SelectedIndex] : null;
            private double viewLatMin;
            private double viewLatMax;
            private double viewLonMin;
            private double viewLonMax;

            public DispatcherMapControl()
            {
                ClipToBounds = true;
                PointerPressed += OnPointerPressed;
                PointerMoved += OnPointerMoved;
                PointerReleased += OnPointerReleased;
                PointerCaptureLost += (_, _) => panning = false;
                PointerWheelChanged += OnPointerWheelChanged;
            }

            public void SetMap(MapInit value)
            {
                map = value;
                RecalculateViewBounds();
                Fit();
            }

            public void SetSnapshot(DispatcherSnapshot value)
            {
                snapshot = value;
                InvalidateVisual();
            }

            public void ZoomAtCenter(double factor)
            {
                zoom = Math.Clamp(zoom * factor, 0.2, 1000);
                InvalidateVisual();
            }

            public void CenterAt(LatLon point)
            {
                pan -= Project(point) - new Point(Bounds.Width * 0.5, Bounds.Height * 0.5);
                InvalidateVisual();
            }

            public void Fit()
            {
                RecalculateViewBounds();
                zoom = 1;
                pan = default;
                InvalidateVisual();
            }

            private void RecalculateViewBounds()
            {
                viewLatMin = double.PositiveInfinity;
                viewLatMax = double.NegativeInfinity;
                viewLonMin = double.PositiveInfinity;
                viewLonMax = double.NegativeInfinity;

                // Use the actual track polyline as the authoritative viewport extent. Track-item
                // metadata may legitimately contain placeholder locations and must never make the
                // real route microscopic after pressing "Fit map".
                if (map?.LineOnApiMapList != null)
                {
                    foreach (MapLine line in map.LineOnApiMapList)
                    {
                        IncludeInView(line.LatLonFrom);
                        IncludeInView(line.LatLonTo);
                    }
                }

                // Very small/test routes may contain no line segments. Fall back to valid map points.
                if (!HasValidViewBounds() && map?.PointOnApiMapList != null)
                {
                    foreach (MapPoint point in map.PointOnApiMapList)
                        IncludeInView(point.LatLon);
                }

                // Last-resort compatibility with older API responses.
                if (!HasValidViewBounds() && map != null &&
                    double.IsFinite(map.LatMin) && double.IsFinite(map.LatMax) &&
                    double.IsFinite(map.LonMin) && double.IsFinite(map.LonMax))
                {
                    viewLatMin = map.LatMin;
                    viewLatMax = map.LatMax;
                    viewLonMin = map.LonMin;
                    viewLonMax = map.LonMax;
                }
            }

            private void IncludeInView(LatLon point)
            {
                if (!float.IsFinite(point.Lat) || !float.IsFinite(point.Lon) ||
                    point.Lat < -90 || point.Lat > 90 || point.Lon < -180 || point.Lon > 180)
                    return;

                viewLatMin = Math.Min(viewLatMin, point.Lat);
                viewLatMax = Math.Max(viewLatMax, point.Lat);
                viewLonMin = Math.Min(viewLonMin, point.Lon);
                viewLonMax = Math.Max(viewLonMax, point.Lon);
            }

            private bool HasValidViewBounds()
                => double.IsFinite(viewLatMin) && double.IsFinite(viewLatMax) &&
                   double.IsFinite(viewLonMin) && double.IsFinite(viewLonMax) &&
                   viewLatMax >= viewLatMin && viewLonMax >= viewLonMin;

            public override void Render(DrawingContext context)
            {
                base.Render(context);
                context.FillRectangle(BackgroundBrush, Bounds);

                if (map == null || Bounds.Width < 2 || Bounds.Height < 2)
                    return;

                if (map.LineOnApiMapList != null)
                {
                    foreach (MapLine line in map.LineOnApiMapList)
                        context.DrawLine(TrackPen, Project(line.LatLonFrom), Project(line.LatLonTo));
                }

                var labels = new List<Rect>();
                DispatcherTrainInfo playerTrain = snapshot?.Trains?.FirstOrDefault(train =>
                    string.Equals(train.TrainType, "Player", StringComparison.OrdinalIgnoreCase));
                if (playerTrain != null)
                {
                    string identity = $"YOUR TRAIN  #{playerTrain.Number}  ·  {playerTrain.Locomotive}  ·  {playerTrain.Name}";
                    context.FillRectangle(BackgroundBrush, new Rect(5, 5, Math.Min(Bounds.Width - 10, 680), 35));
                    TryLabel(context, identity, new Point(13, 11), 15, PlayerBrush, labels);
                }
                if (map.PointOnApiMapList != null && zoom >= 3)
                {
                    foreach (MapPoint point in map.PointOnApiMapList)
                    {
                        if (point.TypeOfPointOnApiMap != 1 || string.IsNullOrWhiteSpace(point.Name))
                            continue;
                        Point p = Project(point.LatLon);
                        if (!Visible(p))
                            continue;
                        context.DrawEllipse(NamedPointBrush,
                            SelectedKind == "point" && SelectedPoint == point ? new Pen(LabelBrush, 2) : null,
                            p, 3.5, 3.5);
                        string locationLabel = (point.Category == "Station" ? "Station: " : "") + point.Name;
                        if (zoom >= 18 && !string.IsNullOrWhiteSpace(point.Detail) && point.Detail != point.Name)
                            locationLabel += " · " + point.Detail;
                        TryLabel(context, locationLabel, p + new Vector(6, -11), 13, NamedPointBrush, labels);
                    }
                }

                if (snapshot?.Switches != null)
                {
                    foreach (DispatcherSwitchInfo junction in snapshot.Switches)
                    {
                        Point p = Project(junction.Location);
                        IBrush brush = junction.Position == 0 ? SwitchMainBrush : SwitchSideBrush;
                        if (Visible(p))
                        {
                            context.DrawEllipse(brush, SelectedKind == "switch" && SelectedIndex == junction.NodeIndex ? new Pen(LabelBrush, 2) : null, p, 4, 4);
                            if (zoom >= 22)
                                TryLabel(context, $"Switch {junction.NodeIndex} · Route {junction.Position}", p + new Vector(7, -15), 12, brush, labels);
                        }
                    }
                }

                if (snapshot?.Signals != null && zoom >= 2)
                {
                    foreach (DispatcherSignalInfo signal in snapshot.Signals)
                    {
                        Point p = Project(signal.Location);
                        if (!Visible(p))
                            continue;
                        IBrush aspect = signal.Aspect.Contains("Stop", StringComparison.OrdinalIgnoreCase) ? SignalStopBrush
                            : signal.Aspect.Contains("Approach", StringComparison.OrdinalIgnoreCase) ? SwitchSideBrush
                            : SwitchMainBrush;
                        context.DrawEllipse(aspect, SelectedKind == "signal" && SelectedIndex == signal.Index ? new Pen(LabelBrush, 2) : null, p, 4, 4);
                        if (zoom >= 22)
                            TryLabel(context, $"Signal {signal.Index} · {signal.Aspect}", p + new Vector(7, 4), 12, aspect, labels);
                    }
                }

                if (snapshot?.Trains == null)
                    return;

                foreach (DispatcherTrainInfo train in snapshot.Trains)
                {
                    Point front = Project(train.Front);
                    Point rear = Project(train.Rear);
                    bool player = string.Equals(train.TrainType, "Player", StringComparison.OrdinalIgnoreCase);
                    IBrush brush = player ? PlayerBrush : TrainBrush;
                    context.DrawLine(player ? PlayerPen : TrainPen, rear, front);
                    context.DrawEllipse(brush, SelectedKind == "train" && SelectedIndex == train.Number ? new Pen(LabelBrush, 2) : null,
                        front, player ? 6.5 : 5.5, player ? 6.5 : 5.5);

                    if (!player && zoom < 2 && !(SelectedKind == "train" && SelectedIndex == train.Number))
                        continue;
                    string name = $"#{train.Number} · {train.Locomotive}";
                    if (zoom >= 10 && !string.IsNullOrWhiteSpace(train.Name) && train.Name != train.Locomotive)
                        name += " · " + train.Name;
                    Point anchor = Visible(front) ? front : rear;
                    if (Visible(anchor))
                        TryLabel(context, name, anchor + new Vector(8, -16), player ? 14 : 13, LabelBrush, labels);
                }
            }

            private Point Project(LatLon point)
            {
                double lonRange = Math.Max(1e-8, viewLonMax - viewLonMin);
                double latRange = Math.Max(1e-8, viewLatMax - viewLatMin);
                double width = Math.Max(1, Bounds.Width - 72);
                double height = Math.Max(1, Bounds.Height - 72);
                double scale = Math.Min(width / lonRange, height / latRange) * zoom;
                double centerLon = (viewLonMin + viewLonMax) * 0.5;
                double centerLat = (viewLatMin + viewLatMax) * 0.5;

                return new Point(
                    Bounds.Width * 0.5 + (point.Lon - centerLon) * scale + pan.X,
                    Bounds.Height * 0.5 - (point.Lat - centerLat) * scale + pan.Y);
            }

            private bool Visible(Point p) => p.X >= -8 && p.Y >= -8 && p.X <= Bounds.Width + 8 && p.Y <= Bounds.Height + 8;

            private static void TryLabel(DrawingContext context, string text, Point position, double size, IBrush brush, List<Rect> labels)
            {
                if (string.IsNullOrWhiteSpace(text))
                    return;

                FormattedText formatted = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Sans"),
                    size,
                    brush);
                Rect bounds = new Rect(position, new Size(formatted.Width + 6, formatted.Height + 3));
                if (labels.Any(item => item.Intersects(bounds)))
                    return;
                labels.Add(bounds);
                context.DrawText(formatted, position);
            }

            private void OnPointerPressed(object sender, PointerPressedEventArgs args)
            {
                PointerPoint point = args.GetCurrentPoint(this);
                if (!point.Properties.IsLeftButtonPressed)
                    return;
                panning = true;
                lastPointer = point.Position;
                pressPointer = lastPointer;
                args.Pointer.Capture(this);
                args.Handled = true;
            }

            private void OnPointerMoved(object sender, PointerEventArgs args)
            {
                if (!panning)
                    return;
                Point current = args.GetPosition(this);
                pan += current - lastPointer;
                lastPointer = current;
                InvalidateVisual();
                args.Handled = true;
            }

            private void OnPointerReleased(object sender, PointerReleasedEventArgs args)
            {
                if (!panning)
                    return;
                panning = false;
                if (Distance(args.GetPosition(this), pressPointer) < 5)
                    SelectAt(args.GetPosition(this));
                args.Pointer.Capture(null);
                args.Handled = true;
            }

            private static double Distance(Point a, Point b)
                => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

            private void SelectAt(Point pointer)
            {
                string kind = null;
                int index = -1;
                double best = 13;
                if (snapshot?.Trains != null)
                    foreach (DispatcherTrainInfo train in snapshot.Trains)
                    {
                        double distance = Distance(Project(train.Front), pointer);
                        if (distance < best) { best = distance; kind = "train"; index = train.Number; }
                    }
                if (snapshot?.Signals != null && zoom >= 2)
                    foreach (DispatcherSignalInfo signal in snapshot.Signals)
                    {
                        double distance = Distance(Project(signal.Location), pointer);
                        if (distance < best) { best = distance; kind = "signal"; index = signal.Index; }
                    }
                if (snapshot?.Switches != null)
                    foreach (DispatcherSwitchInfo junction in snapshot.Switches)
                    {
                        double distance = Distance(Project(junction.Location), pointer);
                        if (distance < best) { best = distance; kind = "switch"; index = junction.NodeIndex; }
                    }
                if (map?.PointOnApiMapList != null && zoom >= 3)
                    for (int i = 0; i < map.PointOnApiMapList.Count; i++)
                    {
                        MapPoint point = map.PointOnApiMapList[i];
                        if (point.TypeOfPointOnApiMap != 1)
                            continue;
                        double distance = Distance(Project(point.LatLon), pointer);
                        if (distance < best) { best = distance; kind = "point"; index = i; }
                    }
                SelectedKind = kind;
                SelectedIndex = index;
                SelectionChanged?.Invoke();
                InvalidateVisual();
            }

            private void OnPointerWheelChanged(object sender, PointerWheelEventArgs args)
            {
                if (args.Delta.Y == 0)
                    return;

                Point pointer = args.GetPosition(this);
                Point center = new Point(Bounds.Width * 0.5, Bounds.Height * 0.5);
                double oldZoom = zoom;
                double factor = Math.Pow(1.2, args.Delta.Y);
                double newZoom = Math.Clamp(oldZoom * factor, 0.2, 1000);

                // Keep the geographic point below the mouse cursor fixed while zooming.
                // This makes inspection of junctions/trains much quicker than zooming only
                // toward the center of the window.
                if (Math.Abs(newZoom - oldZoom) > double.Epsilon)
                {
                    Vector pointerFromMapCenter = pointer - center - pan;
                    double ratio = newZoom / oldZoom;
                    pan += pointerFromMapCenter * (1 - ratio);
                    zoom = newZoom;
                    InvalidateVisual();
                }

                args.Handled = true;
            }
        }

        private sealed class MapInit
        {
            public List<MapPoint> PointOnApiMapList { get; set; }
            public List<MapLine> LineOnApiMapList { get; set; }
            public double LatMin { get; set; }
            public double LatMax { get; set; }
            public double LonMin { get; set; }
            public double LonMax { get; set; }
        }

        private sealed class MapPoint
        {
            public LatLon LatLon { get; set; }
            public int TypeOfPointOnApiMap { get; set; }
            public string Name { get; set; }
            public string Category { get; set; }
            public string Detail { get; set; }
        }

        private sealed class MapLine
        {
            public LatLon LatLonFrom { get; set; }
            public LatLon LatLonTo { get; set; }
        }

        private sealed class DispatcherSnapshot
        {
            public double ClockTime { get; set; }
            public DispatcherTrainInfo[] Trains { get; set; } = Array.Empty<DispatcherTrainInfo>();
            public DispatcherSwitchInfo[] Switches { get; set; } = Array.Empty<DispatcherSwitchInfo>();
            public DispatcherSignalInfo[] Signals { get; set; } = Array.Empty<DispatcherSignalInfo>();
        }

        private sealed class DispatcherTrainInfo
        {
            public int Number { get; set; }
            public string Name { get; set; }
            public string TrainType { get; set; }
            public string ControlMode { get; set; }
            public float SpeedMpS { get; set; }
            public int CarCount { get; set; }
            public string Locomotive { get; set; }
            public string NextStation { get; set; }
            public int? NextArrival { get; set; }
            public int? NextDeparture { get; set; }
            public string[] Cars { get; set; }
            public bool IsFreight { get; set; }
            public string Direction { get; set; }
            public LatLon Front { get; set; }
            public LatLon Rear { get; set; }
        }

        private sealed class DispatcherSwitchInfo
        {
            public int NodeIndex { get; set; }
            public int Position { get; set; }
            public LatLon Location { get; set; }
        }

        private sealed class DispatcherSignalInfo
        {
            public int Index { get; set; }
            public string State { get; set; }
            public string Aspect { get; set; } = "";
            public int? EnabledTrain { get; set; }
            public bool CallOnEnabled { get; set; }
            public bool CanApproach { get; set; }
            public LatLon Location { get; set; }
        }

        private struct LatLon
        {
            public float Lat;
            public float Lon;
        }
    }
}
