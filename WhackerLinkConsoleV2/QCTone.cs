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
* Copyright (C) 2024 Caleb, K4PHP
* 
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace WhackerLinkConsoleV2.Controls
{
    /// <summary>
    /// Quick Call II tone widget that is generated from the codeplug qcList section.
    /// Displayed, positioned and persisted exactly like wav file alert tones,
    /// but instead of playing a wav file it emits the tone A/B pair from the codeplug.
    /// </summary>
    public class QCTone : AlertTone
    {
        /// <summary>
        /// Tone A frequency (Hz)
        /// </summary>
        public double ToneAFrequency { get; }

        /// <summary>
        /// Tone B frequency (Hz)
        /// </summary>
        public double ToneBFrequency { get; }

        /// <summary>
        /// Key used to persist this tones canvas position
        /// </summary>
        public string PositionKey
        {
            get { return AlertFilePath; }
        }

        /// <summary>
        /// Creates an instance of <see cref="QCTone"/>
        /// </summary>
        /// <param name="toneAFrequency"></param>
        /// <param name="toneBFrequency"></param>
        /// <param name="name"></param>
        public QCTone(double toneAFrequency, double toneBFrequency, string name = null)
            : base(QCTonePair.BuildPositionKey(toneAFrequency, toneBFrequency, name), QCTonePair.BuildDisplayName(toneAFrequency, toneBFrequency, name))
        {
            ToneAFrequency = toneAFrequency;
            ToneBFrequency = toneBFrequency;

            ToolTip = string.Format(CultureInfo.InvariantCulture,
                "QC2 tones, A: {0:F1} Hz, B: {1:F1} Hz", toneAFrequency, toneBFrequency);
        }
    }

    /// <summary>
    /// Codeplug qcList entry, a Quick Call II A/B tone pair
    /// </summary>
    public class QCTonePair
    {
        /// <summary>
        /// Tone A frequency (Hz)
        /// </summary>
        public double A { get; set; }

        /// <summary>
        /// Tone B frequency (Hz)
        /// </summary>
        public double B { get; set; }

        /// <summary>
        /// Optional display name for the tone
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Builds the widget display name
        /// </summary>
        public static string BuildDisplayName(double toneA, double toneB, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Format(CultureInfo.InvariantCulture, "QC2 {0:F1}/{1:F1}", toneA, toneB);

            return name.Trim();
        }

        /// <summary>
        /// Builds the widget position key used with the saved tone positions
        /// </summary>
        public static string BuildPositionKey(double toneA, double toneB, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Format(CultureInfo.InvariantCulture, "qc:{0:F1}/{1:F1}", toneA, toneB);

            return "qc:" + name.Trim();
        }
    }

    /// <summary>
    /// Helper used to deserialize the codeplug qcList section, the
    /// WhackerLinkLib Codeplug model deliberately omits QC tones so the
    /// console parses them separately
    /// </summary>
    public class QcToneDocument
    {
        /// <summary>
        /// Quick Call II tone pairs
        /// </summary>
        public List<QCTonePair> QcList { get; set; }
    }
}