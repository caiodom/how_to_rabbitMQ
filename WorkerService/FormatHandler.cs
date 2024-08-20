using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Qoi;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats;
using System.Net.Mime;


namespace WorkerService
{
    public static class FormatHandler
    {
        public static IImageEncoder GetEncoder(ImageFormat format)
        {
            return format switch
            {
                ImageFormat.Png => new PngEncoder(),
                ImageFormat.Jpeg => new JpegEncoder(),
                ImageFormat.Gif => new GifEncoder(),
                ImageFormat.Bmp => new BmpEncoder(),
                ImageFormat.Pbm => new PbmEncoder(),
                ImageFormat.Tga => new TgaEncoder(),
                ImageFormat.Tiff => new TiffEncoder(),
                ImageFormat.Webp => new WebpEncoder(),
                ImageFormat.Qoi => new QoiEncoder(),
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
            };
        }

        public static ImageFormat GetFormat(string contentType)
        {
            return contentType.ToLower() switch
            {
                string ct when (ct.Contains("jpeg")) => ImageFormat.Jpeg,
                string ct when (ct.Contains("png")) => ImageFormat.Png,
                string ct when (ct.Contains("gif")) => ImageFormat.Gif,
                string ct when (ct.Contains("bmp")) => ImageFormat.Bmp,
                string ct when (ct.Contains("pbm")) => ImageFormat.Pbm,
                string ct when (ct.Contains("tga")) => ImageFormat.Tga,
                string ct when (ct.Contains("tiff")) => ImageFormat.Tiff,
                string ct when (ct.Contains("webp")) => ImageFormat.Webp,
                string ct when (ct.Contains("qoi")) => ImageFormat.Qoi,
                _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, null)

            };

        }
    }
}
