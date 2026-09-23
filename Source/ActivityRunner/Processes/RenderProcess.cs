// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team. 

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Diagnostics;
using FreeTrainSimulator.Common.Display;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using Orts.ActivityRunner.Viewer3D;

namespace Orts.ActivityRunner.Processes
{
    internal sealed class RenderProcess : IDisposable
    {
        public const int ShadowMapCountMaximum = 4;

        public Point DisplaySize { get; private set; }

        private readonly Profiler profiler;

        private readonly GameHost game;
        private Viewport viewport;

        private bool syncing;
        private Point windowPosition;
        private System.Drawing.Size windowSize;
        private DisplayDevice currentDisplay;
        private ScreenMode currentScreenMode;
        private bool toggleScreenRequested;

        private readonly Action onClientSizeChanged;

        private readonly GraphicsDeviceManager graphicsDeviceManager;

        private RenderFrame CurrentFrame;   // a frame contains a list of primitives to draw at a specified time
        private RenderFrame NextFrame;      // we prepare the next frame in the background while the current one is rendering,

        public bool IsMouseVisible { get; set; }  // handles cross thread issues by signalling RenderProcess of a change
        public MouseCursor ActualCursor { get; set; } = MouseCursor.Arrow;

        public ref readonly Viewport Viewport => ref viewport;

        // Diagnostic information
        public int[] PrimitiveCount { get; private set; }
        public int[] PrimitivePerFrame { get; private set; }
        public int[] ShadowPrimitiveCount { get; private set; }
        public int[] ShadowPrimitivePerFrame { get; private set; }

        // Dynamic shadow map setup.
        public static int ShadowMapCount { get; private set; } = -1; // number of shadow maps
        public static int[] ShadowMapDistance; // distance of shadow map center from camera
        public static int[] ShadowMapDiameter; // diameter of shadow map
        public static float[] ShadowMapLimit; // diameter of shadow map far edge from camera
        private bool disposedValue;

        internal RenderProcess(GameHost gameHost)
        {
            this.game = gameHost;

            profiler = new Profiler("Render");
            profiler.SetThread();
            Profiler.ProfilingData[ProcessType.Render] = profiler;
            gameHost.Window.Title = RuntimeInfo.ApplicationName;

            LoadSettings();

            if (!string.IsNullOrEmpty(gameHost.UserSettings.Language))
            {
                try
                {
                    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(gameHost.UserSettings.Language);
                }
                catch (CultureNotFoundException) { }
            }

            PrimitiveCount = new int[EnumExtension.GetLength<RenderPrimitiveSequence>()];
            PrimitivePerFrame = new int[EnumExtension.GetLength<RenderPrimitiveSequence>()];

            // Run the game initially at 10FPS fixed-time-step. Do not change this! It affects the loading performance.
            gameHost.IsFixedTimeStep = true;
            gameHost.TargetElapsedTime = TimeSpan.FromMilliseconds(100);
            gameHost.InactiveSleepTime = TimeSpan.FromMilliseconds(100);

            graphicsDeviceManager = new GraphicsDeviceManager(gameHost)
            {
                // Set up the rest of the graphics according to the settings.
                SynchronizeWithVerticalRetrace = gameHost.UserSettings.VerticalSync,
                PreferredBackBufferFormat = SurfaceFormat.Color,
                PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
                PreferMultiSampling = gameHost.UserSettings.MultiSamplingCount > 0,
                IsFullScreen = gameHost.UserSettings.ScreenMode == ScreenMode.BorderlessFullscreen,
            };
            graphicsDeviceManager.PreparingDeviceSettings += GraphicsPreparingDeviceSettings;

            // using reflection to be able to trigger ClientSizeChanged event manually as this is not 
            // reliably raised otherwise with the resize functionality below in SetScreenMode
            MethodInfo m = gameHost.Window.GetType().GetMethod("OnClientSizeChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            onClientSizeChanged = (Action)Delegate.CreateDelegate(typeof(Action), gameHost.Window, m);

            gameHost.Window.ClientSizeChanged += Window_ClientSizeChanged;
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            TrackWindowPlacement();
        }

        /// <summary>
        /// Keeps the remembered size, position and display in step with the window.
        /// </summary>
        /// <remarks>
        /// MonoGame raises an event when the window is resized but not when it is only moved, so
        /// this also runs once per frame. Both paths are cheap: the bounds are already in memory
        /// and the display lookup walks a cached list.
        /// </remarks>
        private void TrackWindowPlacement()
        {
            if (syncing)
                return;

            Rectangle bounds = game.Window.ClientBounds;
            if (currentScreenMode == ScreenMode.Windowed)
            {
                windowSize = new System.Drawing.Size(bounds.Width, bounds.Height);
                windowPosition = game.Window.Position;
            }

            // Dragging a full screen window onto another monitor means refitting to its resolution.
            DisplayDevice newDisplay = DisplayDevices.FromBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            if (!newDisplay.Equals(currentDisplay))
            {
                currentDisplay = newDisplay;
                if (currentScreenMode != ScreenMode.Windowed)
                {
                    SetScreenMode(currentScreenMode);
                    // Recentre on the display it moved to.
                    windowPosition = new Point(
                        currentDisplay.WorkingArea.Left + (currentDisplay.WorkingArea.Width - windowSize.Width) / 2,
                        currentDisplay.WorkingArea.Top + (currentDisplay.WorkingArea.Height - windowSize.Height) / 2);
                }
            }
        }

        private void GraphicsPreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            // This also runs for every later ApplyChanges; only the first time creates the device.
            if (game.GraphicsDevice == null)
                StartupTrail.Mark(StartupStage.CreatingGraphicsDevice);

            GraphicsDeviceInformation information = e.GraphicsDeviceInformation;
            information.GraphicsProfile = GraphicsProfile.HiDef;
            // This stops ResolveBackBuffer() clearing the back buffer.
            information.PresentationParameters.RenderTargetUsage = RenderTargetUsage.PreserveContents;
            information.PresentationParameters.DepthStencilFormat = DepthFormat.Depth24Stencil8;
            information.PresentationParameters.MultiSampleCount = SupportedMultiSampleCount(information, game.UserSettings.MultiSamplingCount);
        }

        /// <summary>
        /// Reduces the requested antialiasing to what this machine will actually give us.
        /// </summary>
        /// <remarks>
        /// Asking for a sample count that cannot be provided does not degrade gracefully: the
        /// device is never created and the game dies before it draws anything, reporting neither
        /// antialiasing nor the setting that caused it. Two separate things have to agree - the
        /// adapter, for the render targets, and the window system, for the back buffer - so both
        /// are asked and the count is halved until they do.
        /// </remarks>
        private static int SupportedMultiSampleCount(GraphicsDeviceInformation information, int requested)
        {
            for (int samples = requested; samples > 1; samples /= 2)
            {
                if (information.Adapter.QueryRenderTargetFormat(information.GraphicsProfile,
                    information.PresentationParameters.BackBufferFormat,
                    information.PresentationParameters.DepthStencilFormat,
                    samples, out _, out _, out int selected) || selected >= samples)
                {
                    int usable = GraphicsCapabilities.SupportedMultiSampleCount(samples);
                    if (usable >= samples)
                        return samples;
                    if (usable > 1)
                    {
                        Trace.TraceWarning($"This display cannot show {requested}x antialiasing; {usable}x is being used instead.");
                        return usable;
                    }
                    break;
                }
            }

            if (requested > 1)
                Trace.TraceWarning($"{requested}x antialiasing is not available on this machine; it has been turned off.");
            return 0;
        }

        internal void Start()
        {
            DisplaySize = game.GraphicsDevice.Viewport.Bounds.Size;

            ShadowMapCount = game.UserSettings.ShadowMapCount;
            if (!game.UserSettings.DynamicShadows || ShadowMapCount < 0)
                ShadowMapCount = 0;
            else if (ShadowMapCount > ShadowMapCountMaximum)
                ShadowMapCount = ShadowMapCountMaximum;
            if (ShadowMapCount < 1)
                game.UserSettings.DynamicShadows = false;

            ShadowMapDistance = new int[ShadowMapCount];
            ShadowMapDiameter = new int[ShadowMapCount];
            ShadowMapLimit = new float[ShadowMapCount];

            ShadowPrimitiveCount = new int[ShadowMapCount];
            ShadowPrimitivePerFrame = new int[ShadowMapCount];

            InitializeShadowMapLocations();

            CurrentFrame = new RenderFrame(game);
            NextFrame = new RenderFrame(game);
        }

        private void InitializeShadowMapLocations()
        {
            float ratio = (float)DisplaySize.X / DisplaySize.Y;
            float fov = MathHelper.ToRadians(game.UserSettings.FieldOfView);
            float n = 0.5f;
            float f = game.UserSettings.ViewingDistance / 2f;

            var m = (float)ShadowMapCount;
            var LastC = n;
            for (var shadowMapIndex = 0; shadowMapIndex < ShadowMapCount; shadowMapIndex++)
            {
                //     Clog  = split distance i using logarithmic splitting
                //         i
                // Cuniform  = split distance i using uniform splitting
                //         i
                //         n = near view plane
                //         f = far view plane
                //         m = number of splits
                //
                //                   i/m
                //     Clog  = n(f/n)
                //         i
                // Cuniform  = n+(f-n)i/m
                //         i

                // Calculate the two Cs and average them to get a good balance.
                var i = (float)(shadowMapIndex + 1);
                var Clog = n * (float)Math.Pow(f / n, i / m);
                var Cuniform = n + (f - n) * i / m;
                var C = (3 * Clog + Cuniform) / 4;

                // This shadow map goes from LastC to C; calculate the correct center and diameter for the sphere from the view frustum.
                var height1 = (float)Math.Tan(fov / 2) * LastC;
                var height2 = (float)Math.Tan(fov / 2) * C;
                var width1 = height1 * ratio;
                var width2 = height2 * ratio;
                var corner1 = new Vector3(height1, width1, LastC);
                var corner2 = new Vector3(height2, width2, C);
                var cornerCenter = (corner1 + corner2) / 2;
                var length = cornerCenter.Length();
                cornerCenter.Normalize();
                var center = length / Vector3.Dot(cornerCenter, Vector3.UnitZ);
                var diameter = 2 * (float)Math.Sqrt(height2 * height2 + width2 * width2 + (C - center) * (C - center));

                ShadowMapDistance[shadowMapIndex] = (int)center;
                ShadowMapDiameter[shadowMapIndex] = (int)diameter;
                ShadowMapLimit[shadowMapIndex] = C;
                LastC = C;
            }
        }

        /// <summary>
        /// Applies the graphics settings, once the game has a window and a graphics device.
        /// </summary>
        /// <remarks>
        /// This runs from the game's own Initialize, which is the earliest point at which the
        /// device exists. Doing any of it in the constructor - reading GraphicsDevice, or calling
        /// ApplyChanges - forces the device to be created before the window is up, which the
        /// OpenGL backend cannot do: there is no context yet for it to load its entry points from,
        /// and it fails inside MonoGame with a null reference rather than a useful message.
        /// </remarks>
        internal void Initialize()
        {
            game.Window.Position = windowPosition;
            SetScreenMode(currentScreenMode);

            RenderPrimitive.SetGraphicsDevice(game.GraphicsDevice);
            FreeTrainSimulator.Common.Info.SystemInfo.SetGraphicAdapterInformation(game.GraphicsDevice.Adapter.Description);

            viewport = game.GraphicsDevice.Viewport;

            // The thread matters too: the context belongs to it, and it should be the main one.
            bool mainThread = FreeTrainSimulator.Common.Native.NativeMethods.GetCurrentWin32ThreadId() == (uint)Environment.ProcessId;
            StartupTrail.Mark(StartupStage.GraphicsDeviceReady,
                $"{game.GraphicsDevice.Adapter.Description}; {DisplayDevices.VideoDriver}; {currentScreenMode} {viewport.Width}x{viewport.Height}, " +
                $"{game.GraphicsDevice.PresentationParameters.MultiSampleCount}x antialiasing; {(mainThread ? "main thread" : "not the main thread")}");
        }

        internal void Update(GameTime gameTime)
        {
            if (IsMouseVisible != game.IsMouseVisible)
                game.IsMouseVisible = IsMouseVisible;

            Mouse.SetCursor(ActualCursor);
            TrackWindowPlacement();

            if (toggleScreenRequested)
            {
                SetScreenMode(currentScreenMode.Next());
                toggleScreenRequested = false;
                viewport = game.GraphicsDevice.Viewport;
            }

            game.UpdaterProcess.WaitForComplection();

            // Swap frames and start the next update (non-threaded updater does the whole update).
            (CurrentFrame, NextFrame) = (NextFrame, CurrentFrame);
            game.UpdaterProcess.TriggerUpdate(NextFrame, gameTime);
            game.SystemProcess.TriggerUpdate(gameTime);
        }

        private void LoadSettings()
        {
            currentScreenMode = game.UserSettings.ScreenMode;
            currentDisplay = DisplayDevices.At(game.UserSettings.WindowScreen);

            windowSize.Width = game.UserSettings.WindowSettings[WindowSetting.Size].X;
            windowSize.Height = game.UserSettings.WindowSettings[WindowSetting.Size].Y;

            // The saved position is a percentage of the free space on the display, so it stays
            // sensible when the resolution changes between sessions.
            windowPosition = game.UserSettings.WindowSettings[WindowSetting.Location].ToPoint();
            windowPosition = new Point(
                currentDisplay.WorkingArea.Left + windowPosition.X * (currentDisplay.WorkingArea.Width - windowSize.Width) / 100,
                currentDisplay.WorkingArea.Top + windowPosition.Y * (currentDisplay.WorkingArea.Height - windowSize.Height) / 100);
        }

        private void SaveSettings()
        {
            /// Settings which should be persisted in the model, need to be configured also in <see cref="FreeTrainSimulator.Models.Shim.ProfileSettingsExtensions.UpdateRuntimeUserSettingsModel"/>
            game.UserSettings.WindowSettings[WindowSetting.Size] = (windowSize.Width, windowSize.Height);
            game.UserSettings.WindowSettings[WindowSetting.Location] = (
                (int)Math.Max(0, Math.Round(100f * (windowPosition.X - currentDisplay.Bounds.Left) / Math.Max(1, currentDisplay.WorkingArea.Width - windowSize.Width))),
                (int)Math.Max(0, Math.Round(100.0 * (windowPosition.Y - currentDisplay.Bounds.Top) / Math.Max(1, currentDisplay.WorkingArea.Height - windowSize.Height))));
            game.UserSettings.WindowScreen = currentDisplay.Index;
        }

        /// <summary>
        /// Applies a windowed, full screen or borderless full screen layout.
        /// </summary>
        /// <remarks>
        /// Only the game thread may resize the window, which is where every caller runs: the
        /// constructor, <see cref="Initialize"/> and <see cref="Update"/>, the last of which is
        /// also how a screen mode key press reaches here.
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
                        game.Window.Position = windowPosition;
                    game.Window.IsBorderless = false;
                    graphicsDeviceManager.PreferredBackBufferWidth = windowSize.Width;
                    graphicsDeviceManager.PreferredBackBufferHeight = windowSize.Height;
                    graphicsDeviceManager.ApplyChanges();
                    break;
                case ScreenMode.WindowedFullscreen:
                    graphicsDeviceManager.PreferredBackBufferWidth = windowSize.Width;
                    graphicsDeviceManager.PreferredBackBufferHeight = windowSize.Height;
                    game.Window.IsBorderless = false;
                    game.Window.Position = new Point(currentDisplay.WorkingArea.Left, currentDisplay.WorkingArea.Top);
                    graphicsDeviceManager.ApplyChanges();
                    if (!graphicsDeviceManager.IsFullScreen)
                        graphicsDeviceManager.ToggleFullScreen();
                    break;
                case ScreenMode.BorderlessFullscreen:
                    graphicsDeviceManager.PreferredBackBufferWidth = currentDisplay.Bounds.Width;
                    graphicsDeviceManager.PreferredBackBufferHeight = currentDisplay.Bounds.Height;
                    graphicsDeviceManager.ApplyChanges();
                    game.Window.IsBorderless = true;
                    game.Window.Position = new Point(currentDisplay.Bounds.X, currentDisplay.Bounds.Y);
                    graphicsDeviceManager.ApplyChanges();
                    break;
            }

            currentScreenMode = targetMode;
            onClientSizeChanged?.Invoke();
            syncing = false;
        }

        internal void BeginDraw()
        {
            if (game.State == null)
                return;

            profiler.Start();

            CurrentFrame.IsScreenChanged = DisplaySize != game.GraphicsDevice.Viewport.Bounds.Size;
            if (CurrentFrame.IsScreenChanged)
            {
                DisplaySize = game.GraphicsDevice.Viewport.Bounds.Size;
                InitializeShadowMapLocations();
            }

            game.State.BeginRender(CurrentFrame);
        }

        internal void Draw(GameTime gameTime)
        {
            try
            {
                CurrentFrame.Draw(gameTime);
            }
            catch (Exception error) when (!Debugger.IsAttached)
            {
                game.ProcessReportError(error);
            }
        }

        internal void EndDraw()
        {
            if (game.State == null)
                return;

            game.State.EndRender(CurrentFrame);

            Array.Copy(PrimitiveCount, PrimitivePerFrame, PrimitiveCount.Length);
            Array.Copy(ShadowPrimitiveCount, ShadowPrimitivePerFrame, ShadowMapCount);

            profiler.Stop();
            (game.SystemInfo[DiagnosticInfo.GpuMetric] as GraphicMetrics).CurrentMetrics = game.GraphicsDevice.Metrics;
        }

        internal void Stop()
        {
            SaveSettings();
        }

        public void ToggleFullScreen()
        {
            toggleScreenRequested = true;
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    graphicsDeviceManager?.Dispose();
                    // TODO: dispose managed state (managed objects)
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

    }
}
