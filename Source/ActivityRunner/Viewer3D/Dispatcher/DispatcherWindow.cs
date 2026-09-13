using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Common.Display;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;

using GetText;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Dispatcher.PopupWindows;
using Orts.Formats.Msts;
using Orts.Simulation;
using Orts.Simulation.Multiplayer;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.Dispatcher
{
    public class DispatcherWindow : Game
    {
        private const int targetFps = 15;

        private readonly GraphicsDeviceManager graphicsDeviceManager;
        private bool syncing;
        private ScreenMode currentScreenMode;
        private DisplayDevice currentDisplay;
        private Point windowPosition;
        private System.Drawing.Size windowSize;

        private readonly ProfileUserSettingsModel userSettings;
        private readonly ProfileDispatcherSettingsModel dispatcherSettings;
        private Color BackgroundColor;

        private Catalog Catalog;

        private readonly Action onClientSizeChanged;

        private SpriteBatch spriteBatch;
        private ContentArea contentArea;
        private DispatcherContent content;
        private CommonDebugInfo debugInfo;

        private UserCommandController<UserCommand> userCommandController;
        private WindowManager<DispatcherWindowType> windowManager;

        private bool followTrain;

        private readonly EnumArray<string, ColorSetting> colorSettings = new EnumArray<string, ColorSetting>(new string[]
        {
            "CornSilk",     // Background
            "DimGray",      // RailTrack
            "BlueViolet",   // RailTrackEnd
            "LightGray",    // RailTrackJunction
            "Firebrick",    // RailTrackCrossing
            "Crimson",      // RailLevelCrossing
            "Olive",        // RoadTrack
            "ForestGreen",  // RoadTrackEnd
            "DeepPink",     // RoadLevelCrossing
            "OrangeRed",    // PathTrack
            "White",        // RoadCarSpawner
            "White",        // SignalItem
            "Firebrick",    // StationItem
            "Navy",         // PlatformItem
            "ForestGreen",  // SidingItem
            "Gold",         // SpeedPostItem
            "Black",        // MilePostItem
            "White",        // HazardItem
            "White",        // PickupItem
            "White",        // SoundRegionItem
            "White",        // LevelCrossingItem
        });

        public DispatcherWindow(ProfileUserSettingsModel userSettings, ProfileDispatcherSettingsModel dispatcherSettings)
        {
            this.userSettings = userSettings ?? throw new ArgumentNullException(nameof(userSettings));
            this.dispatcherSettings = dispatcherSettings ?? throw new ArgumentNullException(nameof(dispatcherSettings));

            currentDisplay = DisplayDevices.At(dispatcherSettings.WindowScreen);
            FontManager.ScalingFactor = WindowManager.DisplayScalingFactor(currentDisplay);
            LoadSettings();

            TargetElapsedTime = TimeSpan.FromMilliseconds(1000 / targetFps);
            IsFixedTimeStep = true;
            graphicsDeviceManager = new GraphicsDeviceManager(this);
            graphicsDeviceManager.PreparingDeviceSettings += GraphicsPreparingDeviceSettings;
            graphicsDeviceManager.PreferMultiSampling = userSettings.MultiSamplingCount > 0;

            IsMouseVisible = true;

            Window.AllowUserResizing = true;

            //Window.ClientSizeChanged += Window_ClientSizeChanged; // not using the GameForm event as it does not raise when Window is moved (ie to another screeen) using keyboard shortcut

            Window.Position = windowPosition;

            SetScreenMode(currentScreenMode);

            Window.ClientSizeChanged += WindowForm_ClientSizeChanged;
            Exiting += DispatcherWindow_Exiting;

            // using reflection to be able to trigger ClientSizeChanged event manually as this is not 
            // reliably raised otherwise with the resize functionality below in SetScreenMode
            MethodInfo m = Window.GetType().GetMethod("OnClientSizeChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            onClientSizeChanged = (Action)Delegate.CreateDelegate(typeof(Action), Window, m);

            LoadLanguage();
            Window.Title = Catalog.GetString("Dispatcher View");
        }

        private async void DispatcherWindow_Exiting(object sender, EventArgs e)
        {
            await SaveSettings().ConfigureAwait(true);
        }

        private void GraphicsPreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            e.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.HiDef;
            e.GraphicsDeviceInformation.PresentationParameters.RenderTargetUsage = RenderTargetUsage.DiscardContents;
            e.GraphicsDeviceInformation.PresentationParameters.DepthStencilFormat = DepthFormat.Depth24Stencil8;
            // Same clamp as the main window: a sample count this machine cannot give kills the device.
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = GraphicsCapabilities.SupportedMultiSampleCount(userSettings.MultiSamplingCount);
        }

        protected override void Initialize()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            InputSettings.Initialize();
            userCommandController = new UserCommandController<UserCommand>();

            KeyboardInputGameComponent keyboardInputGameComponent = new KeyboardInputGameComponent(this);
            Components.Add(keyboardInputGameComponent);
            KeyboardInputHandler<UserCommand> keyboardInput = new KeyboardInputHandler<UserCommand>();
            keyboardInput.Initialize(InputSettings.UserCommands, keyboardInputGameComponent, userCommandController);

            MouseInputGameComponent mouseInputGameComponent = new MouseInputGameComponent(this);
            Components.Add(mouseInputGameComponent);
            MouseInputHandler<UserCommand> mouseInput = new MouseInputHandler<UserCommand>();
            mouseInput.Initialize(mouseInputGameComponent, keyboardInputGameComponent, userCommandController);

            #region popup windows
            windowManager = WindowManager.Initialize<UserCommand, DispatcherWindowType>(this, userCommandController.AddTopLayerController());
            windowManager.SetLazyWindows(DispatcherWindowType.DebugScreen, new Lazy<FormBase>(() =>
            {
                DebugScreen debugWindow = new DebugScreen(windowManager, BackgroundColor);
                debugWindow.SetInformationProvider(DebugScreenInformation.Common, debugInfo);
                return debugWindow;
            }));
            windowManager.SetLazyWindows(DispatcherWindowType.SignalChange, new Lazy<FormBase>(() =>
            {
                return new SignalChangeWindow(windowManager, new Point(50, 50));
            }));
            windowManager.SetLazyWindows(DispatcherWindowType.SwitchChange, new Lazy<FormBase>(() =>
            {
                return new SwitchChangeWindow(windowManager, new Point(50, 50));
            }));
            windowManager.SetLazyWindows(DispatcherWindowType.SignalState, new Lazy<FormBase>(() =>
            {
                return new SignalStateWindow(windowManager, dispatcherSettings.PopupLocations[DispatcherWindowType.SignalState].ToPoint());
            }));
            windowManager.SetLazyWindows(DispatcherWindowType.HelpWindow, new Lazy<FormBase>(() =>
            {
                return new HelpWindow(windowManager, dispatcherSettings.PopupLocations[DispatcherWindowType.HelpWindow].ToPoint());
            }));
            windowManager.SetLazyWindows(DispatcherWindowType.Settings, new Lazy<FormBase>(() =>
            {
                return new SettingsWindow(windowManager, dispatcherSettings, dispatcherSettings.PopupLocations[DispatcherWindowType.Settings].ToPoint());
            }));
            windowManager.SetLazyWindows(DispatcherWindowType.TrainInfo, new Lazy<FormBase>(() =>
            {
                return new TrainInformationWindow(windowManager, dispatcherSettings.PopupLocations[DispatcherWindowType.TrainInfo].ToPoint());
            }));
            Components.Add(windowManager);

            #endregion

            foreach (DispatcherWindowType windowType in EnumExtension.GetValues<DispatcherWindowType>())
            {
                if (dispatcherSettings.PopupStatus[windowType])
                    windowManager[windowType].Open();
            }

            base.Initialize();
        }

        protected override async void LoadContent()
        {
            Simulator simulator = Simulator.Instance;
            base.LoadContent();
            bool useMetricUnits = userSettings.MeasurementUnit == MeasurementUnit.Metric || (userSettings.MeasurementUnit == MeasurementUnit.System && RegionInfo.CurrentRegion.IsMetric) ||
                (userSettings.MeasurementUnit == MeasurementUnit.Route && simulator.RouteModel.MetricUnits);

            ScaleRulerComponent scaleRuler = new ScaleRulerComponent(this, FontManager.Scaled(System.Drawing.FontFamily.GenericSansSerif, System.Drawing.FontStyle.Regular)[14], Color.Black, new Vector2(-20, -55));
            Components.Add(scaleRuler);
            Components.Add(new InsetComponent(this, Color.DarkGray, new Vector2(-10, 30)));

            IMapContentFactory contentFactory = new XnaMapContentFactory();
            content = contentFactory.CreateDispatcherContent(
                this,
                Components.OfType<MouseInputGameComponent>().FirstOrDefault(),
                new XnaMapInsetHost(Components.OfType<InsetComponent>().FirstOrDefault()),
                new XnaMapTextureHelperHost(Components.OfType<TextureContentComponent>()));
            await content.Initialize().ConfigureAwait(true);
            content.InitializeItemVisiblity(dispatcherSettings.ContentTypeVisibility);
            content.UpdateWidgetColorSettings(colorSettings);
            contentArea = ((IXnaMapShellHost)content.ShellHost).Component as ContentArea;
            IMapHostControl hostControl = contentArea;
            hostControl.ResetSize(Window.ClientBounds.Size, 60);
            Components.Add(contentArea);
            hostControl.IsEnabled = true;

            #region usercommandcontroller
            userCommandController.AddEvent(UserCommand.ChangeScreenMode, KeyEventType.KeyPressed, () => SetScreenMode(currentScreenMode.Next()));
            userCommandController.AddEvent(UserCommand.MoveLeft, KeyEventType.KeyDown, hostControl.MoveByKeyLeft);
            userCommandController.AddEvent(UserCommand.MoveRight, KeyEventType.KeyDown, hostControl.MoveByKeyRight);
            userCommandController.AddEvent(UserCommand.MoveUp, KeyEventType.KeyDown, hostControl.MoveByKeyUp);
            userCommandController.AddEvent(UserCommand.MoveDown, KeyEventType.KeyDown, hostControl.MoveByKeyDown);
            userCommandController.AddEvent(UserCommand.ZoomIn, KeyEventType.KeyDown, hostControl.ZoomIn);
            userCommandController.AddEvent(UserCommand.ZoomOut, KeyEventType.KeyDown, hostControl.ZoomOut);
            userCommandController.AddEvent(UserCommand.ResetZoomAndLocation, KeyEventType.KeyPressed, () => { hostControl.ResetZoomAndLocation(Window.ClientBounds.Size, 0); });

            #endregion

            debugInfo = new CommonDebugInfo(contentArea);
            if (windowManager.WindowInitialized(DispatcherWindowType.DebugScreen))
                (windowManager[DispatcherWindowType.DebugScreen] as DebugScreen).SetInformationProvider(DebugScreenInformation.Common, debugInfo);
        }

        protected override void Draw(GameTime gameTime)
        {
            debugInfo?.Update(gameTime);
            GraphicsDevice.Clear(BackgroundColor);
            base.Draw(gameTime);
        }

        protected override void Update(GameTime gameTime)
        {
            IEnumerable<int> trackedTrains = new List<int>();
            foreach (Train train in Simulator.Instance.Trains)
            {
                ((List<int>)trackedTrains).Add(train.Number);
                if (!content.Trains.TryGetValue(train.Number, out TrainWidget trainWidget))
                {
                    trainWidget = new TrainWidget(train.FrontLocation, train.RearLocation, train);
                    foreach (TrainCar car in train.Cars)
                    {
                        trainWidget.Cars.Add(car.UiD, new TrainCarWidget(car.WorldPosition, car.CarLengthM, car.WagonType == WagonType.Unknown ? car.EngineType != EngineType.Unknown ? WagonType.Engine : WagonType.Unknown : car.WagonType));
                    }
                    content.Trains.Add(train.Number, trainWidget);
                }
                else if (train.SpeedMpS != 0)
                {
                    trainWidget.UpdatePosition(train.FrontLocation, train.RearLocation);
                    IEnumerable<int> trackedCars = new List<int>();
                    foreach (TrainCar car in train.Cars)
                    {
                        ((List<int>)trackedCars).Add(car.UiD);
                        if (trainWidget.Cars.TryGetValue(car.UiD, out TrainCarWidget trainCar))
                        {
                            trainCar.UpdatePosition(car.WorldPosition);
                        }
                        else
                        {
                            trainWidget.Cars.Add(car.UiD, new TrainCarWidget(car.WorldPosition, car.CarLengthM, car.WagonType == WagonType.Unknown ? car.EngineType != EngineType.Unknown ? WagonType.Engine : WagonType.Unknown : car.WagonType));
                        }
                    }
                    trackedCars = trainWidget.Cars.Keys.Except(trackedCars);
                    foreach (int carNumber in trackedCars)
                        trainWidget.Cars.Remove(carNumber);
                }
            }
            trackedTrains = content.Trains.Keys.Except(trackedTrains);
            foreach (int trainNumber in trackedTrains)
                content.Trains.Remove(trainNumber);

                Train firstTrain = Simulator.Instance.Trains[0];
                content.UpdateTrainPath(firstTrain.FrontTrackTraveller);
            if (followTrain)
                content.UpdateTrainTrackingPoint(Simulator.Instance.PlayerLocomotive.WorldPosition.WorldLocation);
            base.Update(gameTime);
        }

        #region window size/position handling
        private void WindowForm_LocationChanged(object sender, EventArgs e)
        {
            WindowForm_ClientSizeChanged(sender, e);
        }

        private void WindowForm_ClientSizeChanged(object sender, EventArgs e)
        {
            if (syncing)
                return;
            if (currentScreenMode == ScreenMode.Windowed)
                windowSize = new System.Drawing.Size(Window.ClientBounds.Width, Window.ClientBounds.Height);
            //originally, following code would be in Window.LocationChanged handler, but seems to be more reliable here for MG version 3.7.1
            if (currentScreenMode == ScreenMode.Windowed)
                windowPosition = Window.Position;
            // if (fullscreen) gameWindow is moved to different screen we may need to refit for different screen resolution
            Rectangle bounds = Window.ClientBounds;
            DisplayDevice newDisplay = DisplayDevices.FromBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            if (!newDisplay.Equals(currentDisplay) && currentScreenMode != ScreenMode.Windowed)
            {
                currentDisplay = newDisplay;
                SetScreenMode(currentScreenMode);
                //reset Window position to center on new screen
                windowPosition = new Point(
                    currentDisplay.WorkingArea.Left + (currentDisplay.WorkingArea.Width - windowSize.Width) / 2,
                    currentDisplay.WorkingArea.Top + (currentDisplay.WorkingArea.Height - windowSize.Height) / 2);
            }
        }

        private void LoadSettings()
        {
            windowSize.Width = (int)(currentDisplay.WorkingArea.Width * Math.Abs(dispatcherSettings.WindowSettings[WindowSetting.Size].X) / 100.0);
            windowSize.Height = (int)(currentDisplay.WorkingArea.Height * Math.Abs(dispatcherSettings.WindowSettings[WindowSetting.Size].Y) / 100.0);

            windowPosition = PointExtension.ToPoint(dispatcherSettings.WindowSettings[WindowSetting.Location]);
            windowPosition = new Point(
                currentDisplay.WorkingArea.Left + windowPosition.X * (currentDisplay.WorkingArea.Width - windowSize.Width) / 100,
                currentDisplay.WorkingArea.Top + windowPosition.Y * (currentDisplay.WorkingArea.Height - windowSize.Height) / 100);
            BackgroundColor = ColorExtension.FromName(colorSettings[ColorSetting.Background]);
        }

        private async Task SaveSettings()
        {
            dispatcherSettings.WindowSettings[WindowSetting.Size] = ((int)Math.Round(100.0 * windowSize.Width / currentDisplay.WorkingArea.Width), (int)Math.Round(100.0 * windowSize.Height / currentDisplay.WorkingArea.Height));

            dispatcherSettings.WindowSettings[WindowSetting.Location] = 
                ((int)Math.Max(0, Math.Round(100f * (windowPosition.X - currentDisplay.Bounds.Left) / (currentDisplay.WorkingArea.Width - windowSize.Width))), 
                (int)Math.Max(0, Math.Round(100.0 * (windowPosition.Y - currentDisplay.Bounds.Top) / (currentDisplay.WorkingArea.Height - windowSize.Height))));
            dispatcherSettings.WindowScreen = currentDisplay.Index;

            foreach (DispatcherWindowType windowType in EnumExtension.GetValues<DispatcherWindowType>())
            {
                if (windowManager.WindowInitialized(windowType))
                {
                    dispatcherSettings.PopupLocations[windowType] = windowManager[windowType].RelativeLocation.FromPoint();
                }
                dispatcherSettings.PopupStatus[windowType] = windowManager.WindowOpened(windowType);
            }

            _ = await dispatcherSettings.Parent.UpdateSettingsModel(dispatcherSettings, CancellationToken.None).ConfigureAwait(false);
        }


        private void LoadLanguage()
        {
            // The Windows Forms localizer walked the form's control tree; this window draws its
            // own interface, so only the catalog needs switching.
            CatalogManager.Reset();

            if (!string.IsNullOrEmpty(userSettings.Language))
            {
                try
                {
                    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(userSettings.Language);
                }
                catch (CultureNotFoundException exception)
                {
                    Trace.WriteLine(exception.Message);
                }
            }
            else
            {
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InstalledUICulture;
            }
            Catalog = CatalogManager.Catalog;
        }

        /// <summary>
        /// Applies a windowed, full screen or borderless full screen layout.
        /// </summary>
        /// <remarks>
        /// Every caller runs on the game thread, which is the only one allowed to resize the
        /// window, so no marshalling is needed here.
        /// </remarks>
        private void SetScreenMode(ScreenMode targetMode)
        {
            syncing = true;

            if (graphicsDeviceManager.IsFullScreen)
                graphicsDeviceManager.ToggleFullScreen();

            switch (targetMode)
            {
                case ScreenMode.Windowed:
                    if (targetMode != currentScreenMode)
                        Window.Position = windowPosition;
                    Window.IsBorderless = false;
                    Window.AllowUserResizing = true;
                    graphicsDeviceManager.PreferredBackBufferWidth = windowSize.Width;
                    graphicsDeviceManager.PreferredBackBufferHeight = windowSize.Height;
                    graphicsDeviceManager.ApplyChanges();
                    break;
                case ScreenMode.WindowedFullscreen:
                    graphicsDeviceManager.PreferredBackBufferWidth = currentDisplay.WorkingArea.Width;
                    graphicsDeviceManager.PreferredBackBufferHeight = currentDisplay.WorkingArea.Height;
                    Window.IsBorderless = false;
                    Window.AllowUserResizing = false;
                    Window.Position = new Point(currentDisplay.WorkingArea.Left, currentDisplay.WorkingArea.Top);
                    graphicsDeviceManager.ApplyChanges();
                    break;
                case ScreenMode.BorderlessFullscreen:
                    graphicsDeviceManager.PreferredBackBufferWidth = currentDisplay.Bounds.Width;
                    graphicsDeviceManager.PreferredBackBufferHeight = currentDisplay.Bounds.Height;
                    graphicsDeviceManager.ApplyChanges();
                    Window.IsBorderless = true;
                    Window.Position = new Point(currentDisplay.Bounds.X, currentDisplay.Bounds.Y);
                    graphicsDeviceManager.ApplyChanges();
                    break;
            }

            currentScreenMode = targetMode;
            onClientSizeChanged?.Invoke();
            syncing = false;
        }

        protected override void Dispose(bool disposing)
        {
            Components.Remove(windowManager);
            windowManager.Dispose();
            Components.Remove(contentArea);
            contentArea?.Dispose();
            spriteBatch?.Dispose();
            graphicsDeviceManager?.Dispose();
            base.Dispose(disposing);
        }

        public void Close()
        {
            Exit();
        }

        public void BringToFront()
        {
            // MonoGame has no raise-window call. Toggling borderless makes SDL recreate the
            // window, which brings it forward on every window manager that honours the request.
            try
            {
                bool borderless = Window.IsBorderless;
                Window.IsBorderless = !borderless;
                Window.IsBorderless = borderless;
            }
            catch (ObjectDisposedException)
            { }
        }
        #endregion

        #region Content area user interaction
        public void MouseWheel(UserCommandArgs userCommandArgs, KeyModifiers modifiers)
        {
            if (contentArea is not IMapHostControl hostControl)
                return;

            if ((modifiers & KeyModifiers.Control) == KeyModifiers.Control)
                hostControl.MouseWheel(userCommandArgs, modifiers);
            else
                hostControl.MouseWheelAt(userCommandArgs, modifiers);
        }

        public void MouseDragging(UserCommandArgs userCommandArgs)
        {
            if (contentArea is IMapHostControl hostControl)
                hostControl.MouseDragging(userCommandArgs);
        }

        public void MouseLeftClick(UserCommandArgs userCommandArgs)
        {
            if (content.SignalSelected != null && windowManager.WindowInitialized(DispatcherWindowType.SignalState))
            {
                SignalStateWindow signalstateWindow = windowManager[DispatcherWindowType.SignalState] as SignalStateWindow;
                signalstateWindow.UpdateSignal(content.SignalSelected);
            }
            if (content.Trains != null && windowManager.WindowInitialized(DispatcherWindowType.TrainInfo))
            {
                TrainInformationWindow trainInfoWindow = windowManager[DispatcherWindowType.TrainInfo] as TrainInformationWindow;
                trainInfoWindow.UpdateTrain(content.TrainSelected);
            }

        }

        public void MouseRightClick(UserCommandArgs userCommandArgs)
        {
            if (userCommandArgs is PointerCommandArgs pointerCommandArgs)
            {
                if (content.SignalSelected != null && (MultiPlayerManager.MultiplayerState == MultiplayerState.None))
                {
                    SignalChangeWindow signalstateWindow = windowManager[DispatcherWindowType.SignalChange] as SignalChangeWindow;
                    signalstateWindow.OpenAt(pointerCommandArgs.Position, content.SignalSelected);
                }
                else if (content.SwitchSelected != null && MultiPlayerManager.MultiplayerState == MultiplayerState.None)
                {
                    SwitchChangeWindow switchstateWindow = windowManager[DispatcherWindowType.SwitchChange] as SwitchChangeWindow;
                    switchstateWindow.OpenAt(pointerCommandArgs.Position, content.SwitchSelected);
                }
            }
        }
        #endregion

        private sealed class CommonDebugInfo : DetailInfoBase
        {
            private readonly SmoothedData frameRate = new SmoothedData();
            private readonly ContentArea contentArea;

            private const double fpsLow = targetFps - targetFps / 5.0;
            public CommonDebugInfo(ContentArea contentArea) : base(true)
            {
                this.contentArea = contentArea;
                frameRate.Preset(targetFps);
                this["Version"] = VersionInfo.FullVersion;
            }

            public override void Update(GameTime gameTime)
            {
                this["Time"] = DateTime.Now.ToString(CultureInfo.CurrentCulture);
                this["Scale"] = contentArea == null ? null : $"{contentArea.Scale:F3} (pixel/meter)";
                double elapsedRealTime = gameTime?.ElapsedGameTime.TotalSeconds ?? 1;
                frameRate.Update(elapsedRealTime, 1.0 / elapsedRealTime);
                this["FPS"] = $"{1 / gameTime.ElapsedGameTime.TotalSeconds:0.0} - {frameRate.SmoothedValue:0.0}";
                if (frameRate.SmoothedValue < fpsLow)
                    FormattingOptions["FPS"] = FormatOption.RegularRed;
                else
                    FormattingOptions["FPS"] = null;
            }
        }

    }
}
