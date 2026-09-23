// COPYRIGHT 2026 by the Riel project.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;

using CommonKeyModifiers = FreeTrainSimulator.Common.Input.KeyModifiers;
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

        public SettingsWindow(ProfileModel profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            InitializeComponent();

            ScreenModeBox.ItemsSource = Enum.GetValues<ScreenMode>();
            MsaaBox.ItemsSource = new[] { 0, 2, 4, 8 };
            ShadowResolutionBox.ItemsSource = new[] { 512, 1024, 2048, 4096 };

            SaveButton.Click += SaveButton_Click;
            CancelButton.Click += (_, _) => Close();
            ResetKeyboardButton.Click += (_, _) => ResetKeyboard();

            Opened += async (_, _) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            keyboardSettings = await profile.LoadSettingsModel<ProfileKeyboardSettingsModel>(CancellationToken.None);
            userSettings = await profile.LoadSettingsModel<ProfileUserSettingsModel>(CancellationToken.None);

            BuildKeyboardList();
            ScreenModeBox.SelectedItem = userSettings.ScreenMode;
            WidthBox.Value = userSettings.WindowSettings[WindowSetting.Size].X;
            HeightBox.Value = userSettings.WindowSettings[WindowSetting.Size].Y;
            VsyncBox.IsChecked = userSettings.VerticalSync;
            MsaaBox.SelectedItem = userSettings.MultiSamplingCount;
            DynamicShadowsBox.IsChecked = userSettings.DynamicShadows;
            ModelInstancingBox.IsChecked = userSettings.ModelInstancing;
            ShadowAllShapesBox.IsChecked = userSettings.ShadowAllShapes;
            ViewingDistanceBox.Value = userSettings.ViewingDistance;
            FarMountainsBox.Value = userSettings.FarMountainsViewingDistance;
            FovBox.Value = userSettings.FieldOfView;
            DetailLevelBox.Value = userSettings.VisibleDetailLevel;
            AmbientBox.Value = userSettings.AmbientBrightness;
            ShadowResolutionBox.SelectedItem = userSettings.ShadowMapResolution;
            ShadowBlurBox.IsChecked = userSettings.ShadowMapBlur;
            SignalGlowBox.IsChecked = userSettings.SignalLightGlow;
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

            XnaKeys key;
            if (!TryConvertKey(e.Key, out key) || key == XnaKeys.None)
                return;

            CommonKeyModifiers modifiers = CommonKeyModifiers.None;
            if ((e.KeyModifiers & KeyModifiers.Shift) != 0)
                modifiers |= CommonKeyModifiers.Shift;
            if ((e.KeyModifiers & KeyModifiers.Control) != 0)
                modifiers |= CommonKeyModifiers.Control;
            if ((e.KeyModifiers & KeyModifiers.Alt) != 0)
                modifiers |= CommonKeyModifiers.Alt;

            keyboardSettings.UserCommands[pendingCommand].UniqueDescriptor =
                UserCommandInput.ComposeUniqueDescriptor(modifiers, 0, key);

            pendingKeyButton.Content = keyboardSettings.UserCommands[pendingCommand].ToString();
            pendingKeyButton = null;
            hasPendingCommand = false;
            e.Handled = true;
        }

        private static bool TryConvertKey(Avalonia.Input.Key avaloniaKey, out XnaKeys key)
        {
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
            keyboardSettings = new ProfileKeyboardSettingsModel();
            BuildKeyboardList();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (hasPendingCommand)
            {
                hasPendingCommand = false;
                pendingKeyButton = null;
            }

            userSettings.ScreenMode = ScreenModeBox.SelectedItem is ScreenMode mode ? mode : ScreenMode.WindowedFullscreen;
            int width = (int)(WidthBox.Value ?? 1024);
            int height = (int)(HeightBox.Value ?? 768);
            userSettings.WindowSettings[WindowSetting.Size] = (width, height);
            userSettings.VerticalSync = VsyncBox.IsChecked == true;
            userSettings.MultiSamplingCount = MsaaBox.SelectedItem is int msaa ? msaa : 4;
            userSettings.DynamicShadows = DynamicShadowsBox.IsChecked == true;
            userSettings.ModelInstancing = ModelInstancingBox.IsChecked == true;
            userSettings.ShadowAllShapes = ShadowAllShapesBox.IsChecked == true;
            userSettings.ViewingDistance = (int)(ViewingDistanceBox.Value ?? 2000);
            userSettings.FarMountainsViewingDistance = (int)(FarMountainsBox.Value ?? 40000);
            userSettings.FieldOfView = (int)(FovBox.Value ?? 45);
            userSettings.VisibleDetailLevel = (int)(DetailLevelBox.Value ?? 49);
            userSettings.AmbientBrightness = (int)(AmbientBox.Value ?? 20);
            userSettings.ShadowMapResolution = ShadowResolutionBox.SelectedItem is int resolution ? resolution : 1024;
            userSettings.ShadowMapBlur = ShadowBlurBox.IsChecked == true;
            userSettings.SignalLightGlow = SignalGlowBox.IsChecked == true;

            await profile.UpdateSettingsModel(keyboardSettings, CancellationToken.None);
            await profile.UpdateSettingsModel(userSettings, CancellationToken.None);
            Close();
        }
    }
}
