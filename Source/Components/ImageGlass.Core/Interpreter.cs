using System;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Drawing.IconLib;
using System.Text;
using System.Threading.Tasks;
using FreeImageAPI;
using ImageGlass.Common;
using Svg;

namespace ImageGlass.Core {
    public enum ImageFileType{
        Bmp, Tiff, Png, Gif, Jpeg, Wmf, Emf, WindowsIcon, Hdr
    }
    public class Interpreter {
        const int TAG_ORIENTATION = 0x0112;

        public static async Task<Either<Exception, Bitmap>> Load(IDiskManager diskManager, IoPriority priority, string path, bool forPreview){
            try{
                return Either<Exception, Bitmap>.Right(await unsafeLoad(diskManager, priority, path, forPreview));
            }
            catch (Exception ex){
                return Either<Exception, Bitmap>.Left(ex);
            }
        }
        static readonly string[] BitmapSignatures = new[]{
            "BM", // WIndows bitmap
            "BA", // Bitmap array
            "CI", // Color Icon
            "CP", // Color Pointer
            "IC", // Icon
            "PT" // Pointer
        };
        /// <summary>
        /// Checks the stream header if it matches with
        /// any of the supported image file types.
        /// </summary>
        /// <param name="stream">An open stream pointing to an image file.</param>
        /// <returns>true if the stream is an image file (BMP, TIFF, PNG, GIF, JPEG, WMF, EMF, ICO, CUR);
        /// false otherwise.</returns>
        public static Option<ImageFileType> IsImage(Stream stream)
        {
            // Sniff some bytes from the start of the stream
            // and check against magic numbers of supported 
            // image file formats
            byte[] header = new byte[10];
            stream.Seek(0, SeekOrigin.Begin);
            if (stream.Read(header, 0, header.Length) != header.Length)
                return Option<ImageFileType>.None();

            var bmpHeader = Encoding.ASCII.GetString(header, 0, 2);
            if (Array.Exists(BitmapSignatures, s => bmpHeader == s))
                return Option<ImageFileType>.Some(ImageFileType.Bmp);

            var tiffHeader = Encoding.ASCII.GetString(header, 0, 4);
            if (tiffHeader == "MM\x00\x2a"  // Big-endian
                || tiffHeader == "II\x2a\x00") // Little-endian
                return Option<ImageFileType>.Some(ImageFileType.Tiff);

            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
                return Option<ImageFileType>.Some(ImageFileType.Png);

            string gifHeader = Encoding.ASCII.GetString(header, 0, 4);
            if (gifHeader == "GIF8")
                return Option<ImageFileType>.Some(ImageFileType.Gif);

            if (header[0] == 0xFF && header[1] == 0xD8)
                return Option<ImageFileType>.Some(ImageFileType.Jpeg);

            if (header[0] == 0xD7 && header[1] == 0xCD && header[2] == 0xC6 && header[3] == 0x9A)
                return Option<ImageFileType>.Some(ImageFileType.Wmf);

            if (header[0] == 0x01 && header[1] == 0x00 && header[2] == 0x00 && header[3] == 0x00)
                return Option<ImageFileType>.Some(ImageFileType.Emf);

            if (header[0] == 0x00 && header[1] == 0x00 && header[2] == 0x01 && header[3] == 0x00  // ICO
             || header[0] == 0x00 && header[1] == 0x00 && header[2] == 0x02 && header[3] == 0x00) // CUR
                return Option<ImageFileType>.Some(ImageFileType.WindowsIcon);

            var hdrHeader = Encoding.ASCII.GetString(header, 2, 8);
            if (hdrHeader == "RADIANCE")
                return Option<ImageFileType>.Some(ImageFileType.Hdr);

            return Option<ImageFileType>.None();
        }
        static async Task<Bitmap> unsafeLoad(IDiskManager diskManager, IoPriority priority, string path, bool forPreview) {
            Contract.Requires(!string.IsNullOrEmpty(path));
            Contract.Ensures(Contract.Result<Bitmap>() != null);

            var ext = Path.GetExtension(path);
            Func<string, bool> extIs = s => string.Equals(ext, s, StringComparison.OrdinalIgnoreCase);
            
            //file *.hdr
            if (extIs(".hdr"))
                return await loadFif(diskManager, priority, path, FREE_IMAGE_FORMAT.FIF_HDR, forPreview? FREE_IMAGE_LOAD_FLAGS.RAW_PREVIEW : FREE_IMAGE_LOAD_FLAGS.RAW_DISPLAY);
            else if (extIs(".exr"))
                return await loadFif(diskManager, priority, path, FREE_IMAGE_FORMAT.FIF_EXR, forPreview? FREE_IMAGE_LOAD_FLAGS.RAW_PREVIEW : FREE_IMAGE_LOAD_FLAGS.RAW_DISPLAY);
            else if (extIs(".svg"))
                return await diskManager.ScheduleIO(path, priority, () => SvgDocument.Open(path).Draw());
            else if (extIs(".tga"))
                using (var tar = await diskManager.ScheduleIO(path, priority, () => new Paloma.TargaImage(path)))
                    return new Bitmap(tar.Image);
            else if (extIs(".psd")){
                var psd = new System.Drawing.PSD.PsdFile();
                var img = await diskManager.ScheduleIO(path, priority, () => psd.Load(path));
                return System.Drawing.PSD.ImageDecoder.DecodeImage(img);
            }
            else if (extIs(".ico"))
                return await ReadIconFile(diskManager, path, priority);
            else{
                var data = await diskManager.LoadFile(path, priority);
                var fs = new MemoryStream(data.ToOption().Get());
                var bmp = new Bitmap(fs, useIcm: true);

                if (bmp.RawFormat.Equals(ImageFormat.Jpeg)){
                    //read Exif rotation
                    var rotation = GetRotation(bmp);
                    return rotation == 0 ? bmp : ScaleDownRotateBitmap(bmp, 1.0f, rotation);
                }
                else
                    return bmp;
            }
        }

        static async Task<Bitmap> ReadIconFile(IDiskManager diskManager, string path, IoPriority priority)
        {
            MultiIcon mIcon = new MultiIcon();
            await diskManager.ScheduleIO(path, priority, () => mIcon.Load(path));

            //Try to get the largest image of it
            SingleIcon sIcon = mIcon[0];
            IconImage iImage = sIcon.OrderByDescending(ico => ico.Size.Width).ToList()[0];

            //Convert to bitmap
            return iImage.Icon.ToBitmap();
        }

        /// <summary>
        /// Returns Exif rotation in degrees. Returns 0 if the metadata 
        /// does not exist or could not be read. A negative value means
        /// the image needs to be mirrored about the vertical axis.
        /// </summary>
        /// <param name="img">Image.</param>
        static int GetRotation(Bitmap img){
            var orientationFlag = img
                .PropertyItems
                .Where(prop => prop.Id == TAG_ORIENTATION)
                .Select(prop => BitConverter.ToInt16(prop.Value, 0))
                .FirstOrDefault();

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


        /// <summary>
        /// Scales down and rotates an image.
        /// </summary>
        /// <param name="source">Original image</param>
        /// <param name="scale">Uniform scaling factor</param>
        /// <param name="angle">Rotation angle</param>
        /// <returns>Scaled and rotated image</returns>
        public static Bitmap ScaleDownRotateBitmap(Bitmap source, double scale, int angle) {
            Contract.Requires(source != null);
            Contract.Requires(angle % 90 == 0, "Rotation angle should be a multiple of 90 degrees.");

            // Since we do not upscale, if no rotation is needed, just return original.
            if (scale >= 1.0 && angle == 0)
                return new Bitmap(source);

            int sourceWidth = source.Width;
            int sourceHeight = source.Height;

            // Scale
            double xScale = Math.Max(1.0 / sourceWidth, scale);
            double yScale = Math.Max(1.0 / sourceHeight, scale);
            var newSize = new Size((int) Math.Ceiling(xScale*sourceWidth),
                                   (int) Math.Ceiling(yScale*sourceHeight));

            using (var fiBitmap = new FreeImageBitmap(source)){

                if (scale < 1) {
                    var result = fiBitmap.Rescale(newSize, FREE_IMAGE_FILTER.FILTER_BILINEAR);
                    Contract.Assert(result);
                }

                var rotateResult = fiBitmap.Rotate(-angle); // undo the rotation
                Contract.Assert(rotateResult);

                return fiBitmap.ToBitmap();
            }
        }
        static async Task<Bitmap> loadFif(IDiskManager diskManager, IoPriority priority, string path, FREE_IMAGE_FORMAT format, FREE_IMAGE_LOAD_FLAGS loadFlags){
            var hdr = await diskManager.ScheduleIO(path, priority, () => FreeImage.Load(format, path, loadFlags));
            var bmp = FreeImage.GetBitmap(FreeImage.ToneMapping(hdr, FREE_IMAGE_TMO.FITMO_DRAGO03, 2.2, 0));
            FreeImage.Unload(hdr);
            return bmp;
        }
    }
}
