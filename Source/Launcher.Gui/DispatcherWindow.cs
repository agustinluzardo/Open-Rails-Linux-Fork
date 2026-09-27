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
                Content = "Centrar mapa",
                Margin = new Thickness(12, 8, 8, 8),
                VerticalAlignment = VerticalAlignment.Center,
            };
            fit.Click += (_, _) => map.Fit();

            TextBlock hint = new TextBlock
            {
                Text = "Clic: seleccionar   •   Arrastrar: mover   •   Rueda: zoom",
                Opacity = 0.65,
                Margin = new Thickness(12, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };

            status.Text = "Conectando con Riel…";
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
                    latest = snapshot;
                    map.SetSnapshot(snapshot);
                    UpdateSelection();
                    TimeSpan clock = TimeSpan.FromSeconds((snapshot.ClockTime % 86400 + 86400) % 86400);
                    status.Text = $"{clock:hh\\:mm\\:ss}   •   {snapshot.Trains?.Length ?? 0} trenes   •   Conectado";
                }
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException)
            {
                status.Text = initialized ? "Reconectando con el simulador…" : "Esperando al simulador…";
            }
            finally
            {
                refreshing = false;
            }
        }

        private void UpdateSelection()
        {
            details.Children.Clear();
            string kind = map.SelectedKind;
            int index = map.SelectedIndex;
            if (kind == "train")
            {
                DispatcherTrainInfo train = latest?.Trains?.FirstOrDefault(item => item.Number == index);
                if (train == null)
                    return;
                Heading($"Tren {train.Number}");
                Row("Nombre", train.Name);
                Row("Tipo", train.TrainType + (train.IsFreight ? " · carga" : " · pasajeros"));
                Row("Control", train.ControlMode);
                Row("Velocidad", $"{Math.Abs(train.SpeedMpS) * 3.6:F1} km/h");
                Row("Dirección", train.Direction);
                Row("Vagones", train.CarCount.ToString(CultureInfo.CurrentCulture));
                Button follow = new Button { Content = "Centrar en el tren" };
                follow.Click += (_, _) => map.CenterAt(train.Front);
                details.Children.Add(follow);
            }
            else if (kind == "signal")
            {
                DispatcherSignalInfo signal = latest?.Signals?.FirstOrDefault(item => item.Index == index);
                if (signal == null)
                    return;
                Heading($"Señal {signal.Index}");
                Row("Aspecto", signal.Aspect);
                Row("Control", signal.State);
                Row("Tren", signal.EnabledTrain?.ToString(CultureInfo.CurrentCulture) ?? "—");
                Heading("Cambiar señal");
                AddCommand("Control automático", "signal", index, "Clear");
                AddCommand("Parada", "signal", index, "Lock");
                AddCommand("Precaución", "signal", index, "Approach");
                AddCommand("Vía libre", "signal", index, "Manual");
                if (signal.CallOnEnabled)
                    AddCommand("Autorizar rebase", "signal", index, "CallOn");
            }
            else if (kind == "switch")
            {
                DispatcherSwitchInfo junction = latest?.Switches?.FirstOrDefault(item => item.NodeIndex == index);
                if (junction == null)
                    return;
                Heading($"Cambio de vía {index}");
                Row("Posición", junction.Position == 0 ? "Ruta 0" : "Ruta 1");
                AddCommand("Ruta principal", "switch", index, "MainRoute");
                AddCommand("Ruta desviada", "switch", index, "SideRoute");
                Row("Nota", "Si está ocupado o reservado, el cambio se rechaza.");
            }
            else
            {
                Heading("Dispatcher");
                Row("Mapa", "Seleccioná un tren, una señal o un cambio de vía.");
                Row("Navegación", "Arrastrá para mover; usá la rueda o +/− para acercar.");
                Row("Trenes", (latest?.Trains?.Length ?? 0).ToString(CultureInfo.CurrentCulture));
                Row("Señales", (latest?.Signals?.Length ?? 0).ToString(CultureInfo.CurrentCulture));
            }
        }

        private void Heading(string value) => details.Children.Add(new TextBlock
        {
            Text = value, FontSize = 17, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap,
        });

        private void Row(string label, string value) => details.Children.Add(new TextBlock
        {
            Text = label + ": " + value, FontSize = 13, TextWrapping = TextWrapping.Wrap,
        });

        private void AddCommand(string label, string kind, int index, string state)
        {
            Button button = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Stretch };
            button.Click += async (_, _) =>
            {
                try
                {
                    using HttpResponseMessage response = await client.PostAsJsonAsync("API/DISPATCHER/COMMAND",
                        new { Kind = kind, Index = index, State = state });
                    response.EnsureSuccessStatusCode();
                    DispatcherCommandResult result = await response.Content.ReadFromJsonAsync<DispatcherCommandResult>(JsonOptions);
                    status.Text = result?.Message ?? "Sin respuesta del simulador.";
                    if (result?.Accepted == true)
                        await Refresh();
                }
                catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException)
                {
                    status.Text = "No se pudo enviar la orden al simulador.";
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
                // real route microscopic after pressing "Centrar mapa".
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
                if (map.PointOnApiMapList != null && zoom >= 7)
                {
                    foreach (MapPoint point in map.PointOnApiMapList)
                    {
                        if (point.TypeOfPointOnApiMap != 1 || string.IsNullOrWhiteSpace(point.Name))
                            continue;
                        Point p = Project(point.LatLon);
                        if (!Visible(p))
                            continue;
                        context.DrawEllipse(NamedPointBrush, null, p, 2.5, 2.5);
                        TryLabel(context, point.Name.Split(',')[0], p + new Vector(6, -11), 12, NamedPointBrush, labels);
                    }
                }

                if (snapshot?.Switches != null)
                {
                    foreach (DispatcherSwitchInfo junction in snapshot.Switches)
                    {
                        Point p = Project(junction.Location);
                        IBrush brush = junction.Position == 0 ? SwitchMainBrush : SwitchSideBrush;
                        if (Visible(p))
                            context.DrawEllipse(brush, SelectedKind == "switch" && SelectedIndex == junction.NodeIndex ? new Pen(LabelBrush, 2) : null, p, 4, 4);
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
                    string name = string.IsNullOrWhiteSpace(train.Name) ? $"#{train.Number}" : $"{train.Number} · {train.Name}";
                    TryLabel(context, name, front + new Vector(8, -16), player ? 14 : 13, LabelBrush, labels);
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
                if ((args.GetPosition(this) - pressPointer).Length < 5)
                    SelectAt(args.GetPosition(this));
                args.Pointer.Capture(null);
                args.Handled = true;
            }

            private void SelectAt(Point pointer)
            {
                string kind = null;
                int index = -1;
                double best = 13;
                if (snapshot?.Trains != null)
                    foreach (DispatcherTrainInfo train in snapshot.Trains)
                    {
                        double distance = (Project(train.Front) - pointer).Length;
                        if (distance < best) { best = distance; kind = "train"; index = train.Number; }
                    }
                if (snapshot?.Signals != null && zoom >= 2)
                    foreach (DispatcherSignalInfo signal in snapshot.Signals)
                    {
                        double distance = (Project(signal.Location) - pointer).Length;
                        if (distance < best) { best = distance; kind = "signal"; index = signal.Index; }
                    }
                if (snapshot?.Switches != null)
                    foreach (DispatcherSwitchInfo junction in snapshot.Switches)
                    {
                        double distance = (Project(junction.Location) - pointer).Length;
                        if (distance < best) { best = distance; kind = "switch"; index = junction.NodeIndex; }
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
            public LatLon Location { get; set; }
        }

        private struct LatLon
        {
            public float Lat;
            public float Lon;
        }
    }
}
