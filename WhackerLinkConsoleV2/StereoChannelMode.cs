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

namespace WhackerLinkConsoleV2
{
    /// <summary>
    /// Defines which stereo channel(s) to output audio to
    /// </summary>
    public enum StereoChannelMode
    {
        /// <summary>
        /// Output to both left and right channels (default stereo)
        /// </summary>
        Stereo,

        /// <summary>
        /// Output only to the left channel
        /// </summary>
        LeftOnly,

        /// <summary>
        /// Output only to the right channel
        /// </summary>
        RightOnly
    }
}
