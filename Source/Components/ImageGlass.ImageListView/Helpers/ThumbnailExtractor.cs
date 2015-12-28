// ImageListView - A listview control for image files
// Copyright (C) 2009 Ozgur Ozcitak
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// Ozgur Ozcitak (ozcitak@yahoo.com)
//
// WIC support coded by Jens

using System.Drawing;
using ImageGlass.Common;
using ImageGlass.Core;

namespace ImageGlass.ImageListView.Helpers {
    /// <summary>
    /// Extracts thumbnails from images.
    /// </summary>
    public class DefaultThumbnailExtractor : ThumbnailExtractor {
        /// <summary>
        /// Instantiate extractor
        /// </summary>
        /// <param name="diskManager"></param>
        /// <param name="useEmbedded">Embedded thumbnail usage.</param>
        /// <param name="useExifOrientation">true to automatically rotate images based on Exif orientation; otherwise false.</param>
        public DefaultThumbnailExtractor(IDiskManager diskManager, bool useEmbedded, bool useExifOrientation) : base(diskManager, useEmbedded, useExifOrientation){ }

        /// <summary>
        /// Creates a thumbnail from the given image.
        /// </summary>
        /// <param name="image">The source image.</param>
        /// <param name="size">Requested image size.</param>
        /// <returns>The thumbnail image from the given image.</returns>
        public override Option<Image> FromImage(Image image, Size size) => GetThumbnailBmp(image, size, GetRotation(image));
    }
}
