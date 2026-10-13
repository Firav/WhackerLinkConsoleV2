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
using System;

namespace WhackerLinkConsoleV2
{
    /// <summary>
    /// Sample provider that applies a loudness boost (in dB) followed by a
    /// peak limiter so boosted audio stays free of clipping.
    /// When the boost is 0 dB (or less) the source is passed through untouched,
    /// keeping the default pipeline behavior identical to a plain gain stage.
    /// </summary>
    public class BoostLimiterSampleProvider : ISampleProvider
    {
        /// <summary>
        /// Peak ceiling for the limiter, ~ -0.5 dBFS
        /// </summary>
        private const float Ceiling = 0.9441f;

        /// <summary>
        /// Limiter release time in milliseconds
        /// </summary>
        private const float ReleaseMs = 120f;

        private readonly ISampleProvider _source;
        private readonly float _releaseCoeff;
        private float _boostDb;
        private float _boostFactor = 1.0f;
        private float _limiterGain = 1.0f;

        /// <summary>
        /// Creates a new instance of BoostLimiterSampleProvider
        /// </summary>
        /// <param name="source">Mono audio source</param>
        /// <param name="boostDb">Initial loudness boost in dB (0 = bypass)</param>
        public BoostLimiterSampleProvider(ISampleProvider source, float boostDb = 0f)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));

            WaveFormat = source.WaveFormat;

            // One-pole smoothing coefficient for the limiter release envelope
            _releaseCoeff = (float)Math.Exp(-1.0 / (ReleaseMs * 0.001 * source.WaveFormat.SampleRate));

            BoostDb = boostDb;
        }

        public WaveFormat WaveFormat { get; }

        /// <summary>
        /// Boost in decibels applied before limiting; 0 dB is a full bypass
        /// </summary>
        public float BoostDb
        {
            get => _boostDb;
            set
            {
                _boostDb = Math.Max(0f, value);
                _boostFactor = (float)Math.Pow(10.0, _boostDb / 20.0);
            }
        }

        /// <summary>
        /// Reads samples from the source, applies the boost and limits peaks to the ceiling
        /// </summary>
        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = _source.Read(buffer, offset, count);

            // Bypass: identical output to the existing pipeline when no boost is applied
            if (_boostFactor <= 1.0f)
                return samplesRead;

            for (int i = 0; i < samplesRead; i++)
            {
                float boosted = buffer[offset + i] * _boostFactor;
                float abs = Math.Abs(boosted);

                // Gain reduction needed to keep this sample under the ceiling
                float targetGain = abs > Ceiling ? Ceiling / abs : 1.0f;

                // Instant attack, smoothed release to avoid pumping
                _limiterGain = targetGain < _limiterGain
                    ? targetGain
                    : _releaseCoeff * _limiterGain + (1.0f - _releaseCoeff) * targetGain;

                buffer[offset + i] = boosted * _limiterGain;
            }

            return samplesRead;
        }
    }
}