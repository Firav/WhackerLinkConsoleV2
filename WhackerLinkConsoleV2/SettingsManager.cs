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

using System.IO;
using Newtonsoft.Json;

namespace WhackerLinkConsoleV2
{
    public class SettingsManager
    {
        private const string SettingsFilePath = "UserSettings.json";

        public bool ShowSystemStatus { get; set; } = true;
        public bool ShowChannels { get; set; } = true;
        public bool ShowAlertTones { get; set; } = true;

        public string LastCodeplugPath { get; set; } = null;

        public Dictionary<string, ChannelPosition> ChannelPositions { get; set; } = new Dictionary<string, ChannelPosition>();
        public Dictionary<string, ChannelPosition> SystemStatusPositions { get; set; } = new Dictionary<string, ChannelPosition>();
        public List<string> AlertToneFilePaths { get; set; } = new List<string>();
        public Dictionary<string, ChannelPosition> AlertTonePositions { get; set; } = new Dictionary<string, ChannelPosition>();
        public Dictionary<string, int> ChannelOutputDevices { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, StereoChannelMode> ChannelStereoModes { get; set; } = new Dictionary<string, StereoChannelMode>();

        // Loudness boost settings (fix for quiet systems)
        // Default boost in dB for channels without an explicit per-channel boost (0 = off)
        public float DefaultChannelBoostDb { get; set; } = 0f;
        // Explicit per-channel boost overrides (dB) keyed by talkgroup id
        public Dictionary<string, float> ChannelBoosts { get; set; } = new Dictionary<string, float>();

        // Hotkey bindings for global PTT
        // Supports both single-key (e.g., "T", "P") and dual-key (e.g., "Ctrl+T", "Ctrl+P")
        public string GlobalPttKeybind { get; set; } = "Ctrl+T";          // Global PTT across all selected channels
        public bool EnableGlobalPttHotkey { get; set; } = true;
        
        // Controls whether hotkeys work when the application is not focused/tabbed out
        public bool HotkeysWorkWhenUnfocused { get; set; } = true;

        // PTT Sound Settings
        public bool EnablePttDownSound { get; set; } = false;
        public bool EnablePttUpSound { get; set; } = false;
        
        // PTT Tail Delay (in milliseconds) - time to keep transmission open after PTT release
        public int PttTailDelayMs { get; set; } = 0;

        // Quick Call II page tone transmit level in dBFS (0 = full scale, -30 = comfortable page level).
        // Generated QC2 tones are scaled to this level so listening radios (including the FiveM NUI
        // radios) get a comfortable page instead of a full scale sine wave. 0 restores the old full
        // volume tones. This is the "Option A" console side fix for ear splitting page tones.
        public float QcToneLevelDb { get; set; } = -30f;

        public void LoadSettings()
        {
            if (!File.Exists(SettingsFilePath)) return;

            try
            {
                var json = File.ReadAllText(SettingsFilePath);
                var loadedSettings = JsonConvert.DeserializeObject<SettingsManager>(json);

                if (loadedSettings != null)
                {
                    ShowSystemStatus = loadedSettings.ShowSystemStatus;
                    ShowChannels = loadedSettings.ShowChannels;
                    ShowAlertTones = loadedSettings.ShowAlertTones;
                    LastCodeplugPath = loadedSettings.LastCodeplugPath;
                    ChannelPositions = loadedSettings.ChannelPositions ?? new Dictionary<string, ChannelPosition>();
                    SystemStatusPositions = loadedSettings.SystemStatusPositions ?? new Dictionary<string, ChannelPosition>();
                    AlertToneFilePaths = loadedSettings.AlertToneFilePaths ?? new List<string>();
                    AlertTonePositions = loadedSettings.AlertTonePositions ?? new Dictionary<string, ChannelPosition>();
                    ChannelOutputDevices = loadedSettings.ChannelOutputDevices ?? new Dictionary<string, int>();
                    ChannelStereoModes = loadedSettings.ChannelStereoModes ?? new Dictionary<string, StereoChannelMode>();
                    DefaultChannelBoostDb = loadedSettings.DefaultChannelBoostDb;
                    ChannelBoosts = loadedSettings.ChannelBoosts ?? new Dictionary<string, float>();
                    GlobalPttKeybind = loadedSettings.GlobalPttKeybind ?? "Ctrl+T";
                    EnableGlobalPttHotkey = loadedSettings.EnableGlobalPttHotkey;
                    HotkeysWorkWhenUnfocused = loadedSettings.HotkeysWorkWhenUnfocused;
                    EnablePttDownSound = loadedSettings.EnablePttDownSound;
                    EnablePttUpSound = loadedSettings.EnablePttUpSound;
                    PttTailDelayMs = loadedSettings.PttTailDelayMs;
                    QcToneLevelDb = loadedSettings.QcToneLevelDb;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }
        }

        public void UpdateAlertTonePaths(string newFilePath)
        {
            if (!AlertToneFilePaths.Contains(newFilePath))
            {
                AlertToneFilePaths.Add(newFilePath);
                SaveSettings();
            }
        }

        public void UpdateAlertTonePosition(string alertFileName, double x, double y)
        {
            AlertTonePositions[alertFileName] = new ChannelPosition { X = x, Y = y };
            SaveSettings();
        }

        public void UpdateChannelPosition(string channelName, double x, double y)
        {
            ChannelPositions[channelName] = new ChannelPosition { X = x, Y = y };
            SaveSettings();
        }

        public void UpdateSystemStatusPosition(string systemName, double x, double y)
        {
            SystemStatusPositions[systemName] = new ChannelPosition { X = x, Y = y };
            SaveSettings();
        }

        public void UpdateChannelOutputDevice(string channelName, int deviceIndex)
        {
            ChannelOutputDevices[channelName] = deviceIndex;
            SaveSettings();
        }

        public void UpdateChannelStereoMode(string channelName, StereoChannelMode stereoMode)
        {
            ChannelStereoModes[channelName] = stereoMode;
            SaveSettings();
        }

        public void UpdateChannelBoost(string channelName, float boostDb)
        {
            ChannelBoosts[channelName] = Math.Max(0f, boostDb);
            SaveSettings();
        }

        public void UpdateDefaultChannelBoost(float boostDb)
        {
            DefaultChannelBoostDb = Math.Max(0f, boostDb);
            SaveSettings();
        }

        public void UpdateQcToneLevel(float levelDb)
        {
            QcToneLevelDb = Math.Clamp(levelDb, -60f, 0f);
            SaveSettings();
        }

        /// <summary>
        /// Linear peak amplitude (0.0 - 1.0) for generated Quick Call II page tones,
        /// derived from <see cref="QcToneLevelDb"/>.
        /// </summary>
        public double GetQcToneAmplitude()
        {
            return Math.Pow(10.0, Math.Clamp(QcToneLevelDb, -60f, 0f) / 20.0);
        }

        public void SaveSettings()
        {
            try
            {
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}
