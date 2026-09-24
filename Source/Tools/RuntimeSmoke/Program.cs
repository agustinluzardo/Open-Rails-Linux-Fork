using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Graphics.Window;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Processes;
using Orts.ActivityRunner.Viewer3D;

// Generate the fixture with Tools/TestRoute/make_test_route.py and configure it through
// `riel content add Test <folder>`. Run with isolated XDG directories and SDL_VIDEODRIVER=offscreen.
ProfileModel profile = ((ProfileModel)null).Current(CancellationToken.None).GetAwaiter().GetResult();
ProfileUserSettingsModel settings = profile.LoadSettingsModel<ProfileUserSettingsModel>(CancellationToken.None).GetAwaiter().GetResult();
settings.ScreenMode = ScreenMode.Windowed;
settings.WindowSettings[WindowSetting.Size] = (800, 480);
settings.MultiSamplingCount = 0;
settings.DynamicShadows = false;
using GameHost game = new GameHost(settings);
Type stateType = typeof(GameHost).Assembly.GetType("Orts.ActivityRunner.Processes.GameStateRunActivity", true);
object state = Activator.CreateInstance(stateType, new object[] { new[] {
    "-SingleplayerNewGame", "-ExploreActivity", "Test", "RIELTEST", "MAIN", "COALHOP", "12:00", "Summer", "Clear" } });
typeof(GameHost).GetMethod("PushState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new[] { state });
string output = args.Length > 0 ? args[0] : "/tmp/riel-smoke";
Directory.CreateDirectory(output);
using SmokeDriver driver = new SmokeDriver(game, output, args.Length > 1 && args[1] == "loading");
game.Components.Add(driver);
game.Run();
if (!driver.QuitRequested)
    throw new InvalidOperationException("The game exited before the smoke test requested it.");
Console.WriteLine("SMOKE: Game.Run returned after clean shutdown.");

sealed class SmokeDriver : DrawableGameComponent
{
    private readonly string output;
    private readonly bool exitDuringLoading;
    private readonly Stopwatch timer = Stopwatch.StartNew();
    private readonly string[] windowNames = { "QuitWindow", "HelpWindow", "ActivityWindow", "TrackMonitorWindow", "DrivingTrainWindow", "TrainOperationsWindow", "NextStationWindow", "CompassWindow" };
    private int index;
    private int frames;
    private FormBase current;
    private object windowManager;
    private Viewer viewer;
    public bool QuitRequested { get; private set; }

    internal SmokeDriver(GameHost game, string output, bool exitDuringLoading) : base(game)
    {
        this.output = output;
        this.exitDuringLoading = exitDuringLoading;
        DrawOrder = int.MaxValue;
    }

    public override void Draw(GameTime gameTime)
    {
        if (QuitRequested)
            return;
        if (timer.Elapsed.TotalSeconds > 60)
            throw new TimeoutException("Smoke test did not finish within 60 seconds.");
        object state = typeof(GameHost).GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Game);
        if (exitDuringLoading && ++frames == 5)
        {
            Console.WriteLine($"SMOKE: exiting during {state?.GetType().Name}.");
            QuitRequested = true;
            Game.Exit();
            return;
        }
        if (state?.GetType().Name != "GameStateViewer3D")
            return;
        viewer ??= (Viewer)state.GetType().GetField("Viewer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state);
        windowManager ??= typeof(Viewer).GetField("windowManager", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(viewer);
        if (++frames % 30 != 0)
            return;
        if (current != null)
        {
            Color[] pixels = new Color[Game.GraphicsDevice.Viewport.Width * Game.GraphicsDevice.Viewport.Height];
            Game.GraphicsDevice.GetBackBufferData(pixels);
            using Texture2D image = new Texture2D(Game.GraphicsDevice, Game.GraphicsDevice.Viewport.Width, Game.GraphicsDevice.Viewport.Height);
            image.SetData(pixels);
            using FileStream file = File.Create(Path.Combine(output, windowNames[index - 1] + ".png"));
            image.SaveAsPng(file, image.Width, image.Height);
            Console.WriteLine($"SMOKE: captured {current.GetType().Name}, bounds {current.Borders}");
            current.Close();
            current = null;
        }
        Type windowEnum = windowManager.GetType().GetGenericArguments()[0];
        if (index == windowNames.Length)
        {
            FormBase quit = (FormBase)windowManager.GetType().GetProperty("Item").GetValue(windowManager, new[] { Enum.Parse(windowEnum, "QuitWindow") });
            quit.Open();
            UserCommandArgs commandArgs = new UserCommandArgs();
            typeof(UserCommandController<UserCommand>).GetMethod("Trigger", BindingFlags.Instance | BindingFlags.NonPublic,
                new[] { typeof(UserCommand), typeof(KeyEventType), typeof(UserCommandArgs), typeof(GameTime) })
                .Invoke(viewer.UserCommandController, new object[] { UserCommand.GameQuit, KeyEventType.KeyPressed, commandArgs, gameTime });
            if (!commandArgs.Handled)
                throw new InvalidOperationException("GameQuit was blocked by the modal window.");
            QuitRequested = true;
            return;
        }
        object key = Enum.Parse(windowEnum, windowNames[index++]);
        current = (FormBase)windowManager.GetType().GetProperty("Item").GetValue(windowManager, new[] { key });
        current.Open();
    }
}
