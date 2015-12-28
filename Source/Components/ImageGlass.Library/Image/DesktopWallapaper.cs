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

using System;
using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using ImageGlass.Common;

namespace ImageGlass.Library.Image
{
    public static class DesktopWallapaper
    {
        const int SPI_SETDESKWALLPAPER = 20;
        const int SPIF_UPDATEINIFILE = 0x01;
        const int SPIF_SENDWININICHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        public enum Style : int
        {
            /// <summary>
            /// 0
            /// </summary>
            Centered = 0,
            /// <summary>
            /// 1
            /// </summary>
            Stretched = 1,
            /// <summary>
            /// 2
            /// </summary>
            Tiled = 2
        }

        /// <summary>
        /// Set desktop wallpaper
        /// </summary>
        /// <param name="uri">Image filename</param>
        /// <param name="style">Style of wallpaper</param>
        public static void Set(Uri uri, Style style)
        {
            Set(loadImage(uri), style);
        }
        public static void Set(System.Drawing.Image img, Style style) {
            var key = Option<RegistryKey>.From(() => Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true));
            key.Do(k => {
                if (style == Style.Stretched) {
                    k.SetValue(@"WallpaperStyle", "2");
                    k.SetValue(@"TileWallpaper", "0");
                }

                if (style == Style.Centered) {
                    k.SetValue(@"WallpaperStyle", "1");
                    k.SetValue(@"TileWallpaper", "0");
                }

                if (style == Style.Tiled) {
                    k.SetValue(@"WallpaperStyle", "1");
                    k.SetValue(@"TileWallpaper", "1");
                }
                k.Dispose();
            });

            SystemParametersInfo(SPI_SETDESKWALLPAPER,
                                    0,
                                    saveTempImage(img, "imageglass.jpg", ImageFormat.Jpeg),
                                    SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
        }

        static System.Drawing.Image loadImage(Uri uri) {
            using(var s = new System.Net.WebClient().OpenRead(uri.ToString()))
                return System.Drawing.Image.FromStream(s);
        }
        static string saveTempImage(System.Drawing.Image img, string filename = "wallpaper.bmp", ImageFormat format = null) {
            string tempPath = Path.Combine(Path.GetTempPath(), filename);
            img.Save(tempPath, format ?? ImageFormat.Bmp);
            return tempPath;
        }
    }
}
