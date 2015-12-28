using System.Drawing;

namespace ImageGlass
{
    // Cyotek ImageBox
    // Copyright (c) 2010-2014 Cyotek.
    // http://cyotek.com
    // http://cyotek.com/blog/tag/imagebox

    // Licensed under the MIT License. See imagebox-license.txt for the full text.

    // If you use this control in your applications, attribution, donations or contributions are welcome.

    /// <summary>
    /// Dragging position
    /// </summary>
    public enum DragHandleAnchor
    {
        /// <summary>
        /// Not dragging
        /// </summary>
        None,
        /// <summary>
        /// Top left dragging
        /// </summary>
        TopLeft,
        /// <summary>
        /// Top center dragging
        /// </summary>
        TopCenter,
        /// <summary>
        /// Top right dragging
        /// </summary>
        TopRight,
        /// <summary>
        /// Middle left dragging
        /// </summary>
        MiddleLeft,
        /// <summary>
        /// Middle right dragging
        /// </summary>
        MiddleRight,
        /// <summary>
        /// bottom left dragging
        /// </summary>
        BottomLeft,
        /// <summary>
        /// bottom center dragging
        /// </summary>
        BottomCenter,
        /// <summary>
        /// bottom right dragging
        /// </summary>
        BottomRight
    }

    /// <summary>
    /// Dragging state
    /// </summary>
    public class DragHandle
    {
        /// <summary>
        /// Initialize dragging state
        /// </summary>
        /// <param name="anchor"></param>
        public DragHandle(DragHandleAnchor anchor) : this() {
            Anchor = anchor;
        }

        /// <summary>
        /// Default initialization of dragging state
        /// </summary>
        protected DragHandle() {
            Enabled = true;
            Visible = true;
        }

        /// <summary>
        /// Anchro type
        /// </summary>
        public DragHandleAnchor Anchor { get; private set; }

        /// <summary>
        /// Boundary
        /// </summary>
        public Rectangle Bounds { get; set; }

        /// <summary>
        /// Is active flag
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Is visible.
        /// </summary>
        public bool Visible { get; set; }
    }
}
