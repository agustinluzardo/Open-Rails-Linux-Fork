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

            Button fit = new Button
            {
                Content = "Centrar mapa",
                Margin = new Thickness(12, 8, 8, 8),
                VerticalAlignment = VerticalAlignment.Center,
            };
            fit.Click += (_, _) => map.Fit();

            TextBlock hint = new TextBlock
            {
                Text = "Arrastrar: mover   •   Rueda: zoom",
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
            root.Children.Add(map);
            Content = root;

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
                    map.SetSnapshot(snapshot);
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

        private sealed class DispatcherMapControl : Control
        {
            private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#11151b"));
            private static readonly IBrush TrackBrush = new SolidColorBrush(Color.Parse("#7d8794"));
            private static readonly IBrush NamedPointBrush = new SolidColorBrush(Color.Parse("#aab5c3"));
            private static readonly IBrush TrainBrush = new SolidColorBrush(Color.Parse("#47a7ff"));
            private static readonly IBrush PlayerBrush = new SolidColorBrush(Color.Parse("#ffd54a"));
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
                Fit();
            }

            public void SetSnapshot(DispatcherSnapshot value)
            {
                snapshot = value;
                InvalidateVisual();
            }

            public void Fit()
            {
                zoom = 1;
                pan = default;
                InvalidateVisual();
            }

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

                if (map.PointOnApiMapList != null && zoom >= 1.5)
                {
                    foreach (MapPoint point in map.PointOnApiMapList)
                    {
                        if (point.TypeOfPointOnApiMap != 1 || string.IsNullOrWhiteSpace(point.Name))
                            continue;
                        Point p = Project(point.LatLon);
                        context.DrawEllipse(NamedPointBrush, null, p, 2.5, 2.5);
                        DrawLabel(context, point.Name.Split(',')[0], p + new Vector(5, -7), 11, NamedPointBrush);
                    }
                }

                if (snapshot?.Switches != null && zoom >= 1.15)
                {
                    foreach (DispatcherSwitchInfo junction in snapshot.Switches)
                    {
                        Point p = Project(junction.Location);
                        IBrush brush = junction.Position == 0 ? SwitchMainBrush : SwitchSideBrush;
                        context.DrawEllipse(brush, null, p, 3.2, 3.2);
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
                    context.DrawEllipse(brush, null, front, player ? 5.5 : 4.5, player ? 5.5 : 4.5);

                    string name = string.IsNullOrWhiteSpace(train.Name) ? $"#{train.Number}" : $"{train.Number} · {train.Name}";
                    DrawLabel(context, name, front + new Vector(7, -10), player ? 13 : 12, LabelBrush);
                }
            }

            private Point Project(LatLon point)
            {
                double lonRange = Math.Max(1e-8, map.LonMax - map.LonMin);
                double latRange = Math.Max(1e-8, map.LatMax - map.LatMin);
                double width = Math.Max(1, Bounds.Width - 48);
                double height = Math.Max(1, Bounds.Height - 48);
                double scale = Math.Min(width / lonRange, height / latRange) * zoom;
                double centerLon = (map.LonMin + map.LonMax) * 0.5;
                double centerLat = (map.LatMin + map.LatMax) * 0.5;

                return new Point(
                    Bounds.Width * 0.5 + (point.Lon - centerLon) * scale + pan.X,
                    Bounds.Height * 0.5 - (point.Lat - centerLat) * scale + pan.Y);
            }

            private static void DrawLabel(DrawingContext context, string text, Point position, double size, IBrush brush)
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
                context.DrawText(formatted, position);
            }

            private void OnPointerPressed(object sender, PointerPressedEventArgs args)
            {
                PointerPoint point = args.GetCurrentPoint(this);
                if (!point.Properties.IsLeftButtonPressed)
                    return;
                panning = true;
                lastPointer = point.Position;
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
                args.Pointer.Capture(null);
                args.Handled = true;
            }

            private void OnPointerWheelChanged(object sender, PointerWheelEventArgs args)
            {
                double factor = args.Delta.Y > 0 ? 1.18 : 1 / 1.18;
                zoom = Math.Clamp(zoom * factor, 0.15, 30);
                InvalidateVisual();
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
        }

        private sealed class DispatcherTrainInfo
        {
            public int Number { get; set; }
            public string Name { get; set; }
            public string TrainType { get; set; }
            public string ControlMode { get; set; }
            public float SpeedMpS { get; set; }
            public LatLon Front { get; set; }
            public LatLon Rear { get; set; }
        }

        private sealed class DispatcherSwitchInfo
        {
            public int NodeIndex { get; set; }
            public int Position { get; set; }
            public LatLon Location { get; set; }
        }

        private struct LatLon
        {
            public float Lat;
            public float Lon;
        }
    }
}
