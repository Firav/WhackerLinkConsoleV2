/*
* WhackerLink - WhackerLinkConsoleV2
*
* This program is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published by
* the Free Software Foundation, either version 3 of the License, or
* (at your option) any later version.
*
* This program is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with this program.  If not, see <http://www.gnu.org/licenses/>.
* 
* Copyright (C) 2024-2025 Caleb, K4PHP
* Copyright (C) 2025 Firav (firavdev@gmail.com)
* 
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WhackerLinkLib.Models.Radio;

namespace WhackerLinkConsoleV2
{
    public partial class KeybindingConfigWindow : Window
    {
        private SettingsManager _settingsManager;
        private ChannelKeybindingManager _channelKeybindingManager;
        private Codeplug _codeplug;
        private string _codeplugIdentifier;

        private bool _recordingGlobalPtt = false;
        private Dictionary<string, TextBox> _channelKeybindingControls = new Dictionary<string, TextBox>();

        public KeybindingConfigWindow(SettingsManager settingsManager, ChannelKeybindingManager channelKeybindingManager, Codeplug codeplug, string codeplugFilePath)
        {
            InitializeComponent();
            _settingsManager = settingsManager;
            _channelKeybindingManager = channelKeybindingManager;
            _codeplug = codeplug;
            _codeplugIdentifier = ChannelKeybindingManager.GenerateCodeplugIdentifier(codeplugFilePath);

            LoadSettings();
            GenerateChannelControls();
        }

        private void LoadSettings()
        {
            // Load global keybinding
            GlobalPttKeybindDisplay.Text = _settingsManager.GlobalPttKeybind;

            // Load codeplug identifier
            CodeplugIdentifierText.Text = _codeplugIdentifier;
        }

        private void GenerateChannelControls()
        {
            ChannelKeybindingsPanel.Children.Clear();
            _channelKeybindingControls.Clear();

            if (_codeplug == null || _codeplug.Zones == null || _codeplug.Zones.Count == 0)
            {
                var noChannelsText = new TextBlock
                {
                    Text = "No channels available",
                    Foreground = System.Windows.Media.Brushes.Gray,
                    FontSize = 11,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                ChannelKeybindingsPanel.Children.Add(noChannelsText);
                return;
            }

            var allChannels = _codeplug.Zones.SelectMany(z => z.Channels).Distinct().OrderBy(c => c.Name).ToList();

            if (allChannels.Count == 0)
            {
                var noChannelsText = new TextBlock
                {
                    Text = "No channels in codeplug",
                    Foreground = System.Windows.Media.Brushes.Gray,
                    FontSize = 11,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                ChannelKeybindingsPanel.Children.Add(noChannelsText);
                return;
            }

            foreach (var channel in allChannels)
            {
                var existingKeybind = _channelKeybindingManager.GetChannelKeybinding(_codeplugIdentifier, channel.Name) ?? "";

                var grid = new Grid
                {
                    Margin = new Thickness(0, 0, 0, 10)
                };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Channel name
                var channelNameText = new TextBlock
                {
                    Text = channel.Name,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                Grid.SetColumn(channelNameText, 0);
                grid.Children.Add(channelNameText);

                // Keybinding textbox
                var keybindTextBox = new TextBox
                {
                    Text = existingKeybind,
                    IsReadOnly = true,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(5),
                    Margin = new Thickness(0, 0, 10, 0)
                };
                Grid.SetColumn(keybindTextBox, 1);
                grid.Children.Add(keybindTextBox);

                _channelKeybindingControls[channel.Name] = keybindTextBox;

                // Record button
                var recordButton = new Button
                {
                    Content = "Record",
                    Width = 80,
                    Tag = channel.Name
                };
                recordButton.Click += (s, e) => RecordChannelKeybind_Click(channel.Name);
                Grid.SetColumn(recordButton, 2);
                grid.Children.Add(recordButton);

                ChannelKeybindingsPanel.Children.Add(grid);
            }
        }

        private void RecordGlobalPtt_Click(object sender, RoutedEventArgs e)
        {
            _recordingGlobalPtt = true;
            RecordGlobalPttButton.Content = "Press keys...";
            RecordGlobalPttButton.IsEnabled = false;
            Focus();
        }

        private void RecordChannelKeybind_Click(string channelName)
        {
            if (!_channelKeybindingControls.TryGetValue(channelName, out var textBox))
                return;

            var recordingTag = $"recording_{channelName}";
            Tag = recordingTag;
            textBox.Text = "Press keys...";
            Focus();
        }

        private void ClearGlobalPtt_Click(object sender, RoutedEventArgs e)
        {
            GlobalPttKeybindDisplay.Text = "";
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            // Get current modifiers
            var modifiers = Keyboard.Modifiers;
            var key = e.Key;

            // Don't record if only a modifier key is pressed (wait for the actual key)
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LWin || key == Key.RWin ||
                key == Key.System)
            {
                e.Handled = true;
                return;
            }

            // Handle global PTT recording
            if (_recordingGlobalPtt)
            {
                var keybinding = KeybindingParser.KeybindingToString(modifiers, key);
                GlobalPttKeybindDisplay.Text = keybinding;
                
                _recordingGlobalPtt = false;
                RecordGlobalPttButton.Content = "Record";
                RecordGlobalPttButton.IsEnabled = true;
                
                e.Handled = true;
                return;
            }

            // Handle channel-specific keybind recording
            if (Tag is string tag && tag.StartsWith("recording_"))
            {
                var channelName = tag.Substring("recording_".Length);
                if (_channelKeybindingControls.TryGetValue(channelName, out var textBox))
                {
                    var keybinding = KeybindingParser.KeybindingToString(modifiers, key);
                    textBox.Text = keybinding;
                    Tag = null;
                }
                e.Handled = true;
                return;
            }

            base.OnPreviewKeyDown(e);
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // Validate and save global keybinding
            if (!string.IsNullOrWhiteSpace(GlobalPttKeybindDisplay.Text))
            {
                if (!KeybindingParser.TryParseKeybinding(GlobalPttKeybindDisplay.Text, out _, out _))
                {
                    MessageBox.Show("Invalid Global PTT keybinding format.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _settingsManager.GlobalPttKeybind = GlobalPttKeybindDisplay.Text;
            }

            _settingsManager.SaveSettings();

            // Validate and save per-channel keybindings
            foreach (var kvp in _channelKeybindingControls)
            {
                var channelName = kvp.Key;
                var textBox = kvp.Value;
                var keybinding = textBox.Text;

                if (string.IsNullOrWhiteSpace(keybinding))
                {
                    _channelKeybindingManager.RemoveChannelKeybinding(_codeplugIdentifier, channelName);
                }
                else
                {
                    if (!KeybindingParser.TryParseKeybinding(keybinding, out _, out _))
                    {
                        MessageBox.Show($"Invalid keybinding for channel '{channelName}': {keybinding}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    _channelKeybindingManager.SetChannelKeybinding(_codeplugIdentifier, channelName, keybinding);
                }
            }

            MessageBox.Show("Keybindings saved successfully! Restart the application or reload the codeplug to apply changes.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
