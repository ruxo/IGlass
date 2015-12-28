using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ImageGlass.Common;
using ImageGlass.Core;
using ImageGlass.ImageListView.Helpers;

namespace ImageGlass.ImageListView
{
    /// <summary>
    /// Represents the built-in adaptors.
    /// </summary>
    public static class ImageListViewItemAdaptors
    {
        #region FileSystemAdaptor
        /// <summary>
        /// Represents a file system adaptor.
        /// </summary>
        public class FileSystemAdaptor : ImageListView.ImageListViewItemAdaptor
        {
            readonly IDiskManager diskManager;
            private bool disposed;

            /// <summary>
            /// Initializes a new instance of the <see cref="FileSystemAdaptor"/> class.
            /// </summary>
            /// <param name="diskManager"></param>
            public FileSystemAdaptor(IDiskManager diskManager) {
                this.diskManager = diskManager;
                Contract.Requires(diskManager != null);
                disposed = false;
            }

            /// <summary>
            /// Returns the thumbnail image for the given item.
            /// </summary>
            /// <param name="key">Item key.</param>
            /// <param name="size">Requested image size.</param>
            /// <param name="useEmbeddedThumbnails">Embedded thumbnail usage.</param>
            /// <param name="useExifOrientation">true to automatically rotate images based on Exif orientation; otherwise false.</param>
            /// <param name="useWIC">true to use Windows Imaging Component; otherwise false.</param>
            /// <returns>The thumbnail image from the given item or null if an error occurs.</returns>
            public override async Task<Option<Image>> GetThumbnail(object key, Size size, bool useEmbeddedThumbnails, bool useExifOrientation, bool useWIC)
            {
                if (disposed)
                    return Option<Image>.None();

                var extractor = useWIC
                    ? (IThumbnailExtractor) new WicThumbnailExtractor(diskManager, useEmbeddedThumbnails, useExifOrientation)
                    : new DefaultThumbnailExtractor(diskManager, useEmbeddedThumbnails, useExifOrientation);
                var filename = (string)key;
                return File.Exists(filename) ? await extractor.FromFile(filename, size) : Option<Image>.None();
            }
            /// <summary>
            /// Returns the path to the source image for use in drag operations.
            /// </summary>
            /// <param name="key">Item key.</param>
            /// <returns>The path to the source image.</returns>
            public override string GetSourceImage(object key)
            {
                if (disposed)
                    return null;

                string filename = (string)key;
                return filename;
            }
            /// <summary>
            /// Returns the details for the given item.
            /// </summary>
            /// <param name="key">Item key.</param>
            /// <param name="useWIC">true to use Windows Imaging Component; otherwise false.</param>
            /// <returns>An array of tuples containing item details or null if an error occurs.</returns>
            public override Utility.Tuple<ColumnType, string, object>[] GetDetails(object key, bool useWIC)
            {
                if (disposed)
                    return null;

                string filename = (string)key;
                List<Utility.Tuple<ColumnType, string, object>> details = new List<Utility.Tuple<ColumnType, string, object>>();

                // Get file info
                if (File.Exists(filename))
                {
                    FileInfo info = new FileInfo(filename);
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.DateCreated, string.Empty, info.CreationTime));
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.DateAccessed, string.Empty, info.LastAccessTime));
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.DateModified, string.Empty, info.LastWriteTime));
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.FileSize, string.Empty, info.Length));
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.FilePath, string.Empty, info.DirectoryName ?? ""));

                    // Get metadata
                    MetadataExtractor metadata = MetadataExtractor.FromFile(filename, useWIC);
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.Dimensions, string.Empty, new Size(metadata.Width, metadata.Height)));
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.Resolution, string.Empty, new SizeF((float)metadata.DPIX, (float)metadata.DPIY)));
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.ImageDescription, string.Empty, metadata.ImageDescription ?? ""));
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.EquipmentModel, string.Empty, metadata.EquipmentModel ?? ""));
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.DateTaken, string.Empty, metadata.DateTaken));
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.Artist, string.Empty, metadata.Artist ?? ""));
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.Copyright, string.Empty, metadata.Copyright ?? ""));
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.ExposureTime, string.Empty, (float)metadata.ExposureTime));
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.FNumber, string.Empty, (float)metadata.FNumber));
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.ISOSpeed, string.Empty, (ushort)metadata.ISOSpeed));
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.UserComment, string.Empty, metadata.Comment ?? ""));
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.Rating, string.Empty, (ushort)metadata.Rating));
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.Software, string.Empty, metadata.Software ?? ""));
                    details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.FocalLength, string.Empty, (float)metadata.FocalLength));
                }

                return details.ToArray();
            }
            /// <summary>
            /// Performs application-defined tasks associated with freeing,
            /// releasing, or resetting unmanaged resources.
            /// </summary>
            public override void Dispose()
            {
                disposed = true;
            }
        }
        #endregion

        #region URIAdaptor
        /// <summary>
        /// Represents a URI adaptor.
        /// </summary>
        public class URIAdaptor : ImageListView.ImageListViewItemAdaptor
        {
            private bool disposed;

            /// <summary>
            /// Initializes a new instance of the <see cref="URIAdaptor"/> class.
            /// </summary>
            public URIAdaptor()
            {
                disposed = false;
            }

            /// <summary>
            /// Returns the thumbnail image for the given item.
            /// </summary>
            /// <param name="key">Item key.</param>
            /// <param name="size">Requested image size.</param>
            /// <param name="useEmbeddedThumbnails">Embedded thumbnail usage.</param>
            /// <param name="useExifOrientation">true to automatically rotate images based on Exif orientation; otherwise false.</param>
            /// <param name="useWIC">true to use Windows Imaging Component; otherwise false.</param>
            /// <returns>The thumbnail image from the given item or null if an error occurs.</returns>
            public override async Task<Option<Image>> GetThumbnail(object key, Size size, bool useEmbeddedThumbnails, bool useExifOrientation, bool useWIC) {
                if (disposed)
                    return Option<Image>.None();

                // TODO ah.. my bad decision.. DiskManager should pass as parameter to method instead of constructor :(
                var dummyManager = new DiskManager();
                string uri = (string)key;
                var extractor = useWIC
                    ? (IThumbnailExtractor) new WicThumbnailExtractor(dummyManager, useEmbeddedThumbnails, useExifOrientation)
                    : new DefaultThumbnailExtractor(dummyManager, useEmbeddedThumbnails, useExifOrientation);
                try {
                    using (var client = new HttpClient()) {
                        var imageData = await client.GetByteArrayAsync(uri);
                        using (var stream = new MemoryStream(imageData))
                        using (var sourceImage = Image.FromStream(stream))
                            return extractor.FromImage(sourceImage, size);
                    }
                } catch {
                    return Option<Image>.None();
                }
            }
            /// <summary>
            /// Returns the path to the source image for use in drag operations.
            /// </summary>
            /// <param name="key">Item key.</param>
            /// <returns>The path to the source image.</returns>
            public override string GetSourceImage(object key)
            {
                if (disposed)
                    return null;

                string uri = (string)key;
                try
                {
                    string filename = Path.GetTempFileName();
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(uri, filename);
                        return filename;
                    }
                }
                catch
                {
                    return null;
                }
            }
            /// <summary>
            /// Returns the details for the given item.
            /// </summary>
            /// <param name="key">Item key.</param>
            /// <param name="useWIC">true to use Windows Imaging Component; otherwise false.</param>
            /// <returns>An array of 2-tuples containing item details or null if an error occurs.</returns>
            public override Utility.Tuple<ColumnType, string, object>[] GetDetails(object key, bool useWIC)
            {
                if (disposed)
                    return null;

                string uri = (string)key;
                List<Utility.Tuple<ColumnType, string, object>> details = new List<Utility.Tuple<ColumnType, string, object>>();

                details.Add(new Utility.Tuple<ColumnType, string, object>(ColumnType.Custom, "URL", uri));

                return details.ToArray();
            }
            /// <summary>
            /// Performs application-defined tasks associated with freeing,
            /// releasing, or resetting unmanaged resources.
            /// </summary>
            public override void Dispose()
            {
                disposed = true;
            }
        }
        #endregion
    }
}
