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
* Copyright (C) 2025 Caleb, K4PHP
* Copyright (C) 2026 Firav (firavdev@gmail.com)
* 
*/

using NAudio.Wave;

namespace WhackerLinkConsoleV2
{
    /// <summary>
    /// Converts a mono audio source to stereo with configurable channel routing
    /// </summary>
    public class MonoToStereoChannelProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private StereoChannelMode _channelMode;

        /// <summary>
        /// Gets the output wave format (stereo)
        /// </summary>
        public WaveFormat WaveFormat { get; }

        /// <summary>
        /// Gets or sets the stereo channel mode
        /// </summary>
        public StereoChannelMode ChannelMode
        {
            get => _channelMode;
            set => _channelMode = value;
        }

        /// <summary>
        /// Creates a new instance of MonoToStereoChannelProvider
        /// </summary>
        /// <param name="source">Mono audio source</param>
        /// <param name="channelMode">Initial stereo channel mode</param>
        public MonoToStereoChannelProvider(ISampleProvider source, StereoChannelMode channelMode = StereoChannelMode.Stereo)
        {
            if (source.WaveFormat.Channels != 1)
                throw new ArgumentException("Source must be mono", nameof(source));

            _source = source;
            _channelMode = channelMode;

            // Create stereo output format with same sample rate as source
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 2);
        }

        /// <summary>
        /// Reads samples from the source and outputs to stereo with channel routing
        /// </summary>
        public int Read(float[] buffer, int offset, int count)
        {
            // We need half as many samples from source since we're converting mono to stereo
            int sourceSamplesNeeded = count / 2;
            float[] sourceBuffer = new float[sourceSamplesNeeded];
            int samplesRead = _source.Read(sourceBuffer, 0, sourceSamplesNeeded);

            int outIndex = offset;
            for (int i = 0; i < samplesRead; i++)
            {
                float sample = sourceBuffer[i];

                switch (_channelMode)
                {
                    case StereoChannelMode.Stereo:
                        // Output to both left and right
                        buffer[outIndex++] = sample; // Left
                        buffer[outIndex++] = sample; // Right
                        break;

                    case StereoChannelMode.LeftOnly:
                        // Output to left only, silence on right
                        buffer[outIndex++] = sample; // Left
                        buffer[outIndex++] = 0f;     // Right (silent)
                        break;

                    case StereoChannelMode.RightOnly:
                        // Silence on left, output to right only
                        buffer[outIndex++] = 0f;     // Left (silent)
                        buffer[outIndex++] = sample; // Right
                        break;
                }
            }

            return samplesRead * 2; // Return number of stereo samples written
        }
    }
}
