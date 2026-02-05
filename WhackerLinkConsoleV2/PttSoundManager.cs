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

using NAudio.Wave;
using System;
using System.IO;

namespace WhackerLinkConsoleV2
{
    /// <summary>
    /// Manages playback of PTT sounds (down and up)
    /// </summary>
    public class PttSoundManager : IDisposable
    {
        private readonly SettingsManager _settingsManager;
        private readonly string _pttDownSoundPath;
        private readonly string _pttUpSoundPath;

        /// <summary>
        /// Creates an instance of <see cref="PttSoundManager"/>
        /// </summary>
        /// <param name="settingsManager">Settings manager instance</param>
        /// <param name="audioDirectory">Directory containing PTT sound files</param>
        public PttSoundManager(SettingsManager settingsManager, string audioDirectory = null)
        {
            _settingsManager = settingsManager;
            
            if (string.IsNullOrEmpty(audioDirectory))
            {
                audioDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio");
            }

            _pttDownSoundPath = Path.Combine(audioDirectory, "ptt_down.wav");
            _pttUpSoundPath = Path.Combine(audioDirectory, "ptt_up.wav");

            // Ensure sound files exist
            PttSoundGenerator.GeneratePttSounds(audioDirectory);
        }

        /// <summary>
        /// Play PTT down sound if enabled in settings
        /// </summary>
        public void PlayPttDownSound()
        {
            if (_settingsManager.EnablePttDownSound && File.Exists(_pttDownSoundPath))
            {
                PlaySound(_pttDownSoundPath);
            }
        }

        /// <summary>
        /// Play PTT up sound if enabled in settings
        /// </summary>
        public void PlayPttUpSound()
        {
            if (_settingsManager.EnablePttUpSound && File.Exists(_pttUpSoundPath))
            {
                PlaySound(_pttUpSoundPath);
            }
        }

        /// <summary>
        /// Play a sound file
        /// </summary>
        private void PlaySound(string filePath)
        {
            try
            {
                // Create a new WaveOutEvent for each playback to avoid conflicts
                var waveOut = new WaveOutEvent();
                var audioFileReader = new AudioFileReader(filePath);
                
                waveOut.Init(audioFileReader);
                
                // Clean up after playback completes
                waveOut.PlaybackStopped += (sender, args) =>
                {
                    waveOut.Dispose();
                    audioFileReader.Dispose();
                };
                
                waveOut.Play();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error playing PTT sound: {ex.Message}");
            }
        }

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            // Resources are disposed per-playback, nothing to clean up here
        }
    }
}
