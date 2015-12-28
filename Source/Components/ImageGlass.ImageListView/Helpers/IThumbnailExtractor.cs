using System;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using ImageGlass.Common;
using ImageGlass.Common.Extensions;
using ImageGlass.Core;

namespace ImageGlass.ImageListView.Helpers{
    [ContractClassFor(typeof(IThumbnailExtractor))]
    class IThumbnailExtractorContracts : IThumbnailExtractor{
        public Option<Image> FromImage(Image image, Size size){
            Contract.Requires(image != null);
            Contract.Requires(size.Width > 0 && size.Height > 0, "Thumbnail size cannot be empty.");
            Contract.Ensures(Contract.Result<Option<Image>>() != null);
            return null;
        }
        public Task<Option<Image>> FromFile(string filename, Size size){
            Contract.Requires(!string.IsNullOrWhiteSpace(filename), "Filename cannot be empty");
            Contract.Requires(size.Width > 0 && size.Height > 0, "Thumbnail size cannot be empty.");
            Contract.Ensures(Contract.Result<Option<Image>>() != null);
            return null;
        }
    }

    /// <summary>
    /// Thumbnail maker
    /// </summary>
    [ContractClass(typeof(IThumbnailExtractorContracts))]
    public interface IThumbnailExtractor{
        /// <summary>
        /// Creates a thumbnail from the given image.
        /// </summary>
        /// <param name="image">The source image.</param>
        /// <param name="size">Requested image size.</param>
        /// <returns>The thumbnail image from the given image.</returns>
        Option<Image> FromImage(Image image, Size size);
        /// <summary>
        /// Creates a thumbnail from the given image file.
        /// </summary>
        /// <comment>
        /// This much faster .NET 3.0 method replaces the original .NET 2.0 method.
        /// The image quality is slightly reduced (low filtering mode).
        /// </comment>
        /// <param name="filename">The filename pointing to an image.</param>
        /// <param name="size">Requested image size.</param>
        /// <returns>The thumbnail image from the given file.</returns>
        Task<Option<Image>> FromFile(string filename, Size size);
    }

    /// <summary>
    /// Common class of thumbnail maker
    /// </summary>
    public abstract class ThumbnailExtractor : IThumbnailExtractor{
        private const int TagOrientation = 0x0112;

        /// <summary>
        /// Disk Manager for I/O operation
        /// </summary>
        protected IDiskManager DiskManager { get; }
        /// <summary>
        /// Should auto rotate?
        /// </summary>
        protected bool UseExifOrientation { get; }
        /// <summary>
        /// ...
        /// </summary>
        protected bool UseEmbeddedThumbnails { get; }

        /// <summary>
        /// Initiation
        /// </summary>
        /// <param name="diskManager"></param>
        /// <param name="useEmbedded"></param>
        /// <param name="useExifOrientation"></param>
        protected ThumbnailExtractor(IDiskManager diskManager, bool useEmbedded, bool useExifOrientation){
            Contract.Requires(diskManager != null);

            DiskManager = diskManager;
            UseEmbeddedThumbnails = useEmbedded;
            UseExifOrientation = useExifOrientation;
        }

        /// <summary>
        /// Creates a thumbnail from the given image.
        /// </summary>
        /// <param name="image">The source image.</param>
        /// <param name="size">Requested image size.</param>
        /// <returns>The thumbnail image from the given image.</returns>
        public abstract Option<Image> FromImage(Image image, Size size);
        /// <summary>
        /// Creates a thumbnail from the given image file.
        /// </summary>
        /// <comment>
        /// This much faster .NET 3.0 method replaces the original .NET 2.0 method.
        /// The image quality is slightly reduced (low filtering mode).
        /// </comment>
        /// <param name="filename">The filename pointing to an image.</param>
        /// <param name="size">Requested image size.</param>
        /// <returns>The thumbnail image from the given file.</returns>
        public virtual Task<Option<Image>> FromFile(string filename, Size size){
            return LoadImage(filename)
                .Map(opt => opt.Chain(img => FromImage(img, size)));
        }

        /// <summary>
        /// Load image from <paramref name="filename"/>
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        protected Task<Option<Image>> LoadImage(string filename){
            return LoadImageStream(DiskManager, filename).Map(opt => opt.Chain(LoadImage));
        }
        /// <summary>
        /// Load image from stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        protected Option<Image> LoadImage(Stream stream) => Option<Image>.From(() => Image.FromStream(stream, false, false));

        /// <summary>
        /// Util to get stream option
        /// </summary>
        /// <param name="diskManager"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        protected static async Task<Option<Stream>> LoadImageStream(IDiskManager diskManager, string filename){
            var content = await diskManager.LoadFile(filename, IoPriority.Background);
            return content
                .ToOption()
                .Map(bytes =>{
                    var stream = new MemoryStream(bytes);
                    return Interpreter.IsImage(stream).IsSome ? (Stream) stream : null;
                });
        }

        #region Rotation

        /// <summary>
        /// Get Rotation of the given image depending on flag 
        /// </summary>
        protected int GetRotation(Image img){
            return UseExifOrientation ? GetRotationFromImage(img) : 0;
        }

        /// <summary>
        /// Returns Exif rotation in degrees. Returns 0 if the metadata 
        /// does not exist or could not be read. A negative value means
        /// the image needs to be mirrored about the vertical axis.
        /// </summary>
        /// <param name="img">Image.</param>
        static int GetRotationFromImage(Image img){
            return img
                .PropertyItems
                .TryFirst(prop => prop.Id == TagOrientation)
                .Map(prop => (int) BitConverter.ToUInt16(prop.Value, 0))
                .Map(InterpretExifOrientation)
                .Get(Prelude.Constant(0), Prelude.Identity);
        }
        /// <summary>
        /// Translate EXIF orientation to degree
        /// </summary>
        /// <param name="orientationFlag"></param>
        /// <returns></returns>
        protected static int InterpretExifOrientation(int orientationFlag){
            switch (orientationFlag){
                case 1: return 0;
                case 2: return -360;
                case 3: return 180;
                case 4: return -180;
                case 5: return -90;
                case 6: return 90;
                case 7: return -270;
                case 8: return 270;
                default: return 0;
            }
        }

        #endregion

        /// <summary>
        /// Simply get thumbnail from bitmap stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        protected static Option<Image> GetThumbnailBmp(Stream stream, Size size){
            var image = Image.FromStream(stream, false, false);
            return GetThumbnailBmp(image, size, GetRotationFromImage(image));
        }

        /// <summary>
        /// Creates a thumbnail from the given image.
        /// </summary>
        /// <param name="image">The source image.</param>
        /// <param name="size">Requested image size.</param>
        /// <param name="rotate">Rotation angle.</param>
        /// <returns>The image from the given file.</returns>
        protected static Option<Image> GetThumbnailBmp(Image image, Size size, int rotate) {
            Contract.Requires(image != null);
            Contract.Requires(size.Width > 0 && size.Height > 0);
            Contract.Ensures(Contract.Result<Option<Image>>() != null);

            var scale = rotate%180 != 0
                ? Math.Min(size.Height/(double) image.Width, size.Width/(double) image.Height)
                : Math.Min(size.Width/(double) image.Width, size.Height/(double) image.Height);

            return Option<Image>.From(() => ScaleDownRotateBitmap(image, scale, rotate));
        }
        /// <summary>
        /// Scales down and rotates an image.
        /// </summary>
        /// <param name="source">Original image</param>
        /// <param name="scale">Uniform scaling factor</param>
        /// <param name="angle">Rotation angle</param>
        /// <returns>Scaled and rotated image</returns>
        protected static Image ScaleDownRotateBitmap(Image source, double scale, int angle) {
            return Interpreter.ScaleDownRotateBitmap((Bitmap)source, scale, angle);
        }
    }
}