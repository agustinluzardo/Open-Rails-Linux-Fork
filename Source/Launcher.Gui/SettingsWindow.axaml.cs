// COPYRIGHT 2026 by the Riel project.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Display;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;

using CommonKeyModifiers = FreeTrainSimulator.Common.Input.KeyModifiers;
using AvaloniaKeyModifiers = Avalonia.Input.KeyModifiers;
using XnaKeys = Microsoft.Xna.Framework.Input.Keys;

namespace Riel.Launcher.Gui
{
    public partial class SettingsWindow : Window
    {
        private readonly ProfileModel profile;
        private ProfileKeyboardSettingsModel keyboardSettings;
        private ProfileUserSettingsModel userSettings;
        private Button pendingKeyButton;
        private UserCommand pendingCommand;
        private bool hasPendingCommand;
        private bool settingsLoaded;
        private readonly Dictionary<PropertyInfo, Control> optionControls = new();
        private Screen[] availableScreens = Array.Empty<Screen>();

        public SettingsWindow(ProfileModel profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            InitializeComponent();

            ScreenModeBox.ItemsSource = Enum.GetValues<ScreenMode>();
            BuildOptions();
            DisplayBox.SelectionChanged += (_, _) => UpdateDetectedResolution();
            ScreenModeBox.SelectionChanged += (_, _) => UpdateDetectedResolution();
            AutoResolutionBox.Click += (_, _) => UpdateDetectedResolution();

            SaveButton.Click += SaveButton_Click;
            CancelButton.Click += (_, _) => Close();
            ResetKeyboardButton.Click += (_, _) => ResetKeyboard();

            SetEditingEnabled(false);
            AddHandler(KeyDownEvent, Window_KeyDown, RoutingStrategies.Tunnel);
            Opened += async (_, _) =>
            {
                try
                {
                    await LoadAsync();
                    settingsLoaded = true;
                    SetEditingEnabled(true);
                }
                catch (Exception error)
                {
                    StatusText.Text = $"Could not load settings: {error.Message}";
                }
            };
        }

        private async Task LoadAsync()
        {
            keyboardSettings = await profile.LoadSettingsModel<ProfileKeyboardSettingsModel>(CancellationToken.None);
            userSettings = await profile.LoadSettingsModel<ProfileUserSettingsModel>(CancellationToken.None);

            BuildKeyboardList();
            availableScreens = Screens?.All.ToArray() ?? Array.Empty<Screen>();
            DisplayBox.ItemsSource = availableScreens.Select((screen, index) =>
                $"{Translation.F("Display {0}: {1} × {2}", index + 1, screen.Bounds.Width, screen.Bounds.Height)}" +
                (screen == Screens.Primary ? $" ({Translation.T("Primary")})" : string.Empty)).ToArray();
            int screenIndex = userSettings.WindowScreen;
            if (screenIndex < 0 || screenIndex >= availableScreens.Length)
                screenIndex = Array.IndexOf(availableScreens, Screens?.ScreenFromWindow(this) ?? Screens?.Primary);
            DisplayBox.SelectedIndex = screenIndex >= 0 ? screenIndex : 0;
            ScreenModeBox.SelectedItem = userSettings.ScreenMode;
            AutoResolutionBox.IsChecked = userSettings.UseDesktopResolution;
            WidthBox.Value = userSettings.WindowSettings[WindowSetting.Size].X;
            HeightBox.Value = userSettings.WindowSettings[WindowSetting.Size].Y;
            foreach ((PropertyInfo property, Control control) in optionControls)
            {
                object value = property.GetValue(userSettings);
                switch (control)
                {
                    case CheckBox box: box.IsChecked = (bool)value; break;
                    case NumericUpDown number: number.Value = (int)value; break;
                    case ComboBox choice: choice.SelectedItem = value; break;
                    case TextBox text: text.Text = (string)value; break;
                }
            }
            UpdateDetectedResolution();
        }

        private void UpdateDetectedResolution()
        {
            WidthBox.IsEnabled = HeightBox.IsEnabled = AutoResolutionBox.IsChecked != true;
            if (DisplayBox.SelectedIndex < 0 || DisplayBox.SelectedIndex >= availableScreens.Length)
            {
                DetectedResolutionText.Text = Translation.T("No display detected");
                return;
            }
            Screen screen = availableScreens[DisplayBox.SelectedIndex];
            ScreenMode mode = ScreenModeBox.SelectedItem is ScreenMode selected ? selected : ScreenMode.WindowedFullscreen;
            (int width, int height) = DisplayResolution.SizeFor(mode,
                new System.Drawing.Rectangle(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height),
                new System.Drawing.Rectangle(screen.WorkingArea.X, screen.WorkingArea.Y, screen.WorkingArea.Width, screen.WorkingArea.Height));
            DetectedResolutionText.Text = $"{width} × {height}";
        }

        private void BuildOptions()
        {
            AddGroup(GeneralPanel, "Driving and units");
            AddOptions(GeneralPanel, "CloseLauncherWhilePlaying");
            AddOptions(GeneralPanel, "PressureUnit", "MeasurementUnit", "Alerter", "AlerterExternal", "SpeedControl",
                "Confirmations", "GraduatedRelease", "RetainersOnAllCars", "BrakePipeChargingRate", "PauseAtStart",
                "OdometerShortDistances", "VibrationLevel", "NotificationsTimeout", "TcsScripts", "PerformanceTuner", "PerformanceTunerTarget");

            AddGroup(AudioPanel, "Sound");
            AddOptions(AudioPanel, "SoundVolumePercent", "SoundDetailLevel", "ExternalSoundPassThruPercent");

            AddGroup(VideoPanel, "Rendering");
            AddOptions(VideoPanel, "VerticalSync", "MultiSamplingCount", "DynamicShadows", "ShadowAllShapes", "ModelInstancing",
                "OverheadWireType", "Cab2DStretch", "ViewingDistance", "FarMountainsViewingDistance", "FieldOfView",
                "ExtendedDetailLevelView", "DetailLevelBias", "VisibleDetailLevel", "AmbientBrightness", "ShadowMapBlur",
                "ShadowMapCount", "ShadowMapResolution", "SignalLightGlow");

            AddGroup(SimulationPanel, "Physics and operations");
            AddOptions(SimulationPanel, "AdvancedAdhesion", "AdhesionFilterSize", "AdhesionFactor", "AdhesionFactorChange",
                "WeatherDependentAdhesion", "CouplersBreak", "CurveDependentSpeedLimits", "SimplifiedControls", "SteamHotStart",
                "DieselEngineRun", "ElectricPowerConnected", "ActivityRandomizationLevel", "WeatherRandomizationLevel",
                "ComputerTrainDoors", "SuperElevationLevel", "TrackGauge", "UseLocationPassingPaths", "MstsEnvironment",
                "ForcedRedStationStops", "ValidateBrakingParams");

            AddGroup(DataPanel, "Recording");
            AddOptions(DataPanel, "DataLogger", "DataLogSeparator", "DataLogSpeedUnits", "DataLogStart", "DataLogPerformance",
                "DataLogPhysics", "DataLogMisc", "DataLogSteamPerformance");

            AddGroup(EvaluationPanel, "Trip evaluation");
            AddOptions(EvaluationPanel, "EvaluationTrainSpeed", "EvaluationInterval", "EvaluationStationStops");

            AddGroup(AdvancedPanel, "Network");
            AddOptions(AdvancedPanel, "MultiplayerHost", "MultiplayerPort", "MultiplayerUser", "WebServer", "WebServerPort");
            AddGroup(AdvancedPanel, "Diagnostics and replay");
            AddOptions(AdvancedPanel, "LogLevel", "ErrorDialogEnabled", "ShapeWarnings", "ConfigurationMessages",
                "Profiling", "ProfilingFrameCount", "ProfilingTime", "ProfilingFps", "ReplayPause", "ReplayPauseDuration");
        }

        private static void AddGroup(StackPanel panel, string title) => panel.Children.Add(new TextBlock
        {
            Text = Translation.T(title), FontSize = 18, FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Margin = new Avalonia.Thickness(0, 12, 0, 2)
        });

        private void AddOptions(StackPanel panel, params string[] names)
        {
            foreach (string name in names)
            {
                PropertyInfo property = typeof(ProfileUserSettingsModel).GetProperty(name)
                    ?? throw new InvalidOperationException($"Unknown setting {name}");
                string label = Translation.T(System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z0-9])(?=[A-Z])", " "));
                Control control;
                if (property.PropertyType == typeof(bool))
                {
                    control = new CheckBox { Content = label };
                    panel.Children.Add(control);
                }
                else
                {
                    Grid row = new Grid { ColumnDefinitions = new ColumnDefinitions("230,*"), ColumnSpacing = 12 };
                    row.Children.Add(new TextBlock { Text = label, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap });
                    if (property.PropertyType.IsEnum)
                        control = new ComboBox { ItemsSource = Enum.GetValues(property.PropertyType) };
                    else if (name == "MultiSamplingCount")
                        control = new ComboBox { ItemsSource = new[] { 0, 2, 4, 8, 16, 32 } };
                    else if (name == "ShadowMapResolution")
                        control = new ComboBox { ItemsSource = new[] { 512, 1024, 2048, 4096 } };
                    else if (property.PropertyType == typeof(int))
                        control = new NumericUpDown
                        {
                            Minimum = name switch
                            {
                                "DetailLevelBias" => -100,
                                "FieldOfView" => 30,
                                "TrackGauge" => 500,
                                _ => 0
                            },
                            Maximum = name switch
                            {
                                "SoundVolumePercent" or "ExternalSoundPassThruPercent" or "AmbientBrightness" or
                                "VisibleDetailLevel" => 100,
                                "SoundDetailLevel" => 5,
                                "FieldOfView" => 120,
                                "ShadowMapCount" => 4,
                                "MultiplayerPort" or "WebServerPort" => 65535,
                                _ => 200000
                            },
                            Increment = 1
                        };
                    else if (property.PropertyType == typeof(string))
                        control = new TextBox();
                    else
                        throw new InvalidOperationException($"Unsupported setting {name}");
                    Grid.SetColumn(control, 1);
                    row.Children.Add(control);
                    panel.Children.Add(row);
                }
                optionControls.Add(property, control);
            }
        }

        private void BuildKeyboardList()
        {
            KeyboardPanel.Children.Clear();
            foreach (UserCommand command in Enum.GetValues<UserCommand>())
            {
                UserCommandInput input = keyboardSettings.UserCommands[command];
                Grid row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,260"),
                    Margin = new Avalonia.Thickness(0, 2)
                };

                row.Children.Add(new TextBlock
                {
                    Text = CommandName(command),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                });

                Button keyButton = new Button
                {
                    Content = input.ToString(),
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    Tag = command,
                    MinWidth = 240
                };
                keyButton.Click += KeyButton_Click;
                Grid.SetColumn(keyButton, 1);
                row.Children.Add(keyButton);
                KeyboardPanel.Children.Add(row);
            }
        }

        private static string CommandName(UserCommand command)
        {
            string name = command.ToString();
            System.Text.StringBuilder result = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                    result.Append(' ');
                result.Append(name[i]);
            }
            return result.ToString();
        }

        private void KeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not UserCommand command)
                return;

            if (keyboardSettings.UserCommands[command].IsModifier)
            {
                button.Content = "Modifier-only command";
                return;
            }

            CancelKeyCapture();
            pendingKeyButton = button;
            pendingCommand = command;
            hasPendingCommand = true;
            button.Content = "Press a key…";
            Focus();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (!hasPendingCommand)
                return;

            e.Handled = true;
            if (e.Key == Key.Escape)
            {
                CancelKeyCapture();
                return;
            }
            if (e.Key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
                return;
            if (!TryConvertKey(e.Key, out XnaKeys key) || key == XnaKeys.None)
                return;

            CommonKeyModifiers modifiers = CommonKeyModifiers.None;
            if ((e.KeyModifiers & AvaloniaKeyModifiers.Shift) != 0)
                modifiers |= CommonKeyModifiers.Shift;
            if ((e.KeyModifiers & AvaloniaKeyModifiers.Control) != 0)
                modifiers |= CommonKeyModifiers.Control;
            if ((e.KeyModifiers & AvaloniaKeyModifiers.Alt) != 0)
                modifiers |= CommonKeyModifiers.Alt;

            int oldDescriptor = keyboardSettings.UserCommands[pendingCommand].UniqueDescriptor;
            int newDescriptor = UserCommandInput.ComposeUniqueDescriptor(modifiers, 0, key);
            // Modifiable commands use the high byte to remember which modifier keys are
            // intentionally ignored. Keep that metadata when changing their main key.
            newDescriptor |= oldDescriptor & unchecked((int)0xFF000000);
            keyboardSettings.UserCommands[pendingCommand].UniqueDescriptor = newDescriptor;

            pendingKeyButton.Content = keyboardSettings.UserCommands[pendingCommand].ToString();
            pendingKeyButton = null;
            hasPendingCommand = false;
            e.Handled = true;
        }

        private static bool TryConvertKey(Avalonia.Input.Key avaloniaKey, out XnaKeys key)
        {
            key = avaloniaKey switch
            {
                Key.Return => XnaKeys.Enter,
                Key.Capital => XnaKeys.CapsLock,
                Key.Prior => XnaKeys.PageUp,
                Key.Next => XnaKeys.PageDown,
                Key.Snapshot => XnaKeys.PrintScreen,
                _ => XnaKeys.None
            };
            if (key != XnaKeys.None)
                return true;
            string name = avaloniaKey.ToString();
            if (Enum.TryParse(name, true, out key))
                return true;

            key = name switch
            {
                "OemPlus" => XnaKeys.OemPlus,
                "OemMinus" => XnaKeys.OemMinus,
                "OemComma" => XnaKeys.OemComma,
                "OemPeriod" => XnaKeys.OemPeriod,
                "OemQuestion" => XnaKeys.OemQuestion,
                "OemSemicolon" => XnaKeys.OemSemicolon,
                "OemQuotes" => XnaKeys.OemQuotes,
                "OemOpenBrackets" => XnaKeys.OemOpenBrackets,
                "OemCloseBrackets" => XnaKeys.OemCloseBrackets,
                "OemPipe" => XnaKeys.OemPipe,
                "OemTilde" => XnaKeys.OemTilde,
                "Back" => XnaKeys.Back,
                _ => XnaKeys.None
            };
            return key != XnaKeys.None;
        }

        private void ResetKeyboard()
        {
            if (!settingsLoaded)
                return;
            CancelKeyCapture();
            ProfileKeyboardSettingsModel defaults = new ProfileKeyboardSettingsModel();
            foreach (UserCommand command in Enum.GetValues<UserCommand>())
                keyboardSettings.UserCommands[command].UniqueDescriptor = defaults.UserCommands[command].UniqueDescriptor;
            BuildKeyboardList();
        }

        private void CancelKeyCapture()
        {
            if (pendingKeyButton != null)
                pendingKeyButton.Content = keyboardSettings.UserCommands[pendingCommand].ToString();
            pendingKeyButton = null;
            hasPendingCommand = false;
        }

        private void SetEditingEnabled(bool enabled)
        {
            SettingsTabs.IsEnabled = enabled;
            SaveButton.IsEnabled = enabled;
            ResetKeyboardButton.IsEnabled = enabled;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!settingsLoaded || !SaveButton.IsEnabled)
                return;
            CancelKeyCapture();
            SetEditingEnabled(false);
            StatusText.Text = string.Empty;
            try
            {

                userSettings.ScreenMode = ScreenModeBox.SelectedItem is ScreenMode mode ? mode : ScreenMode.WindowedFullscreen;
                userSettings.WindowScreen = DisplayBox.SelectedIndex >= 0 ? DisplayBox.SelectedIndex : -1;
                userSettings.UseDesktopResolution = AutoResolutionBox.IsChecked == true;
                if (!userSettings.UseDesktopResolution)
                    userSettings.WindowSettings[WindowSetting.Size] = ((int)(WidthBox.Value ?? 1024), (int)(HeightBox.Value ?? 768));
                foreach ((PropertyInfo property, Control control) in optionControls)
                {
                    object value = control switch
                    {
                        CheckBox box => box.IsChecked == true,
                        NumericUpDown number => (int)(number.Value ?? (int)property.GetValue(userSettings)),
                        ComboBox choice => choice.SelectedItem ?? property.GetValue(userSettings),
                        TextBox text => text.Text,
                        _ => throw new InvalidOperationException($"Unsupported setting {property.Name}")
                    };
                    property.SetValue(userSettings, value);
                }

                await profile.UpdateSettingsModel(keyboardSettings, CancellationToken.None);
                await profile.UpdateSettingsModel(userSettings, CancellationToken.None);
                Close();
            }
            catch (Exception error)
            {
                StatusText.Text = $"Could not save settings: {error.Message}";
                SetEditingEnabled(true);
            }
        }
    }
}
