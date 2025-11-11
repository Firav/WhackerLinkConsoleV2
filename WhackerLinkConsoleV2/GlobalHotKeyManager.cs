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
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Input;

namespace WhackerLinkConsoleV2
{
    /// <summary>
    /// Represents arguments for hotkey events
    /// </summary>
    public class HotKeyEventArgs : EventArgs
    {
        public int HotKeyId { get; set; }
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }
    }

    /// <summary>
    /// Manages global system hotkeys using Windows API
    /// </summary>
    public class GlobalHotKeyManager : IDisposable
    {
        // Windows API P/Invoke declarations
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        // Modifier key constants
        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        // Message constants
        private const int WM_HOTKEY = 0x0312;

        // Virtual key codes for modifier keys
        private const int VK_CONTROL = 0x11;
        private const int VK_SHIFT = 0x10;
        private const int VK_MENU = 0x12; // Alt key

        private Window _window;
        private IntPtr _windowHandle;
        private HwndSource _hwndSource;
        private Dictionary<int, HotKeyInfo> _registeredHotKeys = new Dictionary<int, HotKeyInfo>();
        private Dictionary<int, bool> _hotKeyStates = new Dictionary<int, bool>(); // Track if hotkey is currently pressed
        private int _nextHotKeyId = 1;
        private System.Timers.Timer _keyStateTimer;

        public event EventHandler<HotKeyEventArgs> HotKeyPressed;
        public event EventHandler<HotKeyEventArgs> HotKeyReleased;

        /// <summary>
        /// Internal class to track hotkey information
        /// </summary>
        private class HotKeyInfo
        {
            public int Id { get; set; }
            public Key Key { get; set; }
            public ModifierKeys Modifiers { get; set; }
            public Action OnKeyDown { get; set; }
            public Action OnKeyUp { get; set; }
        }

        /// <summary>
        /// Initializes the global hotkey manager for the specified window
        /// </summary>
        public void Initialize(Window window)
        {
            _window = window;
            _windowHandle = new WindowInteropHelper(window).Handle;
            
            if (_windowHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Window handle is invalid. Ensure the window is fully initialized.");
            }

            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            if (_hwndSource != null)
            {
                _hwndSource.AddHook(HwndHook);
            }

            // Start a timer to poll key states for detecting key releases
            _keyStateTimer = new System.Timers.Timer(50); // Poll every 50ms
            _keyStateTimer.Elapsed += CheckKeyStates;
            _keyStateTimer.AutoReset = true;
            _keyStateTimer.Start();
        }

        /// <summary>
        /// Registers a global hotkey
        /// </summary>
        /// <returns>Returns the hotkey ID if successful, -1 if failed</returns>
        public int RegisterHotKey(ModifierKeys modifier, Key key, Action onKeyDown = null, Action onKeyUp = null)
        {
            if (_windowHandle == IntPtr.Zero)
            {
                Console.WriteLine($"ERROR: GlobalHotKeyManager not initialized. Call Initialize() first.");
                return -1;
            }

            // Single-key hotkeys without modifiers are not recommended and may be rejected by Windows
            if (modifier == ModifierKeys.None)
            {
                Console.WriteLine($"WARNING: Single-key hotkey without modifiers ({key}) may not work reliably or may be rejected by Windows.");
                Console.WriteLine($"         It's recommended to use at least one modifier key (e.g., Ctrl+{key}).");
            }

            int hotKeyId = _nextHotKeyId++;
            uint modifierFlags = ConvertModifierKeys(modifier);
            uint vkCode = (uint)KeyInterop.VirtualKeyFromKey(key);

            if (vkCode == 0)
            {
                Console.WriteLine($"ERROR: Could not convert key {key} to virtual key code.");
                return -1;
            }

            if (RegisterHotKey(_windowHandle, hotKeyId, modifierFlags, vkCode))
            {
                var hotKeyInfo = new HotKeyInfo
                {
                    Id = hotKeyId,
                    Key = key,
                    Modifiers = modifier,
                    OnKeyDown = onKeyDown,
                    OnKeyUp = onKeyUp
                };

                _registeredHotKeys[hotKeyId] = hotKeyInfo;
                
                return hotKeyId;
            }
            else
            {
                uint error = GetLastError();
                string modifierStr = modifier == ModifierKeys.None ? "(no modifiers)" : modifier.ToString();
                Console.WriteLine($"ERROR: Failed to register hotkey {modifierStr}+{key}. Error code: {error}");
                
                // Common error codes
                if (error == 1409) // ERROR_HOTKEY_ALREADY_REGISTERED
                {
                    Console.WriteLine($"       This hotkey is already registered by another application.");
                }
                
                return -1;
            }
        }

        /// <summary>
        /// Unregisters a global hotkey by ID
        /// </summary>
        public bool UnregisterHotKey(int hotKeyId)
        {
            if (!_registeredHotKeys.ContainsKey(hotKeyId))
            {
                Console.WriteLine($"WARNING: Hotkey ID {hotKeyId} not found in registered hotkeys.");
                return false;
            }

            if (UnregisterHotKey(_windowHandle, hotKeyId))
            {
                _registeredHotKeys.Remove(hotKeyId);
                return true;
            }
            else
            {
                uint error = GetLastError();
                Console.WriteLine($"ERROR: Failed to unregister hotkey ID {hotKeyId}. Error code: {error}");
                return false;
            }
        }

        /// <summary>
        /// Unregisters all registered hotkeys
        /// </summary>
        public void UnregisterAllHotKeys()
        {
            var hotKeyIds = new List<int>(_registeredHotKeys.Keys);
            foreach (var id in hotKeyIds)
            {
                UnregisterHotKey(id);
            }
        }

        /// <summary>
        /// Gets information about a registered hotkey
        /// </summary>
        public bool GetHotKeyInfo(int hotKeyId, out ModifierKeys modifier, out Key key)
        {
            modifier = ModifierKeys.None;
            key = Key.None;

            if (_registeredHotKeys.TryGetValue(hotKeyId, out var hotKeyInfo))
            {
                modifier = hotKeyInfo.Modifiers;
                key = hotKeyInfo.Key;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the list of all registered hotkey IDs
        /// </summary>
        public IEnumerable<int> GetRegisteredHotKeyIds()
        {
            return new List<int>(_registeredHotKeys.Keys);
        }

        /// <summary>
        /// Converts WPF ModifierKeys to Windows API modifier flags
        /// </summary>
        private uint ConvertModifierKeys(ModifierKeys modifiers)
        {
            uint result = MOD_NONE;

            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                result |= MOD_ALT;
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                result |= MOD_CONTROL;
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                result |= MOD_SHIFT;
            if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
                result |= MOD_WIN;

            return result;
        }

        /// <summary>
        /// Window message hook for processing hotkey messages
        /// </summary>
        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int hotKeyId = wParam.ToInt32();

                if (_registeredHotKeys.TryGetValue(hotKeyId, out var hotKeyInfo))
                {
                    // Check if this hotkey is already pressed (to prevent repeated triggers)
                    bool wasAlreadyPressed = _hotKeyStates.TryGetValue(hotKeyId, out bool isPressed) && isPressed;

                    if (!wasAlreadyPressed)
                    {
                        // Only trigger on first press, not repeated WM_HOTKEY messages
                        var args = new HotKeyEventArgs
                        {
                            HotKeyId = hotKeyId,
                            Key = hotKeyInfo.Key,
                            Modifiers = hotKeyInfo.Modifiers
                        };

                        // Fire the pressed event
                        HotKeyPressed?.Invoke(this, args);

                        // Call the on-key-down callback if registered
                        hotKeyInfo.OnKeyDown?.Invoke();

                        // Mark this hotkey as pressed
                        _hotKeyStates[hotKeyId] = true;
                    }

                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Checks if keys are still pressed (polling for key release detection)
        /// </summary>
        private void CheckKeyStates(object sender, System.Timers.ElapsedEventArgs e)
        {
            var hotKeysToRelease = new List<int>();

            foreach (var kvp in _registeredHotKeys)
            {
                int hotKeyId = kvp.Key;
                var hotKeyInfo = kvp.Value;

                // Check if this hotkey is currently marked as pressed
                if (_hotKeyStates.TryGetValue(hotKeyId, out bool isPressed) && isPressed)
                {
                    // Check if the key combination is still held
                    if (!IsKeyCombinationPressed(hotKeyInfo.Modifiers, hotKeyInfo.Key))
                    {
                        hotKeysToRelease.Add(hotKeyId);
                    }
                }
            }

            // Release keys that are no longer pressed
            foreach (var hotKeyId in hotKeysToRelease)
            {
                _hotKeyStates[hotKeyId] = false;

                if (_registeredHotKeys.TryGetValue(hotKeyId, out var hotKeyInfo))
                {
                    // Call the on-key-up callback if registered
                    _window?.Dispatcher.Invoke(() =>
                    {
                        hotKeyInfo.OnKeyUp?.Invoke();
                    });

                    // Fire the released event
                    _window?.Dispatcher.Invoke(() =>
                    {
                        var args = new HotKeyEventArgs
                        {
                            HotKeyId = hotKeyId,
                            Key = hotKeyInfo.Key,
                            Modifiers = hotKeyInfo.Modifiers
                        };
                        HotKeyReleased?.Invoke(this, args);
                    });
                }
            }
        }

        /// <summary>
        /// Checks if a key combination is currently pressed
        /// </summary>
        private bool IsKeyCombinationPressed(ModifierKeys modifiers, Key key)
        {
            // Check modifiers
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) == 0)
                    return false;
            }

            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) == 0)
                    return false;
            }

            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                if ((GetAsyncKeyState(VK_MENU) & 0x8000) == 0)
                    return false;
            }

            // Check the main key
            int vkCode = KeyInterop.VirtualKeyFromKey(key);
            if ((GetAsyncKeyState(vkCode) & 0x8000) == 0)
                return false;

            return true;
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Dispose()
        {
            // Stop the key state timer
            if (_keyStateTimer != null)
            {
                _keyStateTimer.Stop();
                _keyStateTimer.Dispose();
                _keyStateTimer = null;
            }

            UnregisterAllHotKeys();

            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(HwndHook);
                _hwndSource.Dispose();
            }

            _window = null;
            _windowHandle = IntPtr.Zero;
            _hwndSource = null;
        }
    }
}
