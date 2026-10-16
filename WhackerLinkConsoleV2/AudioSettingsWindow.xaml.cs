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
* 
*/

using System.Windows;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;
using System.Windows.Controls;
using WhackerLinkLib.Models.Radio;

namespace WhackerLinkConsoleV2
{
    public partial class AudioSettingsWindow : Window
    {
        private readonly SettingsManager _settingsManager;
        private readonly AudioManager _audioManager;
        private readonly List<Codeplug.Channel> _channels;
        private readonly Dictionary<string, int> _selectedOutputDevices = new Dictionary<string, int>();
        private readonly Dictionary<string, StereoChannelMode> _selectedStereoModes = new Dictionary<string, StereoChannelMode>();
        // Explicit per-channel boosts changed by the user this session (dB)
        private readonly Dictionary<string, float> _explicitChannelBoosts = new Dictionary<string, float>();

        public AudioSettingsWindow(SettingsManager settingsManager, AudioManager audioManager, List<Codeplug.Channel> channels)
        {
            InitializeComponent();
            _settingsManager = settingsManager;
            _audioManager = audioManager;
            _channels = channels;

            LoadAudioDevices();
            LoadChannelOutputSettings();
            LoadPttSoundSettings();
            LoadBoostSettings();
            LoadQcToneSettings();
        }

        private void LoadAudioDevices()
        {
            List<string> inputDevices = GetAudioInputDevices();
            List<string> outputDevices = GetAudioOutputDevices();

            InputDeviceComboBox.ItemsSource = inputDevices;
            InputDeviceComboBox.SelectedIndex = _settingsManager.ChannelOutputDevices.ContainsKey("GLOBAL_INPUT")
                ? _settingsManager.ChannelOutputDevices["GLOBAL_INPUT"]
                : 0;
        }

        private void LoadChannelOutputSettings()
        {
            List<string> outputDevices = GetAudioOutputDevices();
            List<string> stereoModes = new List<string> { "Stereo (Both)", "Left Only", "Right Only" };

            foreach (var channel in _channels)
            {
                TextBlock channelLabel = new TextBlock
                {
                    Text = channel.Name,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 5, 0, 0)
                };

                ComboBox outputDeviceComboBox = new ComboBox
                {
                    Width = 350,
                    ItemsSource = outputDevices,
                    SelectedIndex = _settingsManager.ChannelOutputDevices.ContainsKey(channel.Tgid)
                        ? _settingsManager.ChannelOutputDevices[channel.Tgid]
                        : 0
                };

                outputDeviceComboBox.SelectionChanged += (s, e) =>
                {
                    int selectedIndex = outputDeviceComboBox.SelectedIndex;
                    _selectedOutputDevices[channel.Tgid] = selectedIndex;
                };

                // Add stereo channel selection label
                TextBlock stereoLabel = new TextBlock
                {
                    Text = "  Stereo Channel:",
                    Margin = new Thickness(0, 5, 0, 0),
                    FontSize = 11
                };

                // Add stereo channel selection
                ComboBox stereoChannelComboBox = new ComboBox
                {
                    Width = 350,
                    ItemsSource = stereoModes,
                    Margin = new Thickness(0, 2, 0, 0)
                };

                // Set initial selection based on saved settings
                StereoChannelMode currentMode = _settingsManager.ChannelStereoModes.ContainsKey(channel.Tgid)
                    ? _settingsManager.ChannelStereoModes[channel.Tgid]
                    : StereoChannelMode.Stereo;

                stereoChannelComboBox.SelectedIndex = currentMode switch
                {
                    StereoChannelMode.Stereo => 0,
                    StereoChannelMode.LeftOnly => 1,
                    StereoChannelMode.RightOnly => 2,
                    _ => 0
                };

                stereoChannelComboBox.SelectionChanged += (s, e) =>
                {
                    StereoChannelMode mode = stereoChannelComboBox.SelectedIndex switch
                    {
                        0 => StereoChannelMode.Stereo,
                        1 => StereoChannelMode.LeftOnly,
                        2 => StereoChannelMode.RightOnly,
                        _ => StereoChannelMode.Stereo
                    };
                    _selectedStereoModes[channel.Tgid] = mode;
                };

                // Add loudness boost selection
                StackPanel boostPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 2, 0, 0)
                };

                boostPanel.Children.Add(new TextBlock
                {
                    Text = "  Boost (dB):",
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 11
                });

                Slider boostSlider = new Slider
                {
                    Width = 150,
                    Minimum = 0,
                    Maximum = 24,
                    TickFrequency = 1,
                    IsSnapToTickEnabled = true,
                    VerticalAlignment = VerticalAlignment.Center
                };

                TextBlock boostLabel = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };

                // Effective boost: per-channel override if present, otherwise the global default
                float currentBoost = _settingsManager.ChannelBoosts.ContainsKey(channel.Tgid)
                    ? _settingsManager.ChannelBoosts[channel.Tgid]
                    : _settingsManager.DefaultChannelBoostDb;

                // Set the initial value before attaching the handler so it doesn't count as an explicit change
                boostSlider.Value = currentBoost;
                boostLabel.Text = $"{(int)currentBoost} dB";

                boostSlider.ValueChanged += (s, e) =>
                {
                    _explicitChannelBoosts[channel.Tgid] = (float)boostSlider.Value;
                    boostLabel.Text = $"{(int)boostSlider.Value} dB";
                };

                boostPanel.Children.Add(boostSlider);
                boostPanel.Children.Add(boostLabel);

                ChannelOutputStackPanel.Children.Add(channelLabel);
                ChannelOutputStackPanel.Children.Add(outputDeviceComboBox);
                ChannelOutputStackPanel.Children.Add(stereoLabel);
                ChannelOutputStackPanel.Children.Add(stereoChannelComboBox);
                ChannelOutputStackPanel.Children.Add(boostPanel);
            }
        }

        private void LoadBoostSettings()
        {
            DefaultBoostSlider.Value = _settingsManager.DefaultChannelBoostDb;
            DefaultBoostLabel.Text = $"{(int)DefaultBoostSlider.Value} dB";
        }

        private void DefaultBoostSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DefaultBoostLabel != null)
                DefaultBoostLabel.Text = $"{(int)e.NewValue} dB";
        }

        private void LoadQcToneSettings()
        {
            QcToneLevelSlider.Value = Math.Clamp(_settingsManager.QcToneLevelDb, (float)QcToneLevelSlider.Minimum, (float)QcToneLevelSlider.Maximum);
            QcToneLevelLabel.Text = $"{(int)QcToneLevelSlider.Value} dBFS";
        }

        private void QcToneLevelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (QcToneLevelLabel != null)
                QcToneLevelLabel.Text = $"{(int)e.NewValue} dBFS";
        }

        private List<string> GetAudioInputDevices()
        {
            List<string> inputDevices = new List<string>();

            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var deviceInfo = WaveIn.GetCapabilities(i);
                inputDevices.Add(deviceInfo.ProductName);
            }

            return inputDevices;
        }

        private List<string> GetAudioOutputDevices()
        {
            List<string> outputDevices = new List<string>();

            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var deviceInfo = WaveOut.GetCapabilities(i);
                outputDevices.Add(deviceInfo.ProductName);
            }

            return outputDevices;
        }

        private void LoadPttSoundSettings()
        {
            EnablePttDownSoundCheckBox.IsChecked = _settingsManager.EnablePttDownSound;
            EnablePttUpSoundCheckBox.IsChecked = _settingsManager.EnablePttUpSound;
            PttTailDelayTextBox.Text = _settingsManager.PttTailDelayMs.ToString();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            int selectedInputIndex = InputDeviceComboBox.SelectedIndex;
            _settingsManager.UpdateChannelOutputDevice("GLOBAL_INPUT", selectedInputIndex);

            foreach (var entry in _selectedOutputDevices)
            {
                _settingsManager.UpdateChannelOutputDevice(entry.Key, entry.Value);
                _audioManager.SetTalkgroupOutputDevice(entry.Key, entry.Value);
            }

            // Save stereo channel modes
            foreach (var entry in _selectedStereoModes)
            {
                _settingsManager.UpdateChannelStereoMode(entry.Key, entry.Value);
                _audioManager.SetTalkgroupStereoMode(entry.Key, entry.Value);
            }

            // Save loudness boost settings
            _settingsManager.UpdateDefaultChannelBoost((float)DefaultBoostSlider.Value);

            // Save the Quick Call II page tone transmit level
            _settingsManager.UpdateQcToneLevel((float)QcToneLevelSlider.Value);

            foreach (var entry in _explicitChannelBoosts)
            {
                _settingsManager.UpdateChannelBoost(entry.Key, entry.Value);
            }

            // Live-apply the effective boost (per-channel override or global default)
            foreach (var channel in _channels)
            {
                float effectiveBoost = _explicitChannelBoosts.ContainsKey(channel.Tgid)
                    ? _explicitChannelBoosts[channel.Tgid]
                    : _settingsManager.ChannelBoosts.ContainsKey(channel.Tgid)
                        ? _settingsManager.ChannelBoosts[channel.Tgid]
                        : _settingsManager.DefaultChannelBoostDb;

                _audioManager.SetTalkgroupBoost(channel.Tgid, effectiveBoost);
            }

            // Save PTT sound settings
            _settingsManager.EnablePttDownSound = EnablePttDownSoundCheckBox.IsChecked ?? true;
            _settingsManager.EnablePttUpSound = EnablePttUpSoundCheckBox.IsChecked ?? true;
            
            // Save PTT tail delay
            if (int.TryParse(PttTailDelayTextBox.Text, out int tailDelay))
            {
                _settingsManager.PttTailDelayMs = Math.Max(0, tailDelay);
            }
            
            _settingsManager.SaveSettings();

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
