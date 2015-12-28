using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// KBR a cropping tool extension to the ImageBox control.
// See http://www.cyotek.com/blog/adding-drag-handles-to-an-imagebox-to-allow-resizing-of-selection-regions
// for the basis of this class.
//
// Modified to provide cropping behavior similar to that discussed in the book "About Face" (2d edition?)
// by Cooper.
//
// TODO image scrolling doesn't take the drag handles into account. I.e. if the drag handle is at the edge of the image, the image cannot be scrolled to access the drag handle.
//
namespace ImageGlass
{
    // Cyotek ImageBox
    // Copyright (c) 2010-2014 Cyotek.
    // http://cyotek.com
    // http://cyotek.com/blog/tag/imagebox

    // Licensed under the MIT License. See imagebox-license.txt for the full text.

    // If you use this control in your applications, attribution, donations or contributions are welcome.

    public class ImageBoxEx : ImageBox
    {
        #region Instance Fields

        private readonly DragHandleCollection _dragHandles;

        private int _dragHandleSize;

        private Size _minimumSelectionSize;

        #endregion

        #region Public Constructors

        public ImageBoxEx() {
            _dragHandles = new DragHandleCollection();
            DragHandleSize = 8;
            MinimumSelectionSize = Size.Empty;
            PositionDragHandles();
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when the DragHandleSize property value changes
        /// </summary>
        [Category("Property Changed")]
        public event EventHandler DragHandleSizeChanged;

        /// <summary>
        /// Occurs when the MinimumSelectionSize property value changes
        /// </summary>
        [Category("Property Changed")]
        public event EventHandler MinimumSelectionSizeChanged;

        [Category("Action")]
        public event EventHandler SelectionMoved;

        [Category("Action")]
        public event CancelEventHandler SelectionMoving;

        [Category("Action")]
        public event EventHandler SelectionResized;

        [Category("Action")]
        public event CancelEventHandler SelectionResizing;

        #endregion

        #region Overridden Methods

        /// <summary>
        ///   Raises the <see cref="System.Windows.Forms.Control.MouseDown" /> event.
        /// </summary>
        /// <param name="e">
        ///   A <see cref="T:System.Windows.Forms.MouseEventArgs" /> that contains the event data.
        /// </param>
        protected override void OnMouseDown(MouseEventArgs e) {
            Point imagePoint = PointToImage(e.Location);

            if (e.Button == MouseButtons.Left && (SelectionRegion.Contains(imagePoint) || HitTest(e.Location) != DragHandleAnchor.None)) {
                DragOrigin = e.Location;
                DragOriginOffset = new Point(imagePoint.X - (int)SelectionRegion.X, imagePoint.Y - (int)SelectionRegion.Y);
            } else {
                DragOriginOffset = Point.Empty;
                DragOrigin = Point.Empty;
            }

            base.OnMouseDown(e);
        }

        /// <summary>
        ///   Raises the <see cref="System.Windows.Forms.Control.MouseMove" /> event.
        /// </summary>
        /// <param name="e">
        ///   A <see cref="T:System.Windows.Forms.MouseEventArgs" /> that contains the event data.
        /// </param>
        protected override void OnMouseMove(MouseEventArgs e) {
            // start either a move or a resize operation
            if (!IsSelecting && !IsMoving && !IsResizing && e.Button == MouseButtons.Left && !DragOrigin.IsEmpty && IsOutsideDragZone(e.Location)) {
                DragHandleAnchor anchor = HitTest(DragOrigin);

                if (anchor == DragHandleAnchor.None) {
                    // Disable selection move
                    //this.StartMove();
                } else if (DragHandles[anchor].Enabled && DragHandles[anchor].Visible) {
                    // resize
                    StartResize(anchor);
                }
            }

            // set the cursor
            SetCursor(e.Location);

            // perform operations
            ProcessSelectionMove(e.Location);
            ProcessSelectionResize(e.Location);

            if (!IsResizing) // NOTE: when resizing selection, don't let the mouse move scroll the image
                base.OnMouseMove(e);
        }

        /// <summary>
        ///   Raises the <see cref="System.Windows.Forms.Control.MouseUp" /> event.
        /// </summary>
        /// <param name="e">
        ///   A <see cref="T:System.Windows.Forms.MouseEventArgs" /> that contains the event data.
        /// </param>
        protected override void OnMouseUp(MouseEventArgs e) {
            if (IsMoving) {
                CompleteMove();
            } else if (IsResizing) {
                CompleteResize();
            }

            base.OnMouseUp(e);
        }

        /// <summary>
        ///   Raises the <see cref="System.Windows.Forms.Control.Paint" /> event.
        /// </summary>
        /// <param name="e">
        ///   A <see cref="T:System.Windows.Forms.PaintEventArgs" /> that contains the event data.
        /// </param>
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);

            if (!SelectionRegion.IsEmpty) {
                PositionDragHandles(); // KBR calling here was the only way to get the drag handles correct
                foreach (DragHandle handle in DragHandles) {
                    if (handle.Visible) {
                        DrawDragHandle(e.Graphics, handle);
                    }
                }
            }
        }

        // KBR preserved from the original code but didn't work right: after zoom the drag handles might not be positioned properly. See above in OnPaint.
        /// <summary>
        ///   Raises the <see cref="System.Windows.Forms.Control.Resize" /> event.
        /// </summary>
        /// <param name="e">
        ///   An <see cref="T:System.EventArgs" /> that contains the event data.
        /// </param>
        //protected override void OnResize(EventArgs e)
        //{
        //  base.OnResize(e);

        //  PositionDragHandles();
        //}

        // KBR preserved from the original code but didn't work right: after zoom the drag handles might not be positioned properly. See above in OnPaint.
        /// <summary>
        ///   Raises the <see cref="System.Windows.Forms.ScrollableControl.Scroll" /> event.
        /// </summary>
        /// <param name="se">
        ///   A <see cref="T:System.Windows.Forms.ScrollEventArgs" /> that contains the event data.
        /// </param>
        //protected override void OnScroll(ScrollEventArgs se)
        //{
        //  base.OnScroll(se);

        //  PositionDragHandles();
        //}

        /// <summary>
        ///   Raises the <see cref="ImageBox.Selecting" /> event.
        /// </summary>
        /// <param name="e">
        ///   The <see cref="System.EventArgs" /> instance containing the event data.
        /// </param>
        protected override void OnSelecting(ImageBoxCancelEventArgs e) {
            e.Cancel = IsMoving | IsResizing | SelectionRegion.Contains(PointToImage(e.Location)) | HitTest(e.Location) != DragHandleAnchor.None;

            base.OnSelecting(e);
        }

        // KBR preserved from the original code but didn't work right: after zoom the drag handles might not be positioned properly. See above in OnPaint.
        /// <summary>
        ///   Raises the <see cref="ImageBox.SelectionRegionChanged" /> event.
        /// </summary>
        /// <param name="e">
        ///   The <see cref="System.EventArgs" /> instance containing the event data.
        /// </param>
        //protected override void OnSelectionRegionChanged(EventArgs e)
        //{
        //  base.OnSelectionRegionChanged(e);

        //  PositionDragHandles();
        //}

        // KBR preserved from the original code but didn't work right: after zoom the drag handles might not be positioned properly. See above in OnPaint.
        /// <summary>
        ///   Raises the <see cref="ImageBox.ZoomChanged" /> event.
        /// </summary>
        /// <param name="e">
        ///   The <see cref="System.EventArgs" /> instance containing the event data.
        /// </param>
        //protected override void OnZoomChanged(EventArgs e)
        //{
        //  base.OnZoomChanged(e);

        //  PositionDragHandles();
        //}

        /// <summary>
        /// Processes a dialog key.
        /// </summary>
        /// <returns>
        /// true if the key was processed by the control; otherwise, false.
        /// </returns>
        /// <param name="keyData">One of the <see cref="T:System.Windows.Forms.Keys"/> values that represents the key to process. </param>
        protected override bool ProcessDialogKey(Keys keyData) {
            bool result;

            if (keyData == Keys.Escape && (IsResizing || IsMoving)) {
                if (IsResizing) {
                    CancelResize();
                } else {
                    CancelMove();
                }

                result = true;
            } else {
                result = base.ProcessDialogKey(keyData);
            }

            return result;
        }

        #endregion

        #region Public Properties

        [Category("Appearance")]
        [DefaultValue(8)]
        public virtual int DragHandleSize {
            get { return _dragHandleSize; }
            set {
                if (DragHandleSize != value) {
                    _dragHandleSize = value;

                    OnDragHandleSizeChanged(EventArgs.Empty);
                }
            }
        }

        [Browsable(false)]
        public DragHandleCollection DragHandles {
            get { return _dragHandles; }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsMoving { get; protected set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsResizing { get; protected set; }

        [Category("Behavior")]
        [DefaultValue(typeof(Size), "0, 0")]
        public virtual Size MinimumSelectionSize {
            get { return _minimumSelectionSize; }
            set {
                if (MinimumSelectionSize != value) {
                    _minimumSelectionSize = value;

                    OnMinimumSelectionSizeChanged(EventArgs.Empty);
                }
            }
        }

        [Browsable(false)]
        public RectangleF PreviousSelectionRegion { get; protected set; }

        #endregion

        #region Protected Properties

        protected Point DragOrigin { get; set; }

        protected Point DragOriginOffset { get; set; }

        protected DragHandleAnchor ResizeAnchor { get; set; }

        #endregion

        #region Public Members

        public void CancelResize() {
            SelectionRegion = PreviousSelectionRegion;
            CompleteResize();
        }

        public void StartMove() {
            CancelEventArgs e;

            if (IsMoving || IsResizing) {
                throw new InvalidOperationException("A move or resize action is currently being performed.");
            }

            e = new CancelEventArgs();

            OnSelectionMoving(e);

            if (!e.Cancel) {
                PreviousSelectionRegion = SelectionRegion;
                IsMoving = true;
            }
        }

        #endregion

        #region Protected Members

        protected virtual void DrawDragHandle(Graphics graphics, DragHandle handle) {
            Pen outerPen;
            Brush innerBrush;

            int left = handle.Bounds.Left;
            int top = handle.Bounds.Top;
            int width = handle.Bounds.Width;
            int height = handle.Bounds.Height;

            if (handle.Enabled) {
                outerPen = SystemPens.WindowFrame;
                innerBrush = SystemBrushes.Window;
            } else {
                outerPen = SystemPens.ControlDark;
                innerBrush = SystemBrushes.Control;
            }

            graphics.FillRectangle(innerBrush, left + 1, top + 1, width - 2, height - 2);
            graphics.DrawLine(outerPen, left + 1, top, left + width - 2, top);
            graphics.DrawLine(outerPen, left, top + 1, left, top + height - 2);
            graphics.DrawLine(outerPen, left + 1, top + height - 1, left + width - 2, top + height - 1);
            graphics.DrawLine(outerPen, left + width - 1, top + 1, left + width - 1, top + height - 2);
        }

        /// <summary>
        /// Raises the <see cref="DragHandleSizeChanged" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnDragHandleSizeChanged(EventArgs e) {
            PositionDragHandles();
            Invalidate();

            EventHandler handler = DragHandleSizeChanged;

            if (handler != null) {
                handler(this, e);
            }
        }

        /// <summary>
        /// Raises the <see cref="MinimumSelectionSizeChanged" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnMinimumSelectionSizeChanged(EventArgs e) {
            EventHandler handler = MinimumSelectionSizeChanged;

            if (handler != null) {
                handler(this, e);
            }
        }

        /// <summary>
        /// Raises the <see cref="SelectionMoved" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnSelectionMoved(EventArgs e) {
            EventHandler handler = SelectionMoved;

            if (handler != null) {
                handler(this, e);
            }
        }

        /// <summary>
        /// Raises the <see cref="SelectionMoving" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnSelectionMoving(CancelEventArgs e) {
            CancelEventHandler handler = SelectionMoving;

            if (handler != null) {
                handler(this, e);
            }
        }

        /// <summary>
        /// Raises the <see cref="SelectionResized" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnSelectionResized(EventArgs e) {
            EventHandler handler = SelectionResized;

            if (handler != null) {
                handler(this, e);
            }
        }

        /// <summary>
        /// Raises the <see cref="SelectionResizing" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnSelectionResizing(CancelEventArgs e) {
            CancelEventHandler handler = SelectionResizing;

            if (handler != null) {
                handler(this, e);
            }
        }

        #endregion

        #region Private Members

        private void CancelMove() {
            SelectionRegion = PreviousSelectionRegion;
            CompleteMove();
        }

        private void CompleteMove() {
            ResetDrag();
            OnSelectionMoved(EventArgs.Empty);
        }

        private void CompleteResize() {
            ResetDrag();
            OnSelectionResized(EventArgs.Empty);
        }

        private DragHandleAnchor HitTest(Point cursorPosition) {
            return DragHandles.HitTest(cursorPosition);
        }

        private bool IsOutsideDragZone(Point location) {
            Rectangle dragZone;
            int dragWidth;
            int dragHeight;

            dragWidth = SystemInformation.DragSize.Width;
            dragHeight = SystemInformation.DragSize.Height;
            dragZone = new Rectangle(DragOrigin.X - (dragWidth / 2), DragOrigin.Y - (dragHeight / 2), dragWidth, dragHeight);

            return !dragZone.Contains(location);
        }

        private void PositionDragHandles() {
            if (DragHandles == null || DragHandleSize <= 0)
                return;

            if (SelectionRegion.IsEmpty) {
                foreach (DragHandle handle in DragHandles) {
                    handle.Bounds = Rectangle.Empty;
                }
                return;
            }
            /*
                    Rectangle viewport = GetImageViewPort();
                    int offsetX = viewport.Left + Padding.Left + AutoScrollPosition.X;
                    int offsetY = viewport.Top + Padding.Top + AutoScrollPosition.Y;
                    int halfDragHandleSize = DragHandleSize / 2;
                    int left = Convert.ToInt32((int) ((SelectionRegion.Left * ZoomFactor) + offsetX));
                    int top = Convert.ToInt32((int) ((SelectionRegion.Top * ZoomFactor) + offsetY));
                    int right = left + Convert.ToInt32(SelectionRegion.Width * ZoomFactor);
                    int bottom = top + Convert.ToInt32(SelectionRegion.Height * ZoomFactor);
                    int halfWidth = Convert.ToInt32(SelectionRegion.Width * ZoomFactor) / 2;
                    int halfHeight = Convert.ToInt32(SelectionRegion.Height * ZoomFactor) / 2;

                    DragHandles[DragHandleAnchor.TopLeft].Bounds = new Rectangle(left - DragHandleSize, top - DragHandleSize, DragHandleSize, DragHandleSize);
                    DragHandles[DragHandleAnchor.TopCenter].Bounds = new Rectangle(left + halfWidth - halfDragHandleSize, top - DragHandleSize, DragHandleSize, DragHandleSize);
                    DragHandles[DragHandleAnchor.TopRight].Bounds = new Rectangle(right, top - DragHandleSize, DragHandleSize, DragHandleSize);
                    DragHandles[DragHandleAnchor.MiddleLeft].Bounds = new Rectangle(left - DragHandleSize, top + halfHeight - halfDragHandleSize, DragHandleSize, DragHandleSize);
                    DragHandles[DragHandleAnchor.MiddleRight].Bounds = new Rectangle(right, top + halfHeight - halfDragHandleSize, DragHandleSize, DragHandleSize);
                    DragHandles[DragHandleAnchor.BottomLeft].Bounds = new Rectangle(left - DragHandleSize, bottom, DragHandleSize, DragHandleSize);
                    DragHandles[DragHandleAnchor.BottomCenter].Bounds = new Rectangle(left + halfWidth - halfDragHandleSize, bottom, DragHandleSize, DragHandleSize);
                    DragHandles[DragHandleAnchor.BottomRight].Bounds = new Rectangle(right, bottom, DragHandleSize, DragHandleSize);
            */
            RectangleF rect = GetOffsetRectangle(SelectionRegion);
            int left = (int)rect.Left;
            int top = (int)rect.Top;
            int bottom = (int)rect.Bottom;
            int right = (int)rect.Right;
            // KBR drag handle spans full image dimension
            DragHandles[DragHandleAnchor.TopCenter].Bounds = new Rectangle(left, top - DragHandleSize, right - left, DragHandleSize);
            DragHandles[DragHandleAnchor.BottomCenter].Bounds = new Rectangle(left, bottom, right - left, DragHandleSize);
            DragHandles[DragHandleAnchor.MiddleLeft].Bounds = new Rectangle(left - DragHandleSize, top, DragHandleSize, bottom - top);
            DragHandles[DragHandleAnchor.MiddleRight].Bounds = new Rectangle(right, top, DragHandleSize, bottom - top);

        }

        private void ProcessSelectionMove(Point cursorPosition) {
            if (!IsMoving)
                return;

            Point imagePoint = PointToImage(cursorPosition, true);

            int x = Math.Max(0, imagePoint.X - DragOriginOffset.X);
            if (x + SelectionRegion.Width >= ViewSize.Width) {
                x = ViewSize.Width - (int)SelectionRegion.Width;
            }

            int y = Math.Max(0, imagePoint.Y - DragOriginOffset.Y);
            if (y + SelectionRegion.Height >= ViewSize.Height) {
                y = ViewSize.Height - (int)SelectionRegion.Height;
            }

            SelectionRegion = new RectangleF(x, y, SelectionRegion.Width, SelectionRegion.Height);
        }

        private void ProcessSelectionResize(Point cursorPosition) {
            if (!IsResizing)
                return;

            Point imagePosition = PointToImage(cursorPosition, true);

            // get the current selection
            float left = SelectionRegion.Left;
            float top = SelectionRegion.Top;
            float right = SelectionRegion.Right;
            float bottom = SelectionRegion.Bottom;

            // decide which edges we're resizing
            bool resizingTopEdge = ResizeAnchor >= DragHandleAnchor.TopLeft && ResizeAnchor <= DragHandleAnchor.TopRight;
            bool resizingBottomEdge = ResizeAnchor >= DragHandleAnchor.BottomLeft && ResizeAnchor <= DragHandleAnchor.BottomRight;
            bool resizingLeftEdge = ResizeAnchor == DragHandleAnchor.TopLeft || ResizeAnchor == DragHandleAnchor.MiddleLeft || ResizeAnchor == DragHandleAnchor.BottomLeft;
            bool resizingRightEdge = ResizeAnchor == DragHandleAnchor.TopRight || ResizeAnchor == DragHandleAnchor.MiddleRight || ResizeAnchor == DragHandleAnchor.BottomRight;

            // and resize!
            if (resizingTopEdge) {
                top = imagePosition.Y;
                if (bottom - top < MinimumSelectionSize.Height) {
                    top = bottom - MinimumSelectionSize.Height;
                }
            } else if (resizingBottomEdge) {
                bottom = imagePosition.Y;
                if (bottom - top < MinimumSelectionSize.Height) {
                    bottom = top + MinimumSelectionSize.Height;
                }
            }

            if (resizingLeftEdge) {
                left = imagePosition.X;
                if (right - left < MinimumSelectionSize.Width) {
                    left = right - MinimumSelectionSize.Width;
                }
            } else if (resizingRightEdge) {
                right = imagePosition.X;
                if (right - left < MinimumSelectionSize.Width) {
                    right = left + MinimumSelectionSize.Width;
                }
            }

            SelectionRegion = new RectangleF(left, top, right - left, bottom - top);
        }

        private void ResetDrag() {
            IsResizing = false;
            IsMoving = false;
            DragOrigin = Point.Empty;
            DragOriginOffset = Point.Empty;
        }

        private void SetCursor(Point point) {
            Cursor cursor;

            if (IsSelecting) {
                cursor = Cursors.Default;
            } else {
                DragHandleAnchor handleAnchor = IsResizing ? ResizeAnchor : HitTest(point);
                if (handleAnchor != DragHandleAnchor.None && DragHandles[handleAnchor].Enabled) {
                    switch (handleAnchor) {
                        case DragHandleAnchor.TopLeft:
                        case DragHandleAnchor.BottomRight:
                            cursor = Cursors.SizeNWSE;
                            break;
                        case DragHandleAnchor.TopCenter:
                        case DragHandleAnchor.BottomCenter:
                            cursor = Cursors.SizeNS;
                            break;
                        case DragHandleAnchor.TopRight:
                        case DragHandleAnchor.BottomLeft:
                            cursor = Cursors.SizeNESW;
                            break;
                        case DragHandleAnchor.MiddleLeft:
                        case DragHandleAnchor.MiddleRight:
                            cursor = Cursors.SizeWE;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
              // KBR disable move hint
              //else if (IsMoving || SelectionRegion.Contains(PointToImage(point)))
              //{
              //  cursor = Cursors.SizeAll;
              //}
              else {
                    cursor = Cursors.Default;
                }
            }

            Cursor = cursor;
        }

        private void StartResize(DragHandleAnchor anchor) {
            if (IsMoving || IsResizing) {
                throw new InvalidOperationException("A move or resize action is currently being performed.");
            }

            CancelEventArgs e = new CancelEventArgs();

            OnSelectionResizing(e);

            if (!e.Cancel) {
                ResizeAnchor = anchor;
                PreviousSelectionRegion = SelectionRegion;
                IsResizing = true;
            }
        }

        #endregion

        /// <summary>
        /// Draws the selection region.
        /// </summary>
        /// <param name="e">
        /// The <see cref="System.Windows.Forms.PaintEventArgs" /> instance containing the event data.
        /// </param>
        protected override void DrawSelection(PaintEventArgs e) {
            e.Graphics.SetClip(GetInsideViewPort(true));

            RectangleF rect = GetOffsetRectangle(SelectionRegion);
            RectangleF rect2 = GetImageViewPort();

            // GraphicsPath goes ballistic if either dimension is <= 0
            if (rect.Width < 1) rect.Width = 1;
            if (rect.Height < 1) rect.Height = 1;

            // Inverted drawing courtesy of
            // http://stackoverflow.com/questions/13039699/system-drawing-invert-region-build-out-of-path
            using (GraphicsPath pat1 = new GraphicsPath()) {
                pat1.AddRectangle(rect);

                using (GraphicsPath pat2 = new GraphicsPath()) {
                    pat2.AddRectangle(rect2);
                    pat2.AddPath(pat1, false);

                    using (Brush brush = new SolidBrush(Color.FromArgb(128, SelectionColor))) {
                        e.Graphics.FillPath(brush, pat2);
                    }
                }
            }
            using (Pen pen = new Pen(SelectionColor)) {
                e.Graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            }

            e.Graphics.ResetClip();
        }

    }
}
