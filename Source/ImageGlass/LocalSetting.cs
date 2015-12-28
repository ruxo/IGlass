/*
ImageGlass Project - Image viewer for Windows
Copyright (C) 2013 DUONG DIEU PHAP
Project homepage: http://imageglass.org

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

namespace ImageGlass
{
    public static class LocalSetting
    {
        static frmFacebook _fFacebook = new frmFacebook();
        static frmSetting _fSetting = new frmSetting();
        static frmExtension _fExtension = new frmExtension();

        #region "Properties"
        /// <summary>
        /// Form frmFacebook
        /// </summary>
        public static frmFacebook FFacebook
        {
            get { return LocalSetting._fFacebook; }
            set { LocalSetting._fFacebook = value; }
        }

        /// <summary>
        /// Form frmSetting
        /// </summary>
        public static frmSetting FSetting
        {
            get { return LocalSetting._fSetting; }
            set { LocalSetting._fSetting = value; }
        }

        /// <summary>
        /// Form frmExtension
        /// </summary>
        public static frmExtension FExtension
        {
            get { return LocalSetting._fExtension; }
            set { LocalSetting._fExtension = value; }
        }

        /// <summary>
        /// Gets, sets old DPI scaling value
        /// </summary>
        public static int OldDPI { get; set; } = 96;

        /// <summary>
        /// Gets, sets current DPI scaling value
        /// </summary>
        public static int CurrentDPI { get; set; } = 96;

        #endregion

    }
}



