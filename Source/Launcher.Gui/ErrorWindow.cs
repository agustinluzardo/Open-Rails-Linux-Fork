// COPYRIGHT 2026 by the Riel project.
//
// This file is part of Riel, a fork of Open Rails.
//
// Riel is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Riel is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Riel.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;

using FreeTrainSimulator.Common.Info;

using static Riel.Launcher.Gui.Translation;

namespace Riel.Launcher.Gui
{
    /// <summary>
    /// Why the simulator stopped: the cause in one sentence, what usually helps, and everything
    /// else a bug report would need.
    /// </summary>
    /// <remarks>
    /// Started from a desktop icon, the simulator's own error output goes nowhere, and a failure
    /// looks exactly like a click that did nothing. This is where it goes instead.
    /// </remarks>
    /// <remarks>
    /// Closing it with a <see cref="SafeMode"/> asks for the same run again, leaving that part out;
    /// closing it any other way asks for nothing.
    /// </remarks>
    public sealed class ErrorWindow : Window
    {
        private readonly SimulatorOutcome outcome;
        private readonly string details;

        /// <param name="routeProblem">
        /// What the content scan recorded against the route that was started, if anything: often
        /// the actual reason, which the simulator itself only sees as data that is not there.
        /// </param>
        /// <param name="safeMode">What the failed run already left out.</param>
        public ErrorWindow(SimulatorOutcome outcome, string commandLine, string routeProblem = null, SafeMode safeMode = SafeMode.None)
        {
            this.outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
            details = Details(outcome, commandLine, safeMode);
            bool crashed = outcome.CrashedNatively;

            Title = T("The simulator stopped");
            Width = 720;
            Height = crashed ? 600 : 520;
            MinWidth = 520;
            MinHeight = 360;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;

            TextBlock heading = new TextBlock
            {
                Text = crashed ? T("The simulator crashed") : T("The simulator stopped because of an error"),
                TextWrapping = TextWrapping.Wrap,
            };
            heading.Classes.Add("heading");

            SelectableTextBlock cause = new SelectableTextBlock
            {
                Text = crashed ? CrashSummary(outcome) : outcome.Cause ?? F("It exited with code {0} without saying why.", outcome.ExitCode),
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeight.SemiBold,
            };

            SelectableTextBlock location = new SelectableTextBlock
            {
                Text = crashed && outcome.Crash != null
                    ? F("It happened in {0}, called from {1}.", outcome.Crash.Module ?? T("managed code"), outcome.Crash.Caller ?? T("code that could not be named"))
                    : null,
                TextWrapping = TextWrapping.Wrap,
                IsVisible = crashed && outcome.Crash != null,
            };

            TextBlock hint = new TextBlock
            {
                Text = crashed ? CrashHint(outcome.Suspect) : Hint(outcome.CauseType, outcome.Cause),
                TextWrapping = TextWrapping.Wrap,
            };
            hint.Classes.Add("secondary");

            SelectableTextBlock scanned = new SelectableTextBlock
            {
                Text = routeProblem == null ? null : F("When the content was scanned, this route reported: {0}", routeProblem),
                TextWrapping = TextWrapping.Wrap,
                IsVisible = routeProblem != null,
            };

            TextBox detailBox = new TextBox
            {
                Text = details,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
            };
            detailBox.Classes.Add("mono");

            TextBlock logLine = new TextBlock
            {
                Text = outcome.LogFile != null
                    ? F("Full log: {0}", outcome.LogFile)
                    : F("No log was written; the simulator stopped before it got that far. Logs are kept in {0}", RuntimeInfo.LogFilesFolder),
                TextWrapping = TextWrapping.Wrap,
            };
            logLine.Classes.Add("secondary");
            logLine.Classes.Add("small");

            Button copy = new Button { Content = T("Copy details") };
            Button openLog = new Button { Content = T("Open the log"), IsEnabled = outcome.LogFile != null };
            Button close = new Button { Content = T("Close"), IsDefault = true, IsCancel = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            copy.Click += async (_, _) =>
            {
                if (Clipboard != null)
                    await Clipboard.SetTextAsync(details);
            };
            openLog.Click += (_, _) => Open(outcome.LogFile);
            close.Click += (_, _) => Close();

            StackPanel top = new StackPanel { Spacing = 10, Margin = new Thickness(0, 0, 0, 12) };
            top.Children.Add(heading);
            top.Children.Add(cause);
            top.Children.Add(location);
            top.Children.Add(scanned);
            top.Children.Add(hint);
            DockPanel.SetDock(top, Dock.Top);

            StackPanel bottom = new StackPanel { Spacing = 10, Margin = new Thickness(0, 12, 0, 0) };

            // After a crash, the way forward is running again with a part left out: offered here
            // so it is one click, and without the part the last run already went without.
            if (crashed)
            {
                StackPanel retries = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                if (!safeMode.HasFlag(SafeMode.NoSound))
                {
                    Button noSound = new Button { Content = T("Try without sound") };
                    noSound.Click += (_, _) => Close(SafeMode.NoSound);
                    retries.Children.Add(noSound);
                }
                if (!safeMode.HasFlag(SafeMode.BasicGraphics))
                {
                    Button basicGraphics = new Button { Content = T("Try with basic graphics") };
                    basicGraphics.Click += (_, _) => Close(SafeMode.BasicGraphics);
                    retries.Children.Add(basicGraphics);
                }
                if (safeMode != SafeMode.None && safeMode != (SafeMode.NoSound | SafeMode.BasicGraphics))
                {
                    Button both = new Button { Content = T("Try without sound and with basic graphics") };
                    both.Click += (_, _) => Close(SafeMode.NoSound | SafeMode.BasicGraphics);
                    retries.Children.Add(both);
                }
                bottom.Children.Add(retries);
            }

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(copy);
            buttons.Children.Add(openLog);
            buttons.Children.Add(close);
            bottom.Children.Add(logLine);
            bottom.Children.Add(buttons);
            DockPanel.SetDock(bottom, Dock.Bottom);

            DockPanel root = new DockPanel { Margin = new Thickness(24, 20) };
            root.Children.Add(top);
            root.Children.Add(bottom);
            root.Children.Add(detailBox);
            Content = root;
        }

        /// <summary>What crashed and when, in one sentence.</summary>
        private static string CrashSummary(SimulatorOutcome outcome)
        {
            string stage = outcome.Stage switch
            {
                StartupStage.Started => T("while starting"),
                StartupStage.OpeningWindow => T("while opening its window"),
                StartupStage.CreatingGraphicsDevice => T("while setting up the graphics card"),
                StartupStage.GraphicsDeviceReady => T("while showing its window for the first time"),
                StartupStage.FirstFrame => T("just after showing its window"),
                StartupStage.StartingSound => T("while opening the sound device"),
                StartupStage.SoundReady or StartupStage.Loading => T("while loading the route"),
                StartupStage.Running => T("while running"),
                _ => null,
            };
            return stage == null
                ? F("It was stopped by a crash in native code ({0}).", outcome.SignalName)
                : F("It was stopped by a crash in native code ({0}) {1}.", outcome.SignalName, stage);
        }

        /// <summary>What to try after a native crash, aimed at the part that most likely caused it.</summary>
        private static string CrashHint(CrashSuspect suspect)
        {
            return suspect switch
            {
                CrashSuspect.Graphics => T("That points at the graphics driver. Try with basic graphics: if it works, the driver has trouble with full screen, antialiasing or shadows. Copy details and include them in a bug report; they name the exact place."),
                CrashSuspect.Sound => T("That points at the sound system. Try without sound: if it works, the problem is between OpenAL and the sound server. Copy details and include them in a bug report; they name the exact place."),
                CrashSuspect.Window => T("That points at SDL or the window system. Try with basic graphics, which starts in a window rather than full screen. Copy details and include them in a bug report; they name the exact place."),
                _ => T("A crash like this happens in a driver or a system library, not in the route. Try without sound and with basic graphics: the one that works points at the part that fails. Copy details and include them in a bug report."),
            };
        }

        /// <summary>What usually helps, for the failures that have a usual answer.</summary>
        private static string Hint(string causeType, string cause)
        {
            switch (causeType)
            {
                case "FileNotFoundException":
                case "DirectoryNotFoundException":
                    return T("A file the route or the train needs is missing. Routes often borrow objects or rolling stock from other content packs; the missing file is named above, and installing the pack it belongs to fixes it.");
                case "UnauthorizedAccessException":
                    return T("A file could not be read because of its permissions. If the content is on a disk shared with Windows, check that your user can read it.");
                case "NoSuitableGraphicsDeviceException":
                    return T("The graphics card could not be set up. Check that its driver is installed; \"glxinfo | grep renderer\" should name the card rather than llvmpipe.");
                case "DllNotFoundException":
                    return T("A system library is missing. \"Check this computer\" in the main window lists which one.");
                case "InvalidCommandLineException":
                    return T("The launcher and the simulator disagreed about what to start. Choosing the route again usually fixes it.");
                case "RouteDataUnavailableException":
                    return T("The content scan could not read this route's track data, so it cannot be driven. Content folders, then Scan again, lists which file is missing or broken.");
                case "OutOfMemoryException":
                    return T("The computer ran out of memory. Closing other programs, or lowering the viewing distance, may help.");
            }

            if (cause != null && cause.Contains("graphics device", StringComparison.OrdinalIgnoreCase))
                return T("The graphics card could not be set up. Check that its driver is installed; \"glxinfo | grep renderer\" should name the card rather than llvmpipe.");

            return T("The details below and the log say more. If it looks like a bug, reporting it with the log attached helps get it fixed.");
        }

        /// <summary>
        /// Everything a bug report needs, in English whatever the interface language: it is read
        /// by whoever fixes the bug, not by the person who hit it.
        /// </summary>
        private static string Details(SimulatorOutcome outcome, string commandLine, SafeMode safeMode)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine($"{RuntimeInfo.ProductName} {VersionInfo.Version}");
            text.AppendLine(outcome.Signal == null
                ? $"Exit code: {outcome.ExitCode}"
                : $"Exit code: {outcome.ExitCode} ({outcome.SignalName})");
            if (!string.IsNullOrEmpty(commandLine))
                text.AppendLine($"Command:   {commandLine}");
            if (safeMode != SafeMode.None)
                text.AppendLine($"Safe mode: {safeMode}");
            if (outcome.LogFile != null)
                text.AppendLine($"Log:       {outcome.LogFile}");

            if (outcome.Trail.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Startup:");
                foreach (StartupStep step in outcome.Trail)
                {
                    text.Append(CultureInfo.InvariantCulture, $"  {step.Elapsed.TotalMilliseconds,7:0} ms  {step.Stage}");
                    text.AppendLine(step.Detail == null ? string.Empty : "  " + step.Detail);
                }
            }

            if (outcome.Crash != null)
            {
                text.AppendLine();
                text.AppendLine($"Crashed in {outcome.Crash.Module ?? "managed code"}, called from {outcome.Crash.Caller ?? "unknown code"}:");
                foreach (string frame in outcome.Crash.Frames.Take(40))
                    text.AppendLine("  " + frame);
                text.AppendLine($"Crash report: {outcome.Crash.File}");
            }

            if (outcome.FatalError != null)
            {
                text.AppendLine();
                text.AppendLine(outcome.FatalError);
            }
            if (outcome.StandardError != null)
            {
                text.AppendLine();
                text.AppendLine("Standard error:");
                text.AppendLine(outcome.StandardError);
            }
            return text.ToString().TrimEnd();
        }

        private static void Open(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
                startInfo.ArgumentList.Add(path);
                using Process process = Process.Start(startInfo);
            }
            catch (Win32Exception)
            {
                // No xdg-open: the path is on screen, which is what matters.
            }
        }
    }
}
