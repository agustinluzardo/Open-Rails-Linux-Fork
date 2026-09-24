using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Models.Settings;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D.Dispatcher.PopupWindows;
using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.Dispatcher
{
    /// <summary>
    /// The Linux map shares the running game's graphics device. DesktopGL requires all
    /// graphics devices to be created on the original UI thread; the old standalone
    /// DispatcherWindow tried to create a second one on a worker and aborted the process.
    /// </summary>
    internal sealed class InGameDispatcherMap : IDisposable
    {
        private readonly Viewer viewer;
        private readonly MouseInputGameComponent mouse;
        private readonly WindowManager windows;
        private readonly MapBackground background;
        private DispatcherContent content;
        private ContentArea area;
        private SwitchChangeWindow switchWindow;
        private SignalChangeWindow signalWindow;
        private SignalStateWindow signalStateWindow;
        private TrainInformationWindow trainWindow;
        private long nextTrainUpdate;

        public bool IsOpen => area?.Visible == true;

        public InGameDispatcherMap(Viewer viewer, MouseInputGameComponent mouse, WindowManager windows)
        {
            this.viewer = viewer;
            this.mouse = mouse;
            this.windows = windows;
            background = new MapBackground(viewer.Game);
        }

        public void Toggle()
        {
            if (area == null)
                Initialize();
            else
                SetOpen(!IsOpen);
        }

        private void Initialize()
        {
            try
            {
                ProfileDispatcherSettingsModel settings = viewer.UserSettings.Parent
                    .LoadSettingsModel<ProfileDispatcherSettingsModel>(CancellationToken.None)
                    .GetAwaiter().GetResult();
                content = new XnaMapContentFactory().CreateDispatcherContent(viewer.Game, mouse);
                content.Initialize().GetAwaiter().GetResult();
                content.InitializeItemVisiblity(settings.ContentTypeVisibility);
                area = ((IXnaMapShellHost)content.ShellHost).Component as ContentArea;
                area.ResetSize(viewer.Game.Window.ClientBounds.Size, 60);
                background.DrawOrder = -1;
                viewer.Game.Components.Add(background);
                viewer.Game.Components.Add(area);

                switchWindow = new SwitchChangeWindow(windows, new Point(50, 50));
                switchWindow.Initialize();
                signalWindow = new SignalChangeWindow(windows, new Point(50, 50));
                signalWindow.Initialize();
                signalStateWindow = new SignalStateWindow(windows, new Point(75, 25));
                signalStateWindow.Initialize();
                trainWindow = new TrainInformationWindow(windows, new Point(75, 55));
                trainWindow.Initialize();
                SetOpen(true);
            }
            catch (Exception error)
            {
                System.Diagnostics.Trace.TraceError("Could not open in-game dispatcher map: {0}", error);
                viewer.Simulator.Confirmer.Information("The dispatcher map could not be opened; see the Riel log.");
                Dispose();
            }
        }

        private void SetOpen(bool open)
        {
            area.Enabled = area.Visible = background.Visible = open;
            if (!open)
            {
                switchWindow?.Close();
                signalWindow?.Close();
                signalStateWindow?.Close();
                trainWindow?.Close();
            }
            else
                nextTrainUpdate = 0;
        }

        public void Update()
        {
            if (!IsOpen || Environment.TickCount64 < nextTrainUpdate)
                return;
            nextTrainUpdate = Environment.TickCount64 + 100;

            Simulator simulator = viewer.Simulator;
            HashSet<int> tracked = new HashSet<int>();
            foreach (Train train in simulator.Trains)
            {
                tracked.Add(train.Number);
                if (!content.Trains.TryGetValue(train.Number, out TrainWidget widget))
                {
                    widget = new TrainWidget(train.FrontLocation, train.RearLocation, train);
                    content.Trains.Add(train.Number, widget);
                }
                else
                    widget.UpdatePosition(train.FrontLocation, train.RearLocation);

                HashSet<int> cars = new HashSet<int>();
                foreach (TrainCar car in train.Cars)
                {
                    cars.Add(car.UiD);
                    if (widget.Cars.TryGetValue(car.UiD, out TrainCarWidget carWidget))
                        carWidget.UpdatePosition(car.WorldPosition);
                    else
                        widget.Cars.Add(car.UiD, new TrainCarWidget(car.WorldPosition, car.CarLengthM,
                            car.WagonType == WagonType.Unknown && car.EngineType != EngineType.Unknown
                                ? WagonType.Engine : car.WagonType));
                }
                foreach (int id in widget.Cars.Keys.Where(id => !cars.Contains(id)).ToArray())
                    widget.Cars.Remove(id);
            }
            foreach (int id in content.Trains.Keys.Where(id => !tracked.Contains(id)).ToArray())
                content.Trains.Remove(id);
            if (simulator.Trains.Count > 0)
                content.UpdateTrainPath(simulator.Trains[0].FrontTrackTraveller);
        }

        public void Scroll(UserCommandArgs args, KeyModifiers modifiers)
        {
            if (!IsOpen || args.Handled)
                return;
            area.MouseWheelAt(args, modifiers);
            args.Handled = true;
        }

        public void Drag(UserCommandArgs args)
        {
            if (!IsOpen || args.Handled)
                return;
            area.MouseDragging(args);
            args.Handled = true;
        }

        public void Select(UserCommandArgs args)
        {
            if (!IsOpen || args.Handled)
                return;
            if (content.SignalSelected != null)
            {
                signalStateWindow.UpdateSignal(content.SignalSelected);
                signalStateWindow.Open();
            }
            else if (content.TrainSelected != null)
            {
                trainWindow.UpdateTrain(content.TrainSelected);
                trainWindow.Open();
            }
            args.Handled = true;
        }

        public void Change(UserCommandArgs args)
        {
            if (!IsOpen || args.Handled || args is not PointerCommandArgs pointer)
                return;
            if (content.SignalSelected != null)
                signalWindow.OpenAt(pointer.Position, content.SignalSelected);
            else if (content.SwitchSelected != null)
                switchWindow.OpenAt(pointer.Position, content.SwitchSelected);
            args.Handled = true;
        }

        public void Dispose()
        {
            if (area != null)
            {
                SetOpen(false);
                viewer.Game.Components.Remove(area);
                area.Dispose();
                area = null;
            }
            viewer.Game.Components.Remove(background);
            background.Dispose();
            switchWindow?.Dispose();
            signalWindow?.Dispose();
            signalStateWindow?.Dispose();
            trainWindow?.Dispose();
        }

        private sealed class MapBackground : DrawableGameComponent
        {
            public MapBackground(Game game) : base(game) { Visible = false; Enabled = false; }
            public override void Draw(GameTime gameTime)
            {
                GraphicsDevice.Clear(Color.Cornsilk);
                base.Draw(gameTime);
            }
        }
    }
}
