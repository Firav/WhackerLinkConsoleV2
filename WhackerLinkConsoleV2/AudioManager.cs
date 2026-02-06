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
        private Dictionary<string, (WaveOutEvent waveOut, MixingSampleProvider mixer, BufferedWaveProvider buffer, GainSampleProvider gainProvider, MonoToStereoChannelProvider stereoProvider)> _talkgroupProviders;
        private SettingsManager _settingsManager;

        /// <summary>
        /// Creates an instance of <see cref="AudioManager"/>
        /// </summary>
        public AudioManager(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
            _talkgroupProviders = new Dictionary<string, (WaveOutEvent, MixingSampleProvider, BufferedWaveProvider, GainSampleProvider, MonoToStereoChannelProvider)>();
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

            var gainProvider = new GainSampleProvider(bufferProvider.ToSampleProvider())
            {
                Gain = 1.0f
            };

            // Convert mono to stereo with channel routing
            var stereoProvider = new MonoToStereoChannelProvider(gainProvider, stereoMode);

            var mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(8000, 2))
            {
                ReadFully = true
            };

            mixer.AddMixerInput(stereoProvider);

            waveOut.Init(mixer);
            waveOut.Play();

            _talkgroupProviders[talkgroupId] = (waveOut, mixer, bufferProvider, gainProvider, stereoProvider);
        }

        /// <summary>
        /// Adjusts the volume of a specific talkgroup stream
        /// </summary>
        public void SetTalkgroupVolume(string talkgroupId, float volume)
        {
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
