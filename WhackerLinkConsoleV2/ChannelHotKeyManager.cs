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
using System.Windows.Input;
using WhackerLinkConsoleV2.Controls;

namespace WhackerLinkConsoleV2
{
    /// <summary>
    /// Manages per-channel PTT hotkey registrations and triggering
    /// </summary>
    public class ChannelHotKeyManager
    {
        private GlobalHotKeyManager _globalHotKeyManager;
        private ChannelKeybindingManager _channelKeybindingManager;
        private Dictionary<string, int> _channelHotKeyIds = new Dictionary<string, int>();
        private Dictionary<int, string> _hotKeyIdToChannelName = new Dictionary<int, string>();
        private List<ChannelBox> _allChannels = new List<ChannelBox>();
        private string _currentCodeplugIdentifier;
        private Action<ChannelBox, bool> _onChannelPttTriggered;

        public ChannelHotKeyManager(GlobalHotKeyManager globalHotKeyManager, ChannelKeybindingManager channelKeybindingManager, Action<ChannelBox, bool> onChannelPttTriggered)
        {
            _globalHotKeyManager = globalHotKeyManager;
            _channelKeybindingManager = channelKeybindingManager;
            _onChannelPttTriggered = onChannelPttTriggered;
        }

        /// <summary>
        /// Initializes channel hotkeys for the current codeplug
        /// </summary>
        public void InitializeChannelHotkeys(string codeplugIdentifier, List<ChannelBox> channels)
        {
            // Unregister old hotkeys if codeplug changed
            if (_currentCodeplugIdentifier != codeplugIdentifier)
            {
                UnregisterAllChannelHotkeys();
                _currentCodeplugIdentifier = codeplugIdentifier;
            }

            _allChannels = channels;

            // Register hotkeys for channels that have keybindings
            int registeredCount = 0;
            foreach (var channel in channels)
            {
                var keybinding = _channelKeybindingManager.GetChannelKeybinding(codeplugIdentifier, channel.ChannelName);
                
                if (!string.IsNullOrWhiteSpace(keybinding))
                {
                    RegisterChannelHotkey(channel.ChannelName, keybinding);
                    registeredCount++;
                }
            }
            
            if (registeredCount > 0)
            {
                Console.WriteLine($"Registered {registeredCount} channel hotkey(s)");
            }
        }

        /// <summary>
        /// Registers a hotkey for a specific channel
        /// </summary>
        private void RegisterChannelHotkey(string channelName, string keybinding)
        {
            if (_globalHotKeyManager == null)
            {
                Console.WriteLine($"ERROR: GlobalHotKeyManager is null! Cannot register hotkey for '{channelName}'");
                return;
            }

            // Unregister old hotkey if it exists
            if (_channelHotKeyIds.ContainsKey(channelName))
            {
                var oldHotKeyId = _channelHotKeyIds[channelName];
                _globalHotKeyManager.UnregisterHotKey(oldHotKeyId);
                _channelHotKeyIds.Remove(channelName);
                _hotKeyIdToChannelName.Remove(oldHotKeyId);
            }

            // Parse the keybinding
            if (!KeybindingParser.TryParseKeybinding(keybinding, out var modifiers, out var key))
            {
                Console.WriteLine($"ERROR: Failed to parse keybinding '{keybinding}' for channel '{channelName}'");
                return;
            }

            // Register with global hotkey manager
            var hotKeyId = _globalHotKeyManager.RegisterHotKey(
                modifiers,
                key,
                () => OnChannelHotKeyDown(channelName),
                () => OnChannelHotKeyUp(channelName)
            );

            if (hotKeyId != -1)
            {
                _channelHotKeyIds[channelName] = hotKeyId;
                _hotKeyIdToChannelName[hotKeyId] = channelName;
            }
            else
            {
                Console.WriteLine($"Failed to register hotkey '{keybinding}' for channel '{channelName}'");
            }
        }

        /// <summary>
        /// Unregisters a hotkey for a specific channel
        /// </summary>
        public void UnregisterChannelHotkey(string channelName)
        {
            if (_channelHotKeyIds.TryGetValue(channelName, out var hotKeyId))
            {
                _globalHotKeyManager.UnregisterHotKey(hotKeyId);
                _channelHotKeyIds.Remove(channelName);
                _hotKeyIdToChannelName.Remove(hotKeyId);
            }
        }

        /// <summary>
        /// Unregisters all channel hotkeys
        /// </summary>
        public void UnregisterAllChannelHotkeys()
        {
            var channelNames = new List<string>(_channelHotKeyIds.Keys);
            foreach (var channelName in channelNames)
            {
                UnregisterChannelHotkey(channelName);
            }
        }

        /// <summary>
        /// Called when a channel hotkey is pressed
        /// </summary>
        private void OnChannelHotKeyDown(string channelName)
        {
            var channel = _allChannels.FirstOrDefault(c => c.ChannelName == channelName);
            if (channel != null)
            {
                _onChannelPttTriggered?.Invoke(channel, true);
            }
        }

        /// <summary>
        /// Called when a channel hotkey is released
        /// </summary>
        private void OnChannelHotKeyUp(string channelName)
        {
            var channel = _allChannels.FirstOrDefault(c => c.ChannelName == channelName);
            if (channel != null)
            {
                _onChannelPttTriggered?.Invoke(channel, false);
            }
        }
    }
}
