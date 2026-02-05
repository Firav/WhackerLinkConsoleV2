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
using System.IO;

namespace WhackerLinkConsoleV2
{
    /// <summary>
    /// Utility class to generate simple PTT sound files
    /// </summary>
    public static class PttSoundGenerator
    {
        private const int SampleRate = 8000;
        private const int BitsPerSample = 16;
        private const int Channels = 1;

        /// <summary>
        /// Generate simple beep sounds for PTT down and up
        /// </summary>
        public static void GeneratePttSounds(string audioDirectory)
        {
            Directory.CreateDirectory(audioDirectory);

            string pttDownPath = Path.Combine(audioDirectory, "ptt_down.wav");
            string pttUpPath = Path.Combine(audioDirectory, "ptt_up.wav");

            // Only generate if they don't exist
            if (!File.Exists(pttDownPath))
            {
                GeneratePttDownSound(pttDownPath);
            }

            if (!File.Exists(pttUpPath))
            {
                GeneratePttUpSound(pttUpPath);
            }
        }

        /// <summary>
        /// Generate PTT down sound (subtle ascending sweep)
        /// </summary>
        private static void GeneratePttDownSound(string filePath)
        {
            const double duration = 0.12; // 120ms - longer
            const double startFreq = 900; // Start at 900 Hz
            const double endFreq = 1050; // End at 1050 Hz - subtle 150 Hz sweep

            byte[] audioData = GenerateSweepTone(startFreq, endFreq, duration);
            WriteWaveFile(filePath, audioData);
        }

        /// <summary>
        /// Generate PTT up sound (subtle descending sweep)
        /// </summary>
        private static void GeneratePttUpSound(string filePath)
        {
            const double duration = 0.12; // 120ms - longer
            const double startFreq = 1050; // Start at 1050 Hz
            const double endFreq = 900; // End at 900 Hz - subtle 150 Hz sweep

            byte[] audioData = GenerateSweepTone(startFreq, endFreq, duration);
            WriteWaveFile(filePath, audioData);
        }

        /// <summary>
        /// Generate a frequency sweep tone with subtle swooping effect
        /// </summary>
        private static byte[] GenerateSweepTone(double startFreq, double endFreq, double durationSeconds)
        {
            int sampleCount = (int)(SampleRate * durationSeconds);
            byte[] buffer = new byte[sampleCount * (BitsPerSample / 8)];

            for (int i = 0; i < sampleCount; i++)
            {
                double time = (double)i / SampleRate;
                double progress = (double)i / sampleCount;
                
                // Linear frequency sweep for subtle swooping
                double frequency = startFreq + (endFreq - startFreq) * progress;
                
                // Apply fade-in and fade-out envelope to avoid clicks
                double envelope = 1.0;
                if (progress < 0.1) // Fade in first 10%
                    envelope = progress / 0.1;
                else if (progress > 0.85) // Fade out last 15%
                    envelope = (1.0 - progress) / 0.15;
                
                // Generate tone with soft volume (0.15 = 15% of max volume)
                short sampleValue = (short)(Math.Sin(2 * Math.PI * frequency * time) * envelope * short.MaxValue * 0.15);

                buffer[i * 2] = (byte)(sampleValue & 0xFF);
                buffer[i * 2 + 1] = (byte)((sampleValue >> 8) & 0xFF);
            }

            return buffer;
        }

        /// <summary>
        /// Write PCM data to a WAV file
        /// </summary>
        private static void WriteWaveFile(string filePath, byte[] audioData)
        {
            var waveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels);
            
            using (var writer = new WaveFileWriter(filePath, waveFormat))
            {
                writer.Write(audioData, 0, audioData.Length);
            }
        }
    }
}
