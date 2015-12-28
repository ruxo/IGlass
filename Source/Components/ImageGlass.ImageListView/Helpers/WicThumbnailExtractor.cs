using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageGlass.Common;
using ImageGlass.Common.Extensions;
using ImageGlass.Core;

namespace ImageGlass.ImageListView.Helpers{
    /// <summary>
    /// Thumbnail extractor that uses Windows Imaging Components
    /// </summary>
    public class WicThumbnailExtractor : ThumbnailExtractor{
        static readonly string[] WICPathOrientation = { "/app1/ifd/{ushort=274}", "/xmp/tiff:Orientation" };

        /// <summary>
        /// Instantiate extractor
        /// </summary>
        public WicThumbnailExtractor(IDiskManager diskManager, bool useEmbedded, bool useExifOrientation) : base(diskManager, useEmbedded, useExifOrientation){}

        static Option<BitmapFrame> CreateThumbnailBitmap(Image image){
            using(var stream = new MemoryStream())
                try {
                    image.Save(stream, ImageFormat.Bmp);
                    // Performance vs image quality settings.
                    // Selecting BitmapCacheOption.None speeds up thumbnail generation of large images tremendously
                    // if the file contains no embedded thumbnail. The image quality is only slightly worse.
                    stream.Seek(0, SeekOrigin.Begin);
                    var frameWpf = BitmapFrame.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                    return Option<BitmapFrame>.Some(frameWpf);
                } catch{
                    return Option<BitmapFrame>.None();
                }
        }

        /// <summary>
        /// Creates a thumbnail from the given image.
        /// </summary>
        /// <param name="image">The source image.</param>
        /// <param name="size">Requested image size.</param>
        /// <returns>The thumbnail image from the given image.</returns>
        public override Option<Image> FromImage(Image image, Size size){
            var rotation = GetRotation(image);

            if (UseEmbeddedThumbnails)
                return CreateThumbnailBitmap(image)
                        .Map(frameWpf => GetEmbeddedThumbnail(frameWpf, size, rotation))
                        .GetOrElse(() => GetThumbnailBmp(image, size, rotation));
            else
                return GetThumbnailBmp(image, size, rotation);
        }
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
        public override Task<Option<Image>> FromFile(string filename, Size size){
            return GetThumbnailBmp(filename, size);
            /*
            return LoadImageStream(filename)
                .Chain(stream =>{
                    if (UseEmbeddedThumbnails)
                        return LoadBitmapFrame(stream)
                            .Map(frameWpf => GetEmbeddedThumbnail(frameWpf, size, GetRotation(frameWpf)))
                            .GetOrElse(() => GetThumbnailBmp(stream, size));
                    else
                        return GetThumbnailBmp(stream, size);
                });
                */
        }

        static Option<BitmapFrame> LoadBitmapFrame(Stream stream){
            // Performance vs image quality settings.
            // Selecting BitmapCacheOption.None speeds up thumbnail generation of large images tremendously
            // if the file contains no embedded thumbnail. The image quality is only slightly worse.
            return Option<BitmapFrame>
                .From(() => BitmapFrame.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None));
        }

        #region Rotation

        int GetRotation(BitmapFrame frameWpf){
            return UseExifOrientation ? GetRotationFromImage(frameWpf) : 0;
        }

        /// <summary>
        /// Returns Exif rotation in degrees. Returns 0 if the metadata 
        /// does not exist or could not be read. A negative value means
        /// the image needs to be mirrored about the vertical axis.
        /// </summary>
        /// <param name="frameWpf">Image source.</param>
        static int GetRotationFromImage(ImageSource frameWpf)
        {
            return Option<BitmapMetadata>
                .From(() => frameWpf.Metadata as BitmapMetadata)
                .Chain(data => GetMetadataObject(data, WICPathOrientation))
                .Map(Convert.ToInt32)
                .Map(InterpretExifOrientation)
                .Get(Prelude.Constant(0), Prelude.Identity);
        }
        static Option<object> GetMetadataObject(BitmapMetadata metadata, IEnumerable<string> query)
            => query.Select(metadata.GetQuery).TryFirst(val => val != null);

        #endregion

        /// <summary>
        /// Converts BitmapSource to Bitmap.
        /// </summary>
        /// <param name="sourceWpf">BitmapSource</param>
        /// <returns>Bitmap</returns>
        static Bitmap ConvertToBitmap(BitmapSource sourceWpf) {
            var bmpWpf = sourceWpf;

            // PixelFormat settings/conversion
            var formatBmp = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
            if (sourceWpf.Format == PixelFormats.Bgr24)
                formatBmp = System.Drawing.Imaging.PixelFormat.Format24bppRgb;
            else if (sourceWpf.Format == PixelFormats.Pbgra32)
                formatBmp = System.Drawing.Imaging.PixelFormat.Format32bppPArgb;
            else if (sourceWpf.Format != PixelFormats.Bgra32){
                // Convert BitmapSource
                FormatConvertedBitmap convertWpf = new FormatConvertedBitmap();
                convertWpf.BeginInit();
                convertWpf.Source = sourceWpf;
                convertWpf.DestinationFormat = PixelFormats.Bgra32;
                convertWpf.EndInit();
                bmpWpf = convertWpf;
            }

            // Copy/Convert to Bitmap
            Bitmap bmp = new Bitmap(bmpWpf.PixelWidth, bmpWpf.PixelHeight, formatBmp);
            Rectangle rect = new Rectangle(Point.Empty, bmp.Size);
            BitmapData data = bmp.LockBits(rect, ImageLockMode.WriteOnly, formatBmp);
            bmpWpf.CopyPixels(System.Windows.Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
            bmp.UnlockBits(data);

            return bmp;
        }
        static double GetThumbnailScale(BitmapSource sourceWpf, Size size, int rotate){
            return rotate%180 != 0
                ? Math.Min(size.Height/(double) sourceWpf.PixelWidth, size.Width/(double) sourceWpf.PixelHeight)
                : Math.Min(size.Width/(double) sourceWpf.PixelWidth, size.Height/(double) sourceWpf.PixelHeight);
        }
        static Option<Image> GetEmbeddedThumbnail(BitmapFrame bmp, Size size, int rotate){
            var thumbnail = Option<BitmapSource>.From(() => bmp.Thumbnail);
            var preview = Option<BitmapSource>.From(() => bmp.Decoder?.Preview);
            var decoderFrame = Option<BitmapSource>.From(() => bmp.Decoder?.Frames[0]);

            // any better pattern? :(
            var list = new[]{thumbnail, preview, decoderFrame}
                        .Where(opt => opt.IsSome)
                        .Select(opt => opt.Get())
                        .ToArray();
            var scaled = list.Select(src => GetThumbnailScale(src, size, rotate));
            return list
                .Zip(scaled, Tuple.Create)
                .TryFirst(result => result.Item2 <= 1.0f)
                .Map(result => (Image) ConvertToBitmap(ScaleDownRotateBitmap(result.Item1, result.Item2, rotate)));
            
        }
        const int TagThumbnailData = 0x501B;
        Option<Image> GetEmbeddedThumbnail(Stream stream, Size size){
            if (UseEmbeddedThumbnails)
                using (Image img = Image.FromStream(stream, false, false)){
                    if (img.PropertyIdList.Any(index => index == TagThumbnailData)){
                        var rawImage = img.GetPropertyItem(TagThumbnailData).Value;
                        Image source;
                        using (var memStream = new MemoryStream(rawImage))
                            source = Image.FromStream(memStream);
                        // Check that the embedded thumbnail is large enough.
                        if (Math.Max((double) source.Width/size.Width,
                                     (double) source.Height/size.Height) >= 1.0f)
                            return Option<Image>.Some(source);
                        source.Dispose();
                    }
                }
            return Option<Image>.None();
        }
        static Option<Image> GetGifThumbnail(Stream stream){
            byte[] gifSignature = new byte[4];
            stream.Read(gifSignature, 0, 4);
            if (Encoding.ASCII.GetString(gifSignature) == "GIF8"){
                stream.Seek(0, SeekOrigin.Begin);
                var streamCopy = new MemoryStream();
                var buffer = new byte[32768];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0){
                    streamCopy.Write(buffer, 0, read);
                }
                // Append the missing semicolon
                streamCopy.Seek(-1, SeekOrigin.End);
                if (streamCopy.ReadByte() != 0x3b)
                    streamCopy.WriteByte(0x3b);
                return Option<Image>.Some( Image.FromStream(streamCopy));
            }
            return Option<Image>.None();
        }

            /// <summary>
        /// Creates a thumbnail from the given image file.
        /// </summary>
        /// <param name="filename">The filename pointing to an image.</param>
        /// <param name="size">Requested image size.</param>
        /// <returns>The image from the given file.</returns>
        async Task<Option<Image>> GetThumbnailBmp(string filename, Size size){
            var image = await GetPreviewBmp(filename, size);
            return image
                .Map(source =>{
                    var rotate = GetRotation(source);
                    var scale = rotate%180 != 0
                        ? Math.Min(size.Height/(double) source.Width, size.Width/(double) source.Height)
                        : Math.Min(size.Width/(double) source.Width, size.Height/(double) source.Height);

                    return ScaleDownRotateBitmap(source, scale, rotate);
                });
            }
        Task<Option<Image>> GetPreviewBmp(string filename, Size size){
            var fileExt = Path.GetExtension(filename);
            Predicate<string> extIs = ext => string.Equals(ext, fileExt, StringComparison.OrdinalIgnoreCase);

            if (extIs(".svg") || extIs(".hdr") || extIs(".exr"))
                return Interpreter
                    .Load(DiskManager, IoPriority.Background, filename, forPreview: true)
                    .Map(opt => opt.ToOption().Map(b => (Image) b));
            else
                return LoadImageStream(DiskManager, filename)
                    .Map(opt => opt.Chain(stream => GetEmbeddedThumbnail(stream, size)
                               .FailedTry(() => GetGifThumbnail(stream))
                               .FailedTry(() => LoadImage(stream))));
        }
        /// <summary>
        /// Scales down and rotates a Wpf bitmap.
        /// </summary>
        /// <param name="sourceWpf">Original Wpf bitmap</param>
        /// <param name="scale">Uniform scaling factor</param>
        /// <param name="angle">Rotation angle</param>
        /// <returns>Scaled and rotated Wpf bitmap</returns>
        private static BitmapSource ScaleDownRotateBitmap(BitmapSource sourceWpf, double scale, int angle)
        {
            Contract.Requires(angle % 90 == 0, "Rotation angle should be a multiple of 90 degrees.");

            // Do not upscale and no rotation.
            if ((float)scale >= 1.0f && angle == 0)
            {
                return sourceWpf;
            }

            // Set up the transformed thumbnail
            TransformedBitmap thumbWpf = new TransformedBitmap();
            thumbWpf.BeginInit();
            thumbWpf.Source = sourceWpf;
            TransformGroup transform = new TransformGroup();

            // Rotation
            if (Math.Abs(angle) % 360 != 0)
                transform.Children.Add(new RotateTransform(Math.Abs(angle)));

            // Scale
            if ((float)scale < 1.0f || angle < 0) // Only downscale
            {
                double xScale = Math.Min(1.0, Math.Max(1.0 / sourceWpf.PixelWidth, scale));
                double yScale = Math.Min(1.0, Math.Max(1.0 / sourceWpf.PixelHeight, scale));

                if (angle < 0)
                    xScale = -xScale;
                transform.Children.Add(new ScaleTransform(xScale, yScale));
            }

            // Apply the tranformation
            thumbWpf.Transform = transform;
            thumbWpf.EndInit();

            return thumbWpf;
        }
    }
}