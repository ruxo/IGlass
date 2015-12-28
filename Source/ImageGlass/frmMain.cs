/*
ImageGlass Project - Image viewer for Windows
Copyright (C) 2015 DUONG DIEU PHAP
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using ImageGlass.Core;
using ImageGlass.Library.Image;
using ImageGlass.Library.Comparer;
using System.IO;
using System.Diagnostics;
using ImageGlass.Services.Configuration;
using ImageGlass.Library;
using System.Collections.Specialized;
using System.Diagnostics.Contracts;
using ImageGlass.Services.InstanceManagement;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ImageGlass.Common;
using ImageGlass.Common.Extensions;
using System.Threading;

namespace ImageGlass {
    [StructLayout(LayoutKind.Auto)]
    struct FileDesc{
        public Option<string> File;
        public string Location;

        public static Option<FileDesc> Verify(string path){
            Contract.Requires(!string.IsNullOrWhiteSpace(path));
            Contract.Ensures(Contract.Result<Option<FileDesc>>() != null);

            if (System.IO.File.Exists(path))
                return Option<FileDesc>.Some(new FileDesc{
                    File = Option<string>.Some(Path.GetFileName(path)),
                    Location = Path.GetFullPath(Path.GetDirectoryName(path))
                });
            else if (Directory.Exists(path))
                return Option<FileDesc>.Some(new FileDesc{
                    File = Option<string>.None(),
                    Location = Path.GetFullPath(path)
                });
            else
                return Option<FileDesc>.None();
        }
    }

    public partial class frmMain : Form{
        public frmMain()
        {
            InitializeComponent();
            cache = new ImgCache(diskManager);
            thumbnailBar.DiskManager = diskManager;
            mnuMain.Renderer = mnuPopup.Renderer = new Theme.ModernMenuRenderer();

            //Check and perform DPI Scaling
            LocalSetting.OldDPI = LocalSetting.CurrentDPI;
            LocalSetting.CurrentDPI = Theme.DPIScaling.CalculateCurrentDPI(this);
            Theme.DPIScaling.HandleDpiChanged(LocalSetting.OldDPI, LocalSetting.CurrentDPI, this);
        }


        #region Local variables
        string _imageInfo = "";

        // window size value before resizing
        Size _windowSize = new Size(600, 500);

        // determine if the image is zoomed
        bool _isZoomed;

        //determine if toolbar is shown
        bool _isShownToolbar = true;

        bool _fullScreen;
        readonly ImgCache cache;
        readonly List<string> imageList = new List<string>();
        Option<string> _imageModifiedPath = Option<string>.None();

        int currentIndex = -1;
        readonly DiskManager diskManager = new DiskManager();
        #endregion

        string CurrentFilePath => imageList.Count > 0  && currentIndex >= 0? imageList[currentIndex] : string.Empty;
        string CurrentFileName => Path.GetFileName(CurrentFilePath);

        #region Drag - drop
        void picMain_DragOver(object sender, DragEventArgs e) => e.Effect = DragDropEffects.All;

        async void picMain_DragDrop(object sender, DragEventArgs e) {
            await Prepare(((string[])e.Data.GetData(DataFormats.FileDrop))[0]);
        }
        #endregion



        #region Preparing image
        /// <summary>
        /// Open an image
        /// </summary>
        async Task OpenFile()
        {
            using (var o = new OpenFileDialog()) {
                o.Filter = GlobalSetting.LangPack.Items["frmMain._OpenFileDialog"] + "|" +
                            GlobalSetting.SupportedExtensions;

                if (o.ShowDialog() == DialogResult.OK && File.Exists(o.FileName))
                    await Prepare(o.FileName);
            }
        }

        /// <summary>
        /// Prepare to load image
        /// </summary>
        /// <param name="path">Path</param>
        async Task Prepare(string path){
            //Reset current index
            currentIndex = 0;

            var fileOpt = FileDesc.Verify(path);
            if (fileOpt.IsNone)
                return;

            var file = fileOpt.Get();

            imageList.Clear();
            imageList.AddRange(LoadImageFilesFromDirectory(file.Location));

            LoadThumbnails();

            file.File
                .Do(initFile =>{
                    var fileIndex = imageList.IndexOf(path);
                    currentIndex = fileIndex < 0
                        ? Math.Min(imageList.Count -1, 0)
                        : fileIndex;

                    if (currentIndex == -1){
                        GlobalSetting.IsImageError = true;
                        Text = "ImageGlass - " + initFile;
                        lblInfo.Text = ImageInfo.GetFileSize(initFile);
                        picMain.Text = GlobalSetting.LangPack.Items["frmMain.picMain._ErrorText"];
                        picMain.Image = null;
                    }
                });
            await NextPic(0);

            //Watch all change of current path
            sysWatch.Path = file.Location;
            sysWatch.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Select current thumbnail
        /// </summary>
        private void SelectCurrentThumbnail() {
            if (thumbnailBar.Items.Count > 0) {
                thumbnailBar.ClearSelection();
                thumbnailBar.Items[currentIndex].Selected = true;
                thumbnailBar.Items[currentIndex].Focused = true;
                thumbnailBar.EnsureVisible(currentIndex);
            }
        }

        /// <summary>
        /// Sort and find all supported image from directory
        /// </summary>
        /// <param name="path">Image folder path</param>
        static string[] LoadImageFilesFromDirectory(string path)
        {
            //Get files from dir
            var dsFile = DirectoryFinder.FindFiles(path,
                GlobalSetting.IsRecursive,
                f => {
                    var extension = Path.GetExtension(f);
                    return extension != null && GlobalSetting.SupportedExtensions.Contains(extension.ToLower());
                });

            //Load image order from config
            var orderBy = GlobalSetting.LoadImageOrderConfig();

            //Sort image file
            IEnumerable<string> result;
            switch (orderBy){
                case ImageOrderBy.Name:
                    var files = dsFile.ToArray();
                    Array.Sort(files, new NumericComparer());
                    result = files;
                    break;
                case ImageOrderBy.Length:
                    result = dsFile.OrderBy(f => new FileInfo(f).Length);
                    break;
                case ImageOrderBy.CreationTime:
                    result = dsFile.OrderBy(File.GetCreationTimeUtc);
                    break;
                case ImageOrderBy.Extension:
                    result = dsFile.OrderBy(Path.GetExtension);
                    break;
                case ImageOrderBy.LastAccessTime:
                    result = dsFile.OrderBy(File.GetLastAccessTimeUtc);
                    break;
                case ImageOrderBy.LastWriteTime:
                    result = dsFile.OrderBy(File.GetLastWriteTimeUtc);
                    break;
                case ImageOrderBy.Random:
                    result = dsFile.OrderBy(f => Guid.NewGuid());
                    break;
                default:
                    result = dsFile;
                    break;
            }
            return result.ToArray();
        }

        /// <summary>
        /// Clear and reload all thumbnail image
        /// </summary>
        private void LoadThumbnails()
        {
            thumbnailBar.Items.Clear();

            imageList
                .Select(file => new ImageListView.ImageListViewItem(file) { Tag = file })
                .ForEach(thumbnailBar.Items.Add);
        }

        int ValidateIndex(int index){
            var valid = index%imageList.Count;
            return valid < 0 ? (imageList.Count + valid) : valid;
        }

        const int PrefetchSpanNumber = 3;
        void PrefetchImages(int index){
            if (imageList.Count == 0)
                return;

            var min = GlobalSetting.IsImageBoosterBack ? index - PrefetchSpanNumber : index;
            var max = index + PrefetchSpanNumber;
            cache.Preload(Enumerable
                              .Range(min, max - min + 1)
                              .Where(i => i != index)
                              .Select(ValidateIndex)
                              .Select(i => imageList[i])
                );
        }

        /// <summary>
        /// Change image
        /// </summary>
        /// <param name="step">Image step to change. Zero is reload the current image.</param>
        async Task NextPic(int step){
            if (inCropMode)
                ReleaseCropMode();

            if (imageList.Count < 1) {
                this.Text = "ImageGlass";
                lblInfo.Text = string.Empty;

                GlobalSetting.IsImageError = true;
                picMain.Image = null;
                return;
            }

            PrefetchImages(currentIndex + step);

            await ImageSaveChange();

            DisplayTextMessage(GlobalSetting.LangPack.Items["frmMain._Loading"], 5000);

            picMain.Text = "";
            GlobalSetting.IsTempMemoryData = false;

            //Update current index
            currentIndex += step;

            //Check if current index is greater than upper limit
            if (currentIndex >= imageList.Count) currentIndex = 0;

            //Check if current index is less than lower limit
            if (currentIndex < 0) currentIndex = imageList.Count - 1;

            var image = await cache.GetImage(CurrentFilePath);

            GlobalSetting.IsImageError = image.IsLeft;
            if (image.IsLeft && !File.Exists(CurrentFilePath)) {
                imageList.RemoveAt(currentIndex);

                picMain.Image = null;
                picMain.Text = GlobalSetting.LangPack.Items["frmMain.picMain._ErrorText"];
                SelectCurrentThumbnail();
            } else {
                //Show image
                picMain.Image = image.Get(_ => new Bitmap(1, 1), Prelude.Identity);
                mnuMainRefresh_Click(null, null);
                SelectCurrentThumbnail();
            }
        }
        /// <summary>
        /// Update image information on status bar
        /// </summary>
        private void UpdateStatusBar(bool @zoomOnly = false)
        {
            string fileinfo = "";

            if (imageList.Count < 1)
            {
                this.Text = "ImageGlass";
                lblInfo.Text = fileinfo;
                return;
            }

            //Set the text of Window title
            this.Text = "ImageGlass - " +
                        (currentIndex + 1) + "/" + imageList.Count + " " +
                        GlobalSetting.LangPack.Items["frmMain._Text"] + " - " +
                        CurrentFileName;

            if (GlobalSetting.IsImageError)
            {
                try
                {
                    fileinfo = ImageInfo.GetFileSize(CurrentFilePath) + "\t  |  ";
                    fileinfo += Path.GetExtension(CurrentFileName).Replace(".", "").ToUpper() + "  |  ";
                    fileinfo += File.GetCreationTime(CurrentFilePath).ToString("yyyy/M/d HH:m:s");
                    this._imageInfo = fileinfo;
                }
                catch { fileinfo = ""; }
            }
            else
            {
                fileinfo += picMain.Image.Width + " x " + picMain.Image.Height + " px  |  ";

                if (zoomOnly)
                {
                    fileinfo = picMain.Zoom.ToString() + "%  |  " + _imageInfo;
                }
                else
                {
                    fileinfo += ImageInfo.GetFileSize(CurrentFilePath) + "\t  |  ";
                    fileinfo += File.GetCreationTime(CurrentFilePath).ToString("yyyy/M/d HH:m:s");

                    this._imageInfo = fileinfo;

                    fileinfo = picMain.Zoom.ToString() + "%  |  " + fileinfo;
                }
            }

            //Move image information to Window title
            this.Text += "  |  " + fileinfo;

        }
        #endregion

        #region Key event
        private void frmMain_KeyDown(object sender, KeyEventArgs e)
        {
            //this.Text = e.KeyValue.ToString();
            if (KeyEvent.Key(e, Keys.Oemtilde))
                mnuMain.Show(picMain, 0, picMain.Top);

            // Rotation Counterclockwise----------------------------------------------------
            if (KeyEvent.CtrlKey(e, Keys.Oemcomma))
                mnuMainRotateCounterclockwise_Click(null, null);

            //Rotate Clockwise--------------------------------------------------------------
            if (KeyEvent.CtrlKey(e, Keys.OemPeriod))
                mnuMainRotateClockwise_Click(null, null);

            //ESC ultility------------------------------------------------------------------
            if (KeyEvent.Key(e, Keys.Escape))
            {
                //exit slideshow
                if (GlobalSetting.IsPlaySlideShow) {
                    mnuMainSlideShowExit_Click(null, null);
                }
                //exit full screen
                else if (_fullScreen) {
                    fullScreen();
                } else if (inCropMode)
                    ReleaseCropMode();
                //Quit ImageGlass
                else if (GlobalSetting.IsPressESCToQuit) {
                    Application.Exit();
                }
                return;
            }


            //Clear clipboard----------------------------------------------------------------
            if (KeyEvent.CtrlKey(e, Keys.Oemtilde))
                mnuMainClearClipboard_Click(null, null);

            //Start / stop slideshow---------------------------------------------------------
            if (KeyEvent.Key(e, Keys.Space) && GlobalSetting.IsPlaySlideShow)//SPACE
                mnuMainSlideShowPause_Click(null, null);

#pragma warning disable 4014
            //Previous Image----------------------------------------------------------------
            if (KeyEvent.Key(e, Keys.PageUp, Keys.Left))
                NextPic(-1);

            //Next Image---------------------------------------------------------------------
            if (KeyEvent.Key(e, Keys.PageDown, Keys.Right))
                NextPic(1);
#pragma warning restore 4014

            //Goto the first Image---------------------------------------------------------------
            if (KeyEvent.Key(e, Keys.Home))
                mnuMainGotoFirst_Click(null, e);

            //Goto the last Image---------------------------------------------------------------
            if (KeyEvent.Key(e, Keys.End))
                mnuMainGotoLast_Click(null, e);

            //Zoom + ------------------------------------------------------------------------
            if (KeyEvent.CtrlKey(e, Keys.Oemplus))
                btnZoomIn_Click(null, null);

            //Zoom - ------------------------------------------------------------------------
            if (KeyEvent.CtrlKey(e, Keys.OemMinus))
                btnZoomOut_Click(null, null);

            //Actual size image -------------------------------------------------------------
            if (KeyEvent.CtrlKey(e, Keys.D0))
                btnActualSize_Click(null, null);

            //Full screen--------------------------------------------------------------------
            if (KeyEvent.Key(e, Keys.Enter))
                fullScreen();
            
            if (inCropMode)
            {
                if (KeyEvent.Key(e, Keys.A))
                    CropModeSaveAs();
                else if (KeyEvent.Key(e, Keys.S))
                    CropModeSave();
                else if (KeyEvent.Key(e, Keys.X))
                    ReleaseCropMode();
                else if (KeyEvent.Key(e, Keys.R))
                    ResetCropMode();
            }
        }

        #endregion



        #region Private functions
        
        /// <summary>
        /// Rename image
        /// </summary>
        /// <param name="oldFilename"></param>
        /// <param name="newname"></param>
        private void RenameImage()
        {
            if (GlobalSetting.IsImageError || !File.Exists(CurrentFilePath))
                return;

            //Lay ten file
            string oldName;
            string newName;
            oldName = newName = CurrentFileName;
            string currentPath = Path.GetDirectoryName(CurrentFilePath);

            //Lay ext
            string ext = newName.Substring(newName.LastIndexOf(".", StringComparison.Ordinal));
            newName = newName.Substring(0, newName.Length - ext.Length);

            //Hien input box
            string str = null;
            if (InputBox.ShowDiaLog("Rename", GlobalSetting.LangPack.Items["frmMain._RenameDialog"],
                                    newName) == System.Windows.Forms.DialogResult.OK)
            {
                str = InputBox.Message;
            }
            if (str == null)
                return;

            newName = str + ext;
            //Neu ten giong nhau thi return;
            if (oldName == newName)
                return;

            try
            {
                //Doi ten tap tin
                ImageInfo.RenameFile(Path.Combine(currentPath, oldName), Path.Combine(currentPath, newName));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Display a message on picture box
        /// </summary>
        /// <param name="msg">Message</param>
        /// <param name="duration">Duration (milisecond)</param>
        void DisplayTextMessage(string msg, int duration) {
            Contract.Requires(!string.IsNullOrEmpty(msg));
            Contract.Requires(duration > 0);

            var tmsg = new System.Windows.Forms.Timer {
                Enabled = false,
                Interval = duration   //display in xxx mili seconds
            };
            tmsg.Tick += delegate {
                tmsg.Stop();
                clearTextMessage();
            };

            using (var font = new Font(Font.FontFamily, 12)){
                picMain.TextBackColor = Color.Black;
                picMain.Font = font;
                picMain.ForeColor = Color.White;
                picMain.Text = msg;
            }
            tmsg.Start();
        }
        void clearTextMessage(){
            picMain.TextBackColor = Color.Transparent;
            picMain.Font = this.Font;
            picMain.ForeColor = Color.Black;
            picMain.Text = string.Empty;
        }
        private void CopyFile()
        {
            if (GlobalSetting.IsImageError || !File.Exists(CurrentFilePath))
                return;

            GlobalSetting.StringClipboard = new StringCollection();
            GlobalSetting.StringClipboard.Add(CurrentFilePath);
            Clipboard.SetFileDropList(GlobalSetting.StringClipboard);

            this.DisplayTextMessage(
                string.Format(GlobalSetting.LangPack.Items["frmMain._CopyFileText"],
                GlobalSetting.StringClipboard.Count), 1000);
        }

        private void CopyMultiFile()
        {
            if (GlobalSetting.IsImageError || !File.Exists(CurrentFilePath))
                return;

            string filename = CurrentFilePath;

            //exit if duplicated filename
            if (GlobalSetting.StringClipboard.IndexOf(filename) != -1)
            {
                return;
            }

            //add filename to clipboard
            GlobalSetting.StringClipboard.Add(filename);
            Clipboard.SetFileDropList(GlobalSetting.StringClipboard);

            this.DisplayTextMessage(
                string.Format(GlobalSetting.LangPack.Items["frmMain._CopyFileText"],
                GlobalSetting.StringClipboard.Count), 1000);
        }

        private void CutFile()
        {
            if (GlobalSetting.IsImageError || !File.Exists(CurrentFilePath))
                return;

            GlobalSetting.StringClipboard = new StringCollection();
            GlobalSetting.StringClipboard.Add(CurrentFilePath);

            byte[] moveEffect = new byte[] { 2, 0, 0, 0 };
            MemoryStream dropEffect = new MemoryStream();
            dropEffect.Write(moveEffect, 0, moveEffect.Length);

            DataObject data = new DataObject();
            data.SetFileDropList(GlobalSetting.StringClipboard);
            data.SetData("Preferred DropEffect", dropEffect);

            Clipboard.Clear();
            Clipboard.SetDataObject(data, true);

            this.DisplayTextMessage(
                string.Format(GlobalSetting.LangPack.Items["frmMain._CutFileText"],
                GlobalSetting.StringClipboard.Count), 1000);
        }

        private void CutMultiFile()
        {
            if (GlobalSetting.IsImageError || !File.Exists(CurrentFilePath))
                return;

            string filename = CurrentFilePath;

            //exit if duplicated filename
            if (GlobalSetting.StringClipboard.IndexOf(filename) != -1)
            {
                return;
            }

            //add filename to clipboard
            GlobalSetting.StringClipboard.Add(filename);

            byte[] moveEffect = new byte[] { 2, 0, 0, 0 };
            MemoryStream dropEffect = new MemoryStream();
            dropEffect.Write(moveEffect, 0, moveEffect.Length);

            DataObject data = new DataObject();
            data.SetFileDropList(GlobalSetting.StringClipboard);
            data.SetData("Preferred DropEffect", dropEffect);

            Clipboard.Clear();
            Clipboard.SetDataObject(data, true);

            this.DisplayTextMessage(
                string.Format(GlobalSetting.LangPack.Items["frmMain._CutFileText"],
                GlobalSetting.StringClipboard.Count), 1000);
        }

        /// <summary>
        /// Save all change of image
        /// </summary>
        Task ImageSaveChange(){
            return _imageModifiedPath
                .Get(() => Task.FromResult(0),
                     path =>{
                         _imageModifiedPath = Option<string>.None();
                         DisplayTextMessage(GlobalSetting.LangPack.Items["frmMain._SaveChanges"], 1000);
                         var saveImage = (Image) picMain.Image.Clone();
                         var saveImageTask = Task.Run(() => ImageInfo.SaveImage(saveImage, path));
                         saveImageTask.ContinueWith(_ => saveImage.Dispose());
                         return saveImageTask;
                     });
        }

        void markDirty(){
            // NOTE what if _imageModifiedPath is existed and is different from the current?? is it possible?
            _imageModifiedPath = Option<string>.Some(CurrentFilePath);
        }

        void fullScreen(){
            //full screen
            if (!_fullScreen)
            {
                saveConfig();

                //save last state of toolbar
                this._isShownToolbar = GlobalSetting.IsShowToolBar;

                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Normal;
                _fullScreen = true;
                this.Bounds = Screen.FromControl(this).Bounds;

                //Hide
                toolMain.Visible = false;
                GlobalSetting.IsShowToolBar = false;
                mnuMainToolbar.Checked = false;

                DisplayTextMessage(GlobalSetting.LangPack.Items["frmMain._FullScreenMessage"] , 5000);
            }
            //exit full screen
            else
            {
                //restore last state of toolbar
                GlobalSetting.IsShowToolBar = this._isShownToolbar;

                this.FormBorderStyle = FormBorderStyle.Sizable;

                var config = loadConfig();
                WindowState = config.State;
                Bounds = config.WindowsBound.Get(Prelude.Constant(new Rectangle(280, 125, 750, 545)), Prelude.Identity);

                _fullScreen = false;

                toolMain.Visible = GlobalSetting.IsShowToolBar;
                mnuMainToolbar.Checked = GlobalSetting.IsShowToolBar;
            }
        }

        void generateThumbnail(){
            GlobalSetting.IsShowThumbnail = !GlobalSetting.IsShowThumbnail;
            sp1.Panel2Collapsed = !GlobalSetting.IsShowThumbnail;
            btnThumb.Checked = GlobalSetting.IsShowThumbnail;

            if (GlobalSetting.IsShowThumbnail)
            {
                //hien
                sp1.Panel2MinSize = GlobalSetting.ThumbnailDimension + 40;
                sp1.SplitterDistance = sp1.Height - GlobalSetting.ThumbnailDimension - 41;
            }

            mnuMainThumbnailBar.Checked = GlobalSetting.IsShowThumbnail;
            SelectCurrentThumbnail();
        }
        #endregion

        #region Configurations
        /// <summary>
        /// Load default theme
        /// </summary>
        private void LoadThemeDefault()
        {
            // <main>
            toolMain.BackgroundImage = ImageGlass.Properties.Resources.topbar;
            thumbnailBar.BackgroundImage = ImageGlass.Properties.Resources.bottombar;
            lblInfo.ForeColor = Color.Black;

            picMain.BackColor = this.BackColor;

            // <toolbar_icon>
            btnBack.Image = ImageGlass.Properties.Resources.back;
            btnNext.Image = ImageGlass.Properties.Resources.next;
            btnRotateLeft.Image = ImageGlass.Properties.Resources.leftrotate;
            btnRotateRight.Image = ImageGlass.Properties.Resources.rightrotate;
            btnZoomIn.Image = ImageGlass.Properties.Resources.zoomin;
            btnZoomOut.Image = ImageGlass.Properties.Resources.zoomout;
            btnActualSize.Image = ImageGlass.Properties.Resources.scaletofit;
            btnZoomLock.Image = ImageGlass.Properties.Resources.zoomlock;
            btnScaletoWidth.Image = ImageGlass.Properties.Resources.scaletowidth;
            btnScaletoHeight.Image = ImageGlass.Properties.Resources.scaletoheight;
            btnWindowAutosize.Image = ImageGlass.Properties.Resources.autosizewindow;
            btnOpen.Image = ImageGlass.Properties.Resources.open;
            btnRefresh.Image = ImageGlass.Properties.Resources.refresh;
            btnGoto.Image = ImageGlass.Properties.Resources.gotoimage;
            btnThumb.Image = ImageGlass.Properties.Resources.thumbnail;
            btnCheckedBackground.Image = ImageGlass.Properties.Resources.background;
            btnFullScreen.Image = ImageGlass.Properties.Resources.fullscreen;
            btnSlideShow.Image = ImageGlass.Properties.Resources.slideshow;
            btnConvert.Image = ImageGlass.Properties.Resources.convert;
            btnPrintImage.Image = ImageGlass.Properties.Resources.printer;
            btnFacebook.Image = ImageGlass.Properties.Resources.uploadfb;
            btnExtension.Image = ImageGlass.Properties.Resources.extension;
            btnSetting.Image = ImageGlass.Properties.Resources.settings;
            btnHelp.Image = ImageGlass.Properties.Resources.about;
            btnMenu.Image = ImageGlass.Properties.Resources.menu;

            GlobalSetting.SetConfig("Theme", "default");
        }


        /// <summary>
        /// Apply changing theme
        /// </summary>
        private void LoadTheme()
        { 
            string themeFile = GlobalSetting.GetConfig("Theme", "default");

            if (File.Exists(themeFile))
            {
                Theme.Theme t = new Theme.Theme(themeFile);
                string dir = (Path.GetDirectoryName(themeFile) + "\\").Replace("\\\\", "\\");

                // <main>
                try { toolMain.BackgroundImage = Image.FromFile(dir + t.topbar); }
                catch { toolMain.BackgroundImage = ImageGlass.Properties.Resources.topbar; }

                try { thumbnailBar.BackgroundImage = Image.FromFile(dir + t.bottombar); }
                catch { thumbnailBar.BackgroundImage = ImageGlass.Properties.Resources.bottombar; }

                try
                {
                    lblInfo.ForeColor = t.statuscolor;
                }
                catch
                {
                    lblInfo.ForeColor = Color.White;
                }


                try
                {
                    picMain.BackColor = t.backcolor;
                    GlobalSetting.BackgroundColor = t.backcolor;
                }
                catch
                {
                    picMain.BackColor = Color.White;
                    GlobalSetting.BackgroundColor = Color.White;
                }


                // <toolbar_icon>
                try { btnBack.Image = Image.FromFile(dir + t.back); }
                catch { btnBack.Image = ImageGlass.Properties.Resources.back; }

                try { btnNext.Image = Image.FromFile(dir + t.next); }
                catch { btnNext.Image = ImageGlass.Properties.Resources.next; }

                try { btnRotateLeft.Image = Image.FromFile(dir + t.leftrotate); }
                catch { btnRotateLeft.Image = ImageGlass.Properties.Resources.leftrotate; }

                try { btnRotateRight.Image = Image.FromFile(dir + t.rightrotate); }
                catch { btnRotateRight.Image = ImageGlass.Properties.Resources.rightrotate; }

                try { btnZoomIn.Image = Image.FromFile(dir + t.zoomin); }
                catch { btnZoomIn.Image = ImageGlass.Properties.Resources.zoomin; }

                try { btnZoomOut.Image = Image.FromFile(dir + t.zoomout); }
                catch { btnZoomOut.Image = ImageGlass.Properties.Resources.zoomout; }

                try { btnActualSize.Image = Image.FromFile(dir + t.scaletofit); }
                catch { btnActualSize.Image = ImageGlass.Properties.Resources.scaletofit; }

                try { btnZoomLock.Image = Image.FromFile(dir + t.zoomlock); }
                catch { btnZoomLock.Image = ImageGlass.Properties.Resources.zoomlock; }

                try { btnScaletoWidth.Image = Image.FromFile(dir + t.scaletowidth); }
                catch { btnScaletoWidth.Image = ImageGlass.Properties.Resources.scaletowidth; }

                try { btnScaletoHeight.Image = Image.FromFile(dir + t.scaletoheight); }
                catch { btnScaletoHeight.Image = ImageGlass.Properties.Resources.scaletoheight; }

                try { btnWindowAutosize.Image = Image.FromFile(dir + t.autosizewindow); }
                catch { btnWindowAutosize.Image = ImageGlass.Properties.Resources.autosizewindow; }

                try { btnOpen.Image = Image.FromFile(dir + t.open); }
                catch { btnOpen.Image = ImageGlass.Properties.Resources.open; }

                try { btnRefresh.Image = Image.FromFile(dir + t.refresh); }
                catch { btnRefresh.Image = ImageGlass.Properties.Resources.refresh; }

                try { btnGoto.Image = Image.FromFile(dir + t.gotoimage); }
                catch { btnGoto.Image = ImageGlass.Properties.Resources.gotoimage; }

                try { btnThumb.Image = Image.FromFile(dir + t.thumbnail); }
                catch { btnThumb.Image = ImageGlass.Properties.Resources.thumbnail; }

                try { btnCheckedBackground.Image = Image.FromFile(dir + t.checkBackground); }
                catch { btnCheckedBackground.Image = ImageGlass.Properties.Resources.background; }

                try { btnFullScreen.Image = Image.FromFile(dir + t.fullscreen); }
                catch { btnFullScreen.Image = ImageGlass.Properties.Resources.fullscreen; }

                try { btnSlideShow.Image = Image.FromFile(dir + t.slideshow); }
                catch { btnSlideShow.Image = ImageGlass.Properties.Resources.slideshow; }

                try { btnConvert.Image = Image.FromFile(dir + t.convert); }
                catch { btnConvert.Image = ImageGlass.Properties.Resources.convert; }

                try { btnPrintImage.Image = Image.FromFile(dir + t.print); }
                catch { btnPrintImage.Image = ImageGlass.Properties.Resources.printer; }

                try { btnFacebook.Image = Image.FromFile(dir + t.uploadfb); }
                catch { btnFacebook.Image = ImageGlass.Properties.Resources.uploadfb; }

                try { btnExtension.Image = Image.FromFile(dir + t.extension); }
                catch { btnExtension.Image = ImageGlass.Properties.Resources.extension; }

                try { btnSetting.Image = Image.FromFile(dir + t.settings); }
                catch { btnSetting.Image = ImageGlass.Properties.Resources.settings; }

                try { btnHelp.Image = Image.FromFile(dir + t.about); }
                catch { btnHelp.Image = ImageGlass.Properties.Resources.about; }

                try { btnMenu.Image = Image.FromFile(dir + t.menu); }
                catch { btnMenu.Image = ImageGlass.Properties.Resources.menu; }

                GlobalSetting.SetConfig("Theme", themeFile);
            }
            else
            {
                LoadThemeDefault();
            }

        }


        /// <summary>
        /// Load app configurations
        /// </summary>
        async Task LoadConfig()
        {
            //Load language pack-------------------------------------------------------------
            string s = GlobalSetting.GetConfig("Language", "English");
            if (s.ToLower().CompareTo("english") != 0 && File.Exists(s))
            {
                GlobalSetting.LangPack = new Library.Language(s);

                //force update language pack
                GlobalSetting.IsForcedActive = true;
                frmMain_Activated(null, null);
            }
            
            //Windows Bound (Position + Size)------------------------------------------------
            Rectangle rc = GlobalSetting.StringToRect(GlobalSetting.GetConfig("WindowsBound", "280,125,850,550"));
            this.Bounds = rc;

            //windows state--------------------------------------------------------------
            s = GlobalSetting.GetConfig("WindowsState", "Normal");
            if (s == "Normal")
            {
                this.WindowState = FormWindowState.Normal;
            }
            else if (s == "Maximized")
            {
                this.WindowState = FormWindowState.Maximized;
            }

            //check current version for the first time running
            s = GlobalSetting.GetConfig("igVersion", Application.ProductVersion);
            if (s.CompareTo(Application.ProductVersion) == 0) //Old version
            {
                //Load Extra extensions
                GlobalSetting.SupportedExtraExtensions = GlobalSetting.GetConfig("ExtraExtensions", GlobalSetting.SupportedExtraExtensions);
            }

            //Load theme--------------------------------------------------------------------
            LoadTheme();

            //Slideshow Interval-----------------------------------------------------------
            int i = int.Parse(GlobalSetting.GetConfig("Interval", "5"));
            if (!(0 < i && i < 61)) i = 5;//time limit [1; 60] seconds
            timSlideShow.Interval = 1000 * i;

            //Show checked bakcground-------------------------------------------------------
            GlobalSetting.IsShowCheckedBackground = bool.Parse(GlobalSetting.GetConfig("IsShowCheckedBackground", "False").ToString());
            GlobalSetting.IsShowCheckedBackground = !GlobalSetting.IsShowCheckedBackground;
            mnuMainCheckBackground_Click(null, EventArgs.Empty);
            

            //Recursive loading--------------------------------------------------------------
            GlobalSetting.IsRecursive = bool.Parse(GlobalSetting.GetConfig("Recursive", "False"));

            //Get welcome screen------------------------------------------------------------
            GlobalSetting.IsWelcomePicture = bool.Parse(GlobalSetting.GetConfig("Welcome", "True"));

            //Load default image------------------------------------------------------------
            string y = GlobalSetting.GetConfig("Welcome", "True");
            if (y.ToLower() == "true")
            {
                //Do not show welcome image if params exist.
                if(Environment.GetCommandLineArgs().Count() < 2)
                {
                    await Prepare(GlobalSetting.StartUpDir + "default.png");
                }
                
            }

            //Load is loop back slideshow---------------------------------------------------
            GlobalSetting.IsLoopBackSlideShow = bool.Parse(GlobalSetting.GetConfig("IsLoopBackSlideShow", "True"));

            //Load IsPressESCToQuit---------------------------------------------------------
            GlobalSetting.IsPressESCToQuit = bool.Parse(GlobalSetting.GetConfig("IsPressESCToQuit", "True"));

            //Load background---------------------------------------------------------------
            string z = GlobalSetting.GetConfig("BackgroundColor", "-1");
            GlobalSetting.BackgroundColor = Color.FromArgb(int.Parse(z));
            picMain.BackColor = GlobalSetting.BackgroundColor;

            //Load state of Toolbar---------------------------------------------------------
            GlobalSetting.IsShowToolBar = bool.Parse(GlobalSetting.GetConfig("IsShowToolBar", "True"));
            GlobalSetting.IsShowToolBar = !GlobalSetting.IsShowToolBar;
            mnuMainToolbar_Click(null, EventArgs.Empty);


            //Load Thumbnail dimension
            if (int.TryParse(GlobalSetting.GetConfig("ThumbnailDimension", "48"), out i))
            {
                GlobalSetting.ThumbnailDimension = i;
            }
            else
            {
                GlobalSetting.ThumbnailDimension = 48;
            }

            thumbnailBar.SetRenderer(new ImageListView.ImageListViewRenderers.ThemeRenderer());
            thumbnailBar.ThumbnailSize = new Size(GlobalSetting.ThumbnailDimension + GlobalSetting.ThumbnailDimension / 3, GlobalSetting.ThumbnailDimension);

            //Load state of Thumbnail---------------------------------------------------------
            GlobalSetting.IsShowThumbnail = bool.Parse(GlobalSetting.GetConfig("IsShowThumbnail", "False"));
            GlobalSetting.IsShowThumbnail = !GlobalSetting.IsShowThumbnail;
            mnuMainThumbnailBar_Click(null, EventArgs.Empty);
        }
        #endregion

        #region Manage config
        AppConfig getConfig(){
            return new AppConfig{
                IgVersion = Application.ProductVersion.ToString(),
                WindowsBound = WindowState == FormWindowState.Normal? (Option<Rectangle>) Option<Rectangle>.Some(Bounds) : Option<Rectangle>.None(),
                State = WindowState,
                IsShowCheckedBackground = GlobalSetting.IsShowCheckedBackground,
                IsShowToolBar = GlobalSetting.IsShowToolBar,
                IsShowThumbnail = GlobalSetting.IsShowThumbnail
            };
        }
        AppConfig loadConfig() => AppConfig.Load(GlobalSetting.GetConfig);
        /// <summary>
        /// Save app configurations
        /// </summary>
        Task saveConfig(){
            getConfig().Save(GlobalSetting.SetConfig);
            return ImageSaveChange();
        }

        #endregion


        #region Form events

        protected override void WndProc(ref Message m){
            //Check if the received message is WM_SHOWME
            if (m.Msg == NativeMethods.WM_SHOWME){
                //Set frmMain of the first instance to TopMost
                if (WindowState == FormWindowState.Minimized){
                    WindowState = FormWindowState.Normal;
                }
                // get our current "TopMost" value (ours will always be false though)
                bool top = TopMost;
                // make our form jump to the top of everything
                TopMost = true;
                // set it back to whatever it was
                TopMost = top;
            }
            //This message is sent when the form is dragged to a different monitor i.e. when
            //the bigger part of its are is on the new monitor. 
            else if (m.Msg == Theme.DPIScaling.WM_DPICHANGED){
                LocalSetting.OldDPI = LocalSetting.CurrentDPI;
                LocalSetting.CurrentDPI = Theme.DPIScaling.LOWORD((int) m.WParam);

                if (LocalSetting.OldDPI != LocalSetting.CurrentDPI){
                    Theme.DPIScaling.HandleDpiChanged(LocalSetting.OldDPI, LocalSetting.CurrentDPI, this);

                    toolMain.Height = 33;
                }
            }
            base.WndProc(ref m);
        }


        private async void frmMain_Load(object sender, EventArgs e)
        {
            Contract.Requires(SynchronizationContext.Current != null, "There must be UI sync context at this point!");

            //uiTask = new TaskFactory(TaskScheduler.FromCurrentSynchronizationContext());

            //Remove white line under tool strip
            toolMain.Renderer = new Theme.ToolStripRenderer();

            await LoadConfig();

            //Load image from param
            await LoadFromParams(Environment.GetCommandLineArgs());

            sp1.SplitterDistance = sp1.Height - GlobalSetting.ThumbnailDimension - 41;
            sp1.SplitterWidth = 1;
        }

        public async Task LoadFromParams(string[] args)
        {
            //Load image from param
            if (args.Length >= 2)
            {
                string filename = "";
                filename = args[1];

                if (File.Exists(filename))
                {
                    FileInfo f = new FileInfo(filename);
                    await Prepare(f.FullName);
                }
                else if (Directory.Exists(filename))
                {
                    DirectoryInfo d = new DirectoryInfo(filename);
                    await Prepare(d.FullName);
                }
            }
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            //clear temp files
            string temp_dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                            "\\ImageGlass\\Temp\\";
            if (Directory.Exists(temp_dir))
            {
                Directory.Delete(temp_dir, true);
            }            

            saveConfig();
        }

        private void frmMain_Activated(object sender, EventArgs e)
        {
            if (GlobalSetting.IsForcedActive)
            {
                picMain.BackColor = GlobalSetting.BackgroundColor;

                //Toolbar
                btnBack.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnBack"];
                btnNext.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnNext"];
                btnRotateLeft.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnRotateLeft"];
                btnRotateRight.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnRotateRight"];
                btnZoomIn.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnZoomIn"];
                btnZoomOut.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnZoomOut"];
                btnActualSize.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnActualSize"];
                btnZoomLock.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnZoomLock"];
                btnScaletoWidth.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnScaletoWidth"];
                btnScaletoHeight.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnScaletoHeight"];
                btnWindowAutosize.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnWindowAutosize"];
                btnOpen.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnOpen"];
                btnRefresh.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnRefresh"];
                btnGoto.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnGoto"];
                btnThumb.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnThumb"];
                btnCheckedBackground.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnCaro"];
                btnFullScreen.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnFullScreen"];
                btnSlideShow.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnSlideShow"];
                btnConvert.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnConvert"];
                btnPrintImage.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnPrintImage"];
                btnFacebook.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnFacebook"];
                btnExtension.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnExtension"];
                btnSetting.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnSetting"];
                btnHelp.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnHelp"];
                btnMenu.ToolTipText = GlobalSetting.LangPack.Items["frmMain.btnMenu"];

                //Main menu
                mnuMainOpenFile.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainOpenFile"];
                mnuMainOpenImageData.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainOpenImageData"];
                mnuMainSaveAs.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainSaveAs"];
                mnuMainRefresh.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainRefresh"];
                mnuMainEditImage.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainEditImage"];

                mnuMainNavigation.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainNavigation"];
                mnuMainViewNext.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainViewNext"];
                mnuMainViewPrevious.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainViewPrevious"];
                mnuMainGoto.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainGoto"];
                mnuMainGotoFirst.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainGotoFirst"];
                mnuMainGotoLast.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainGotoLast"];

                mnuMainFullScreen.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainFullScreen"];

                mnuMainSlideShow.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainSlideShow"];
                mnuMainSlideShowStart.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainSlideShowStart"];
                mnuMainSlideShowPause.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainSlideShowPause"];
                mnuMainSlideShowExit.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainSlideShowExit"];

                mnuMainPrint.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainPrint"];

                mnuMainManipulation.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainManipulation"];
                mnuMainRotateCounterclockwise.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainRotateCounterclockwise"];
                mnuMainRotateClockwise.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainRotateClockwise"];
                mnuMainZoomIn.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainZoomIn"];
                mnuMainZoomOut.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainZoomOut"];
                mnuMainActualSize.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainActualSize"];
                mnuMainLockZoomRatio.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainLockZoomRatio"];
                mnuMainScaleToWidth.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainScaleToWidth"];
                mnuMainScaleToHeight.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainScaleToHeight"];
                mnuMainWindowAdaptImage.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainWindowAdaptImage"];
                mnuMainRename.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainRename"];
                mnuMainMoveToRecycleBin.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainMoveToRecycleBin"];
                mnuMainDeleteFromHardDisk.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainDeleteFromHardDisk"];
                mnuMainExtractFrames.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainExtractFrames"];
                mnuMainStartStopAnimating.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainStartStopAnimating"];
                mnuMainSetAsDesktop.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainSetAsDesktop"];
                mnuMainImageLocation.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainImageLocation"];
                mnuMainImageProperties.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainImageProperties"];

                mnuMainClipboard.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainClipboard"];
                mnuMainCopy.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainCopy"];
                mnuMainCopyMulti.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainCopyMulti"];
                mnuMainCut.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainCut"];
                mnuMainCutMulti.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainCutMulti"];
                mnuMainCopyImagePath.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainCopyImagePath"];
                mnuMainClearClipboard.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainClearClipboard"];

                mnuMainShare.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainShare"];
                mnuMainShareFacebook.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainShareFacebook"];

                mnuMainLayout.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainLayout"];
                mnuMainToolbar.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainToolbar"];
                mnuMainThumbnailBar.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainThumbnailBar"];
                mnuMainCheckBackground.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainCheckBackground"];

                mnuMainTools.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainTools"];
                mnuMainExtensionManager.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainExtensionManager"];

                mnuMainSettings.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainSettings"];
                mnuMainAbout.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainAbout"];
                mnuMainReportIssue.Text = GlobalSetting.LangPack.Items["frmMain.mnuMainReportIssue"];
            }

            GlobalSetting.IsForcedActive = false;
        }

        private void frmMain_ResizeBegin(object sender, EventArgs e)
        {
            this._windowSize = this.Size;
        }

        private void frmMain_ResizeEnd(object sender, EventArgs e)
        {
            if (this.Size != this._windowSize && !this._isZoomed)
            {
                mnuMainRefresh_Click(null, null);

                saveConfig();
            }
            
        }

        async private void thumbnailBar_ItemClick(object sender, ImageListView.ItemClickEventArgs e)
        {
            currentIndex = e.Item.Index;
            await NextPic(0);
        }

        async private void timSlideShow_Tick(object sender, EventArgs e)
        {
            await NextPic(1);

            //stop playing slideshow at last image
            if (currentIndex == imageList.Count - 1)
            {
                if (!GlobalSetting.IsLoopBackSlideShow)
                {
                    mnuMainSlideShowPause_Click(null, null);
                }
            }
        }

        private void sysWatch_Renamed(object sender, RenamedEventArgs e)
        {
            string newName = e.FullPath;
            string oldName = e.OldFullPath;

            //Get index of renamed image
            int imgIndex = imageList.IndexOf(oldName);
            if (imgIndex > -1)
            {
                //Rename image list
                imageList[imgIndex] = newName;

                this.UpdateStatusBar();

                //Rename image in thumbnail bar
                thumbnailBar.Items[imgIndex].Text = e.Name;
                thumbnailBar.Items[imgIndex].Tag = newName;
            }
        }

        private async void sysWatch_Deleted(object sender, FileSystemEventArgs e)
        {
            //Get index of deleted image
            int imgIndex = imageList.IndexOf(e.FullPath);

            if (imgIndex > -1)
            {
                imageList.RemoveAt(imgIndex);

                //delete thumbnail list
                thumbnailBar.Items.RemoveAt(imgIndex);

                await NextPic(0);
            }
        }

        private async void sysWatch_Changed(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                await NextPic(0);
            }
        }

        private void picMain_Zoomed(object sender, ImageBoxZoomEventArgs e)
        {
            this._isZoomed = true;
            this.UpdateStatusBar(true);
        }

        private void picMain_MouseClick(object sender, MouseEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Middle: //Refresh
                    mnuMainRefresh_Click(null, null);
                    break;

                case MouseButtons.XButton1: //Back
                    mnuMainViewPrevious_Click(null, null);
                    break;

                case MouseButtons.XButton2: //Next
                    mnuMainViewNext_Click(null, null);
                    break;

                default:
                    break;
            }

        }
        #endregion



        #region Toolbar Button
        private void btnNext_Click(object sender, EventArgs e)
        {
            mnuMainViewNext_Click(null, e);
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            mnuMainViewPrevious_Click(null, e);
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            mnuMainRefresh_Click(null, e);
        }

        private void btnRotateRight_Click(object sender, EventArgs e)
        {
            mnuMainRotateClockwise_Click(null, e);
        }

        private void btnRotateLeft_Click(object sender, EventArgs e)
        {
            mnuMainRotateCounterclockwise_Click(null, e);
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            mnuMainOpenFile_Click(null, e);
        }

        private void btnThumb_Click(object sender, EventArgs e)
        {
            mnuMainThumbnailBar_Click(null, e);
        }

        private void btnActualSize_Click(object sender, EventArgs e)
        {
            mnuMainActualSize_Click(null, e);
        }

        private void btnScaletoWidth_Click(object sender, EventArgs e)
        {
            mnuMainScaleToWidth_Click(null, e);
        }

        private void btnScaletoHeight_Click(object sender, EventArgs e)
        {
            mnuMainScaleToHeight_Click(null, e);
        }

        private void btnWindowAutosize_Click(object sender, EventArgs e)
        {
            mnuMainWindowAdaptImage_Click(null, e);
        }

        private void btnGoto_Click(object sender, EventArgs e)
        {
            mnuMainGoto_Click(null, e);
        }

        private void btnCheckedBackground_Click(object sender, EventArgs e)
        {
            mnuMainCheckBackground_Click(null, e);
        }

        private void btnZoomIn_Click(object sender, EventArgs e)
        {
            mnuMainZoomIn_Click(null, e);
        }

        private void btnZoomOut_Click(object sender, EventArgs e)
        {
            mnuMainZoomOut_Click(null, e);
        }

        private void btnZoomLock_Click(object sender, EventArgs e)
        {
            mnuMainLockZoomRatio_Click(null, e);
        }

        private void btnSlideShow_Click(object sender, EventArgs e)
        {
            mnuMainSlideShowStart_Click(null, null);
        }

        private void btnFullScreen_Click(object sender, EventArgs e)
        {
            fullScreen();
        }

        private void btnPrintImage_Click(object sender, EventArgs e)
        {
            mnuMainPrint_Click(null, e);
        }

        private void btnFacebook_Click(object sender, EventArgs e)
        {
            mnuMainShareFacebook_Click(null, e);
        }

        private void btnExtension_Click(object sender, EventArgs e)
        {
            mnuMainExtensionManager_Click(null, e);
        }

        private void btnSetting_Click(object sender, EventArgs e)
        {
            mnuMainSettings_Click(null, e);
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            mnuMainAbout_Click(null, e);
        }

        private void btnConvert_Click(object sender, EventArgs e)
        {
            mnuMainSaveAs_Click(null, e);
        }

        private void btnReport_Click(object sender, EventArgs e)
        {
            mnuMainReportIssue_Click(null, e);
        }
        #endregion
        


        #region Popup Menu
        private async void mnuPopup_Opening(object sender, CancelEventArgs e)
        {
            if (GlobalSetting.IsImageError || !File.Exists(CurrentFilePath)) {
                e.Cancel = true;
                return;
            }

            //clear current items
            mnuPopup.Items.Clear();

            if (GlobalSetting.IsPlaySlideShow)
            {
                mnuPopup.Items.Add(Library.Menu.Clone(mnuMainSlideShowPause));
                mnuPopup.Items.Add(Library.Menu.Clone(mnuMainSlideShowExit));
                mnuPopup.Items.Add(new ToolStripSeparator());//---------------
            }
            
            //toolbar menu
            mnuPopup.Items.Add(Library.Menu.Clone(mnuMainToolbar));
            mnuPopup.Items.Add(new ToolStripSeparator());//---------------

            mnuPopup.Items.Add(Library.Menu.Clone(mnuMainEditImage));
            
            //check if image can animate (GIF)
            var image = await cache.GetImage(CurrentFilePath);
            var img = image.Get(_ => new Bitmap(1, 1), Prelude.Identity);
            FrameDimension dim = new FrameDimension(img.FrameDimensionsList[0]);
            int frameCount = img.GetFrameCount(dim);

            if (frameCount > 1)
            {
                var mi = Library.Menu.Clone(mnuMainExtractFrames);
                mi.Text = string.Format(GlobalSetting.LangPack.Items["frmMain.mnuMainExtractFrames"], frameCount);

                mnuPopup.Items.Add(Library.Menu.Clone(mi));
                mnuPopup.Items.Add(Library.Menu.Clone(mnuMainStartStopAnimating));
            }

            mnuPopup.Items.Add(Library.Menu.Clone(mnuMainSetAsDesktop));

            mnuPopup.Items.Add(new ToolStripSeparator());//------------
            mnuPopup.Items.Add(Library.Menu.Clone(mnuMainOpenImageData));
            mnuPopup.Items.Add(Library.Menu.Clone(mnuMainCopy));
            mnuPopup.Items.Add(Library.Menu.Clone(mnuMainCut));
            mnuPopup.Items.Add(Library.Menu.Clone(mnuMainClearClipboard));

            mnuPopup.Items.Add(new ToolStripSeparator());//------------
            mnuPopup.Items.Add(Library.Menu.Clone(mnuMainRename));
            mnuPopup.Items.Add(Library.Menu.Clone(mnuMainMoveToRecycleBin));
            mnuPopup.Items.Add(Library.Menu.Clone(mnuMainDeleteFromHardDisk));

            mnuPopup.Items.Add(new ToolStripSeparator());//------------
            mnuPopup.Items.Add(Library.Menu.Clone(mnuMainShareFacebook));

            mnuPopup.Items.Add(new ToolStripSeparator());//------------
            mnuPopup.Items.Add(Library.Menu.Clone(mnuMainCopyImagePath));
            mnuPopup.Items.Add(Library.Menu.Clone(mnuMainImageLocation));
            mnuPopup.Items.Add(Library.Menu.Clone(mnuMainImageProperties));

        }
        #endregion



        #region Main Menu (Main function)

        async void mnuMainOpenFile_Click(object sender, EventArgs e) {
            await OpenFile();
        }

        private async void mnuMainOpenImageData_Click(object sender, EventArgs e)
        {
            //Is there a file in clipboard ?--------------------------------------------------
            if (Clipboard.ContainsFileDropList())
            {
                string[] sFile = (string[])Clipboard.GetData(System.Windows.Forms.DataFormats.FileDrop);
                int fileCount = 0;

                fileCount = sFile.Length;

                //neu co file thi load
                await Prepare(sFile[0]);
            }


            //Is there a image in clipboard ?-------------------------------------------------
            //CheckImageInClipboard: ;
            else if (Clipboard.ContainsImage())
            {
                picMain.Image = Clipboard.GetImage();
                GlobalSetting.IsTempMemoryData = true;
            }

            //Is there a filename in clipboard?-----------------------------------------------
            //CheckPathInClipboard: ;
            else if (Clipboard.ContainsText())
            {
                if (File.Exists(Clipboard.GetText()) || Directory.Exists(Clipboard.GetText()))
                {
                    await Prepare(Clipboard.GetText());
                }
                //get image from Base64string 
                else
                {
                    try
                    {
                        // data:image/jpeg;base64,xxxxxxxx
                        string base64str = Clipboard.GetText().Substring(Clipboard.GetText().LastIndexOf(',') + 1);
                        var file_bytes = Convert.FromBase64String(base64str);
                        var file_stream = new MemoryStream(file_bytes);
                        var file_image = Image.FromStream(file_stream);

                        picMain.Image = file_image;
                        GlobalSetting.IsTempMemoryData = true;
                    }
                    catch { }
                }
            }
        }

        private void mnuMainSaveAs_Click(object sender, EventArgs e)
        {
            if (picMain.Image == null)
            {
                return;
            }

            string filename = CurrentFilePath;
            if (string.IsNullOrEmpty(filename))
                filename = "untitled.png";

            Library.Image.ImageInfo.ConvertImage(picMain.Image, filename);
        }

        private void mnuMainRefresh_Click(object sender, EventArgs e)
        {
            // Any scrolling from prior image would 'stick': reset here
            picMain.ScrollTo(0, 0, 0, 0);

            //Zoom condition
            if (btnZoomLock.Checked)
            {
                picMain.Zoom = GlobalSetting.ZoomLockValue;
            }
            else
            {
                //Reset zoom
                picMain.ZoomToFit();

                this._isZoomed = false;
            }

            //Get image file information
            this.UpdateStatusBar();
        }

        private void mnuMainEditImage_Click(object sender, EventArgs e)
        {
            if (GlobalSetting.IsImageError)
            {
                return;
            }

            string filename = CurrentFilePath;

            Process p = new Process();
            p.StartInfo.FileName = filename;
            p.StartInfo.Verb = "edit";

            //show error dialog
            p.StartInfo.ErrorDialog = true;

            try
            {
                p.Start();
            }
            catch (Exception)
            { }
        }

        private async void mnuMainViewNext_Click(object sender, EventArgs e)
        {
            if (imageList.Count < 1)
                return;

            await NextPic(1);
        }

        private async void mnuMainViewPrevious_Click(object sender, EventArgs e)
        {
            if (imageList.Count < 1)
                return;

            await NextPic(-1);
        }

        private async void mnuMainGoto_Click(object sender, EventArgs e)
        {
            int n = currentIndex;
            string s = "0";
            if (InputBox.ShowDiaLog("Message", GlobalSetting.LangPack.Items["frmMain._GotoDialogText"],
                                    "0", true) == System.Windows.Forms.DialogResult.OK)
            {
                s = InputBox.Message;
            }

            if (int.TryParse(s, out n))
            {
                n--;

                if (-1 < n && n < imageList.Count)
                {
                    currentIndex = n;
                    await NextPic(0);
                }
            }
        }

        private async void mnuMainGotoFirst_Click(object sender, EventArgs e)
        {
            currentIndex = 0;
            await NextPic(0);
        }

        private async void mnuMainGotoLast_Click(object sender, EventArgs e)
        {
            currentIndex = imageList.Count - 1;
            await NextPic(0);
        }

        void mnuMainFullScreen_Click(object sender, EventArgs e){
            fullScreen();
        }
        

        private void mnuMainSlideShowStart_Click(object sender, EventArgs e)
        {
            if (imageList.Count < 1)
                return;

            //not performing
            if (!GlobalSetting.IsPlaySlideShow)
            {
                //perform slideshow
                picMain.BackColor = Color.Black;
                fullScreen();

                timSlideShow.Start();
                timSlideShow.Enabled = true;

                GlobalSetting.IsPlaySlideShow = true;
            }
            //performing
            else
            {
                mnuMainSlideShowExit_Click(null, null);
            }
            this.DisplayTextMessage(GlobalSetting.LangPack.Items["frmMain._SlideshowMessage"] , 5000);
        }

        private void mnuMainSlideShowPause_Click(object sender, EventArgs e)
        {
            //performing
            if (timSlideShow.Enabled)
            {
                timSlideShow.Enabled = false;
                timSlideShow.Stop();
            }
            else
            {
                timSlideShow.Enabled = true;
                timSlideShow.Start();
            }

        }

        private void mnuMainSlideShowExit_Click(object sender, EventArgs e)
        {
            timSlideShow.Stop();
            timSlideShow.Enabled = false;
            GlobalSetting.IsPlaySlideShow = false;

            picMain.BackColor = GlobalSetting.BackgroundColor;
            fullScreen();
        }

        /// <summary>
        /// Save current loaded image to file and print it
        /// </summary>
        private string SaveTemporaryMemoryData()
        {
            //save temp file
            string temp_dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                        "\\ImageGlass\\Temp\\";
            if (!Directory.Exists(temp_dir))
            {
                Directory.CreateDirectory(temp_dir);
            }

            string filename = temp_dir + "temp_" + DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss") + ".png";

            picMain.Image.Save(filename, System.Drawing.Imaging.ImageFormat.Png);

            return filename;
        }

        private void mnuMainPrint_Click(object sender, EventArgs e)
        {
            string filename = "";

            //save image from memory
            if (GlobalSetting.IsTempMemoryData)
            {
                filename = this.SaveTemporaryMemoryData();
            }
            //image error
            else if (imageList.Count < 1 || GlobalSetting.IsImageError)
                return;
            else
            {
                filename = CurrentFilePath;

                // check if file extension is NOT supported for native print
                // these extensions will not be printed by its associated app.
                if (GlobalSetting.SupportedExtraExtensions.Contains(Path.GetExtension(filename).ToLower()))
                {
                    filename = this.SaveTemporaryMemoryData();
                }
            }

            Process p = new Process();
            p.StartInfo.FileName = filename;
            p.StartInfo.Verb = "print";

            //show error dialog
            p.StartInfo.ErrorDialog = true;

            try
            {
                p.Start();
            }
            catch (Exception)
            { }

        }

        void mnuMainRotateCounterclockwise_Click(object sender, EventArgs e)
        {
            if (picMain.Image == null || picMain.CanAnimate)
            {
                return;
            }

            Bitmap bmp = new Bitmap(picMain.Image);
            bmp.RotateFlip(RotateFlipType.Rotate270FlipNone);
            picMain.Image = bmp;

            markDirty();
        }

        private void mnuMainRotateClockwise_Click(object sender, EventArgs e)
        {
            if (picMain.Image == null || picMain.CanAnimate)
            {
                return;
            }

            Bitmap bmp = new Bitmap(picMain.Image);
            bmp.RotateFlip(RotateFlipType.Rotate90FlipNone);
            picMain.Image = bmp;

            markDirty();
        }

        private void mnuMainZoomIn_Click(object sender, EventArgs e)
        {
            if (picMain.Image == null)
            {
                return;
            }

            picMain.ZoomIn();
        }

        private void mnuMainZoomOut_Click(object sender, EventArgs e)
        {
            if (picMain.Image == null)
            {
                return;
            }

            picMain.ZoomOut();
        }

        private void mnuMainActualSize_Click(object sender, EventArgs e)
        {
            if (picMain.Image == null)
            {
                return;
            }

            picMain.ActualSize();
        }

        private void mnuMainLockZoomRatio_Click(object sender, EventArgs e)
        {
            if (btnZoomLock.Checked)
            {
                GlobalSetting.ZoomLockValue = picMain.Zoom;
            }
            else
            {
                GlobalSetting.ZoomLockValue = 100;
                btnZoomLock.Checked = false;
            }
        }

        private void mnuMainScaleToWidth_Click(object sender, EventArgs e)
        {
            if (picMain.Image == null)
            {
                return;
            }

            // Scale to Width
            double frac = picMain.Width / (1.0 * picMain.Image.Width);
            picMain.Zoom = (int)(frac * 100);
        }

        private void mnuMainScaleToHeight_Click(object sender, EventArgs e)
        {
            if (picMain.Image == null)
            {
                return;
            }

            // Scale to Height
            double frac = picMain.Height / (1.0 * picMain.Image.Height);
            picMain.Zoom = (int)(frac * 100);
        }

        private void mnuMainWindowAdaptImage_Click(object sender, EventArgs e)
        {
            if (picMain.Image == null)
            {
                return;
            }
            
            Rectangle screen = Screen.FromControl(this).WorkingArea;
            this.WindowState = FormWindowState.Normal;

            //if image size is bigger than screen
            if (picMain.Image.Width >= screen.Width || picMain.Height >= screen.Height)
            {
                this.Left = this.Top = 0;
                this.Width = screen.Width;
                this.Height = screen.Height;
            }
            else
            {
                this.Size = new Size(Width += picMain.Image.Width - picMain.Width,
                                Height += picMain.Image.Height - picMain.Height);

                picMain.Bounds = new Rectangle(Point.Empty, picMain.Image.Size);
                this.Top = (screen.Height - this.Height) / 2 + screen.Top;
                this.Left = (screen.Width - this.Width) / 2 + screen.Left;
            }
            
        }

        private void mnuMainRename_Click(object sender, EventArgs e)
        {
            RenameImage();
        }

        private async void mnuMainMoveToRecycleBin_Click(object sender, EventArgs e)
        {
            if (!File.Exists(CurrentFilePath))
                return;

            string f = CurrentFilePath;
            try
            {
                //in case of GIF file...
                // why???
                string ext = Path.GetExtension(CurrentFilePath).ToLower();
                if (ext == ".gif")
                {
                    try
                    {
                        //delete thumbnail list
                        thumbnailBar.Items.RemoveAt(currentIndex);
                    }
                    catch { }

                    imageList.RemoveAt(currentIndex);

                    await NextPic(0);
                }

                ImageInfo.DeleteFile(f, true);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private async void mnuMainDeleteFromHardDisk_Click(object sender, EventArgs e)
        {
            if (!File.Exists(CurrentFilePath))
                return;

            var msg = MessageBox.Show(
                                string.Format(GlobalSetting.LangPack.Items["frmMain._DeleteDialogText"], CurrentFilePath),
                                GlobalSetting.LangPack.Items["frmMain._DeleteDialogTitle"],
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (msg == DialogResult.Yes)
            {
                string f = CurrentFilePath;
                try
                {
                    //Neu la anh GIF thi giai phong bo nho truoc khi xoa
                    string ext = Path.GetExtension(f).ToLower();
                    if (ext == ".gif")
                    {
                        try
                        {
                            //delete thumbnail list
                            thumbnailBar.Items.RemoveAt(currentIndex);
                        }
                        catch { }

                        //delete image list
                        imageList.RemoveAt(currentIndex);

                        await NextPic(0);
                    }

                    ImageInfo.DeleteFile(f);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void mnuMainExtractFrames_Click(object sender, EventArgs e)
        {
            if (!((ToolStripMenuItem)sender).Enabled) // Shortcut keys still work even when menu is disabled!
                return;

            if (!GlobalSetting.IsImageError)
            {
                FolderBrowserDialog f = new FolderBrowserDialog();
                f.Description = GlobalSetting.LangPack.Items["frmMain._ExtractFrameText"];
                f.ShowNewFolderButton = true;
                DialogResult res = f.ShowDialog();

                if (res == DialogResult.OK && Directory.Exists(f.SelectedPath))
                {
                    Animation ani = new Animation();
                    ani.ExtractAllFrames(CurrentFilePath, f.SelectedPath);
                }

                f = null;
            }
        }

        // ReSharper disable once EmptyGeneralCatchClause
        private void mnuMainSetAsDesktop_Click(object sender, EventArgs e)
        {
            if (GlobalSetting.IsImageError)
                return;

            using (var bmp = new Bitmap(picMain.Image))
                DesktopWallapaper.Set(bmp, DesktopWallapaper.Style.Centered);
        }

        private void mnuMainImageLocation_Click(object sender, EventArgs e)
        {
            if (imageList.Count > 0)
                Process.Start("explorer.exe", "/select,\"" + CurrentFilePath + "\"");
        }

        private void mnuMainImageProperties_Click(object sender, EventArgs e)
        {
            if (imageList.Count > 0)
                ImageInfo.DisplayFileProperties(CurrentFilePath, this.Handle);
        }

        private void mnuMainCopy_Click(object sender, EventArgs e)
        {
            CopyFile();
        }

        private void mnuMainCopyMulti_Click(object sender, EventArgs e)
        {
            CopyMultiFile();
        }

        private void mnuMainCut_Click(object sender, EventArgs e)
        {
            CutFile();
        }

        private void mnuMainCutMulti_Click(object sender, EventArgs e)
        {
            CutMultiFile();
        }

        private void mnuMainCopyImagePath_Click(object sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(CurrentFilePath);
            }
            catch { }
        }

        private void mnuMainClearClipboard_Click(object sender, EventArgs e)
        {
            //clear copied files in clipboard
            if (GlobalSetting.StringClipboard.Count > 0)
            {
                GlobalSetting.StringClipboard = new StringCollection();
                Clipboard.Clear();
                this.DisplayTextMessage(GlobalSetting.LangPack.Items["frmMain._ClearClipboard"], 1000);
            }
        }

        private void mnuMainShareFacebook_Click(object sender, EventArgs e)
        {
            if (imageList.Count > 0 && File.Exists(CurrentFilePath))
            {
                if (LocalSetting.FFacebook.IsDisposed)
                {
                    LocalSetting.FFacebook = new frmFacebook();
                }

                //CHECK FILE EXTENSION BEFORE UPLOADING
                string filename = "";

                //save image from memory
                if (GlobalSetting.IsTempMemoryData)
                {
                    filename = this.SaveTemporaryMemoryData();
                }
                //image error
                else if (imageList.Count < 1 || GlobalSetting.IsImageError)
                {
                    return;
                }
                else
                {
                    filename = CurrentFilePath;

                    // check if file extension is NOT supported for native print
                    // these extensions will not be printed by its associated app.
                    if (GlobalSetting.SupportedExtraExtensions.Contains(Path.GetExtension(filename).ToLower()))
                    {
                        filename = this.SaveTemporaryMemoryData();
                    }
                }

                LocalSetting.FFacebook.Filename = filename;
                GlobalSetting.IsForcedActive = false;
                LocalSetting.FFacebook.Show();
                LocalSetting.FFacebook.Activate();
            }
        }

        private void mnuMainToolbar_Click(object sender, EventArgs e)
        {
            GlobalSetting.IsShowToolBar = !GlobalSetting.IsShowToolBar;
            if (GlobalSetting.IsShowToolBar)
            {
                //Hien
                toolMain.Visible = true;
            }
            else
            {
                //An
                toolMain.Visible = false;
            }
            mnuMainToolbar.Checked = GlobalSetting.IsShowToolBar;
        }

        private void mnuMainThumbnailBar_Click(object sender, EventArgs e) {
            generateThumbnail();
        }

        private void mnuMainCheckBackground_Click(object sender, EventArgs e)
        {
            GlobalSetting.IsShowCheckedBackground = !GlobalSetting.IsShowCheckedBackground;
            btnCheckedBackground.Checked = GlobalSetting.IsShowCheckedBackground;

            if (btnCheckedBackground.Checked)
            {
                //show
                picMain.GridDisplayMode = ImageBoxGridDisplayMode.Client;
            }
            else
            {
                //hide
                picMain.GridDisplayMode = ImageBoxGridDisplayMode.None;
            }

            mnuMainCheckBackground.Checked = btnCheckedBackground.Checked;
        }

        private void mnuMainExtensionManager_Click(object sender, EventArgs e)
        {
            if (LocalSetting.FExtension.IsDisposed)
            {
                LocalSetting.FExtension = new frmExtension();
            }
            GlobalSetting.IsForcedActive = false;
            LocalSetting.FExtension.Show();
            LocalSetting.FExtension.Activate();
        }

        private void mnuMainSettings_Click(object sender, EventArgs e)
        {
            if (LocalSetting.FSetting.IsDisposed)
            {
                LocalSetting.FSetting = new frmSetting();
            }

            GlobalSetting.IsForcedActive = false;
            LocalSetting.FSetting.Show();
            LocalSetting.FSetting.Activate();
        }

        private void mnuMainAbout_Click(object sender, EventArgs e)
        {
            frmAbout f = new frmAbout();
            f.ShowDialog();
        }

        private void mnuMainReportIssue_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start("https://github.com/d2phap/ImageGlass/issues");
            }
            catch { }
        }

        private void mnuMainStartStopAnimating_Click(object sender, EventArgs e)
        {
            if (picMain.IsAnimating)
            {
                picMain.StopAnimating();
            }
            else
            {
                picMain.StartAnimating();
            }
        }

        private async void mnuMain_Opening(object sender, CancelEventArgs e)
        {
            mnuMainExtractFrames.Enabled = false;
            mnuMainStartStopAnimating.Enabled = false;
            if (currentIndex > 0) {
                var image = await cache.GetImage(CurrentFilePath);

                var img = image.Get(_ => new Bitmap(1, 1), Prelude.Identity);
                FrameDimension dim = new FrameDimension(img.FrameDimensionsList[0]);
                int frameCount = img.GetFrameCount(dim);

                mnuMainExtractFrames.Text = string.Format(GlobalSetting.LangPack.Items["frmMain.mnuMainExtractFrames"], frameCount);

                if (frameCount > 1) {
                    mnuMainExtractFrames.Enabled = true;
                    mnuMainStartStopAnimating.Enabled = true;
                }
            }
        }



        #endregion

        #region Crop Mode
        bool inCropMode;

        private void cropToolStripMenuItem_Click(object sender, EventArgs e) {
            showCropMode();
        }
        private void showCropMode() {
            if (inCropMode)
                return;

            picMain.AllowDrop = false;

            // Cropping mode
            // 1. Zoom so that image + drag handles are visible
            // 2. Set selection region to full image
            // TODO selection color?
            // 3. Accept/cancel control?
            // 4. "Image is modified" state?

            int val = picMain.DragHandleSize;
            int targetW = picMain.Image.Width + val + val + 3;
            int targetH = picMain.Image.Height + val + val + 3;

            foreach (var handle in picMain.DragHandles)
            {
                switch (handle.Anchor)
                {
                    case DragHandleAnchor.BottomCenter:
                    case DragHandleAnchor.TopCenter:
                    case DragHandleAnchor.MiddleLeft:
                    case DragHandleAnchor.MiddleRight:
                        handle.Enabled = true;
                        handle.Visible = true;
                        break;
                    default:
                        handle.Enabled = false;
                        handle.Visible = false;
                        break;
                }
            }

            double frac = Math.Min(picMain.Width / (1.0 * targetW), picMain.Height / (1.0 * targetH));
            picMain.Zoom = (int)(frac * 100);
            // TODO: fix this...
            // picMain.ZoomFactor = frac;

            picMain.SelectionRegion = new RectangleF(0, 0, picMain.Image.Width, picMain.Image.Height);

            DisplayTextMessage(
                "Drag resize handles to crop image.\n\nPress ESC or X to exit.\n\nPress A to 'Save As' cropped image.\nPress S to 'Save' cropped image.\nPress R to start over.",
                7000);

            inCropMode = true;
        }
        private void ReleaseCropMode()
        {
            inCropMode = false;
            picMain.SelectionRegion = RectangleF.Empty;
            picMain.AllowDrop = true;
        }

        private Image GetCropImage()
        {
            RectangleF rect = new RectangleF(picMain.SelectionRegion.Location, picMain.SelectionRegion.Size);
            Image cropImage = new Bitmap((int)rect.Width, (int)rect.Height);

            using (Graphics g = Graphics.FromImage(cropImage))
                g.DrawImage(picMain.Image, new Rectangle(0, 0, (int)rect.Width, (int)rect.Height), rect, GraphicsUnit.Pixel);
            return cropImage;
        }

        private void CropModeSave()
        {
            picMain.Image = GetCropImage();
            markDirty();
            ImageSaveChange();
            ReleaseCropMode();
        }

        private void CropModeSaveAs()
        {
            picMain.Image = GetCropImage();
            mnuMainSaveAs_Click(null, null);
            ReleaseCropMode();
            // TODO what has to happen for this saved image to get into the ImageFilenameList?
        }

        private void ResetCropMode()
        {
            inCropMode = false;
            showCropMode();
        }
        #endregion
    }
}
