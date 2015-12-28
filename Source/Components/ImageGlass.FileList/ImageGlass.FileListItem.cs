﻿/*
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
using System.Drawing;
using System.Windows.Forms;

namespace ImageGlass.FileList
{
    public partial class FileListItem : UserControl
    {
        public FileListItem()
        {
            InitializeComponent();
        }

        #region Properties
        private Color _BACKGROUND_MOUSEENTER = Color.FromArgb(237, 245, 254);

        public Color BACKGROUND_MOUSEENTER
        {
            get { return _BACKGROUND_MOUSEENTER; }
            set { _BACKGROUND_MOUSEENTER = value; }
        }
        private Color _BACKGROUND_MOUSELEAVE = Color.FromArgb(255, 255, 255);

        public Color BACKGROUND_MOUSELEAVE
        {
            get { return _BACKGROUND_MOUSELEAVE; }
            set { _BACKGROUND_MOUSELEAVE = value; }
        }
        private Color _BACKGROUND_MOUSEDOWN = Color.FromArgb(219, 236, 253);

        public Color BACKGROUND_MOUSEDOWN
        {
            get { return _BACKGROUND_MOUSEDOWN; }
            set { _BACKGROUND_MOUSEDOWN = value; }
        }

        private string _title = string.Empty;

        public string Title
        {
            get { return _title; }
            set
            {
                _title = value;
            }
        }
        private string _path = string.Empty;

        public string Path
        {
            get { return _path; }
            set
            {
                _path = value;
            }
        }
        private string _currenVersion = string.Empty;

        public string CurrenVersion
        {
            get { return _currenVersion; }
            set
            {
                _currenVersion = value;
            }
        }        
        private Bitmap _imgAvatar;

        public Bitmap ImgAvatar
        {
            get { return _imgAvatar; }
            set
            {
                _imgAvatar = value;
                picAvatar.Image = _imgAvatar;
            }
        }
        #endregion

        /// <summary>
        /// Mouse event:0 (normal), 1 (mouse enter), 2 (mouse down), 3 (mouse up), 4 (mouse leave)
        /// </summary>
        private int _mouseEvent = 0;

        #region Stylish
        
        private void FileListItem_MouseLeave(object sender, EventArgs e)
        {
            _mouseEvent = 4;
            this.Invalidate();
        }

        private void FileListItem_MouseDown(object sender, MouseEventArgs e)
        {
            _mouseEvent = 2;
            this.Invalidate();
        }

        private void FileListItem_MouseUp(object sender, MouseEventArgs e)
        {
            _mouseEvent = 3;
            this.Invalidate();
        }

        private void FileListItem_MouseEnter(object sender, EventArgs e)
        {
            _mouseEvent = 1;            
            this.Invalidate();
        }
        #endregion




        private void FileListItem_Paint(object sender, PaintEventArgs e)
        {
            if (_mouseEvent == 0)
            {
                DrawItem(BACKGROUND_MOUSELEAVE, e.Graphics);
            }
            else if (_mouseEvent == 1)
            {
                DrawItem(BACKGROUND_MOUSEENTER, e.Graphics);
            }
            else if (_mouseEvent == 2)
            {
                DrawItem(BACKGROUND_MOUSEDOWN, e.Graphics);
            }
            else if (_mouseEvent == 3)
            {
                DrawItem(BACKGROUND_MOUSEENTER, e.Graphics);
            }
            else if (_mouseEvent == 4)
            {
                DrawItem(BACKGROUND_MOUSELEAVE, e.Graphics);
            }
        }

        private void DrawItem(Color bgColor, Graphics g)
        {
            string str;

            if (_currenVersion == null)
            {
                str = _title + "\r\n" + _path;
            }
            else
            {
                str = _title + " - version: " + _currenVersion + "\r\n" + _path;
            }

            Font f = new System.Drawing.Font("sans-serif", 9);
            Brush b = Brushes.Black;
            PointF p = new PointF(51, 9);

            g.Clear(bgColor);
            g.DrawString(str, f, b, p);
            
        }

        private void FileListItem_Load(object sender, EventArgs e)
        {
            string str;

            if (_currenVersion == null)
            {
                str = _title + "\r\n" + _path;
            }
            else
            {                
                str = _title + " - version: " + _currenVersion + "\r\n" + _path;
            }

            this.tip1.SetToolTip(this, str);
        }

        
    }
}
