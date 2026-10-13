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
* 
*/

using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.Generic;

namespace WhackerLinkConsoleV2
{
    /// <summary>
    /// Class for managing 
    /// </summary>
    public class AudioManager
    {
        private Dictionary<string, (WaveOutEvent waveOut, MixingSampleProvider mixer, BufferedWaveProvider buffer, GainSampleProvider gainProvider, BoostLimiterSampleProvider boostProvider, MonoToStereoChannelProvider stereoProvider)> _talkgroupProviders;
        private Dictionary<string, float> _talkgroupVolumes;
        private SettingsManager _settingsManager;

        /// <summary>
        /// Creates an instance of <see cref="AudioManager"/>
        /// </summary>
        public AudioManager(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
            _talkgroupProviders = new Dictionary<string, (WaveOutEvent, MixingSampleProvider, BufferedWaveProvider, GainSampleProvider, BoostLimiterSampleProvider, MonoToStereoChannelProvider)>();
            _talkgroupVolumes = new Dictionary<string, float>();
        }

        /// <summary>
        /// Bad name, adds samples to a provider or creates a new provider
        /// </summary>
        /// <param name="talkgroupId"></param>
        /// <param name="audioData"></param>
        public void AddTalkgroupStream(string talkgroupId, byte[] audioData)
        {
            if (!_talkgroupProviders.ContainsKey(talkgroupId))
                AddTalkgroupStream(talkgroupId);

            _talkgroupProviders[talkgroupId].buffer.AddSamples(audioData, 0, audioData.Length);
        }

        /// <summary>
        /// Internal helper to create a talkgroup stream
        /// </summary>
        /// <param name="talkgroupId"></param>
        private void AddTalkgroupStream(string talkgroupId)
        {
            int deviceIndex = _settingsManager.ChannelOutputDevices.ContainsKey(talkgroupId) ? _settingsManager.ChannelOutputDevices[talkgroupId] : 0;
            StereoChannelMode stereoMode = _settingsManager.ChannelStereoModes.ContainsKey(talkgroupId) ? _settingsManager.ChannelStereoModes[talkgroupId] : StereoChannelMode.Stereo;

            var waveOut = new WaveOutEvent
            {
                DeviceNumber = deviceIndex
            };

            var bufferProvider = new BufferedWaveProvider(new WaveFormat(8000, 16, 1))
            {
                DiscardOnBufferOverflow = true
            };

            // Reapply the volume previously set through the channel slider (in-memory only) so the
            // gain survives stream rebuilds such as output device changes
            float channelVolume = _talkgroupVolumes.TryGetValue(talkgroupId, out float savedVolume) ? savedVolume : 1.0f;

            var gainProvider = new GainSampleProvider(bufferProvider.ToSampleProvider())
            {
                Gain = channelVolume
            };

            // Loudness boost + limiting stage (full bypass when boost is 0 dB)
            var boostProvider = new BoostLimiterSampleProvider(gainProvider)
            {
                BoostDb = GetBoostFor(talkgroupId)
            };

            // Convert mono to stereo with channel routing
            var stereoProvider = new MonoToStereoChannelProvider(boostProvider, stereoMode);

            var mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(8000, 2))
            {
                ReadFully = true
            };

            mixer.AddMixerInput(stereoProvider);

            waveOut.Init(mixer);
            waveOut.Play();

            _talkgroupProviders[talkgroupId] = (waveOut, mixer, bufferProvider, gainProvider, boostProvider, stereoProvider);
        }

        /// <summary>
        /// Resolves the loudness boost (dB) for a talkgroup: per-channel override if present, otherwise the global default
        /// </summary>
        private float GetBoostFor(string talkgroupId)
        {
            return _settingsManager.ChannelBoosts.ContainsKey(talkgroupId)
                ? _settingsManager.ChannelBoosts[talkgroupId]
                : _settingsManager.DefaultChannelBoostDb;
        }

        /// <summary>
        /// Sets the loudness boost (dB) for a specific talkgroup stream
        /// </summary>
        public void SetTalkgroupBoost(string talkgroupId, float boostDb)
        {
            if (_talkgroupProviders.ContainsKey(talkgroupId))
            {
                _talkgroupProviders[talkgroupId].boostProvider.BoostDb = boostDb;
            }
            else
            {
                AddTalkgroupStream(talkgroupId);
                _talkgroupProviders[talkgroupId].boostProvider.BoostDb = boostDb;
            }
        }

        /// <summary>
        /// Adjusts the volume of a specific talkgroup stream and remembers it (in-memory) so
        /// rebuilt streams, for example after an output device change, keep the slider's level
        /// </summary>
        public void SetTalkgroupVolume(string talkgroupId, float volume)
        {
            _talkgroupVolumes[talkgroupId] = volume;

            if (_talkgroupProviders.ContainsKey(talkgroupId))
            {
                _talkgroupProviders[talkgroupId].gainProvider.Gain = volume;
            }
            else
            {
                AddTalkgroupStream(talkgroupId);
                _talkgroupProviders[talkgroupId].gainProvider.Gain = volume;
            }
        }

        /// <summary>
        /// Set stream output device
        /// </summary>
        /// <param name="talkgroupId"></param>
        /// <param name="deviceIndex"></param>
        public void SetTalkgroupOutputDevice(string talkgroupId, int deviceIndex)
        {
            if (_talkgroupProviders.ContainsKey(talkgroupId))
            {
                _talkgroupProviders[talkgroupId].waveOut.Stop();
                _talkgroupProviders.Remove(talkgroupId);
            }

            _settingsManager.UpdateChannelOutputDevice(talkgroupId, deviceIndex);
            AddTalkgroupStream(talkgroupId);
        }

        /// <summary>
        /// Set stereo channel mode for a talkgroup
        /// </summary>
        /// <param name="talkgroupId"></param>
        /// <param name="stereoMode"></param>
        public void SetTalkgroupStereoMode(string talkgroupId, StereoChannelMode stereoMode)
        {
            if (_talkgroupProviders.ContainsKey(talkgroupId))
            {
                _talkgroupProviders[talkgroupId].stereoProvider.ChannelMode = stereoMode;
            }
            
            _settingsManager.UpdateChannelStereoMode(talkgroupId, stereoMode);
        }

        /// <summary>
        /// Lop off the wave out
        /// </summary>
        public void Stop()
        {
            foreach (var provider in _talkgroupProviders.Values)
                provider.waveOut.Stop();
        }
    }
}
