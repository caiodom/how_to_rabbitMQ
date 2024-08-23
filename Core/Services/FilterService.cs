using Core.Interfaces;
using Core.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    public class FilterService : IFilterService
    {

        public FilterService() { }
        public async Task FilterHandler(string imagePath, string filterType, string contentType)
        {
            using (var image = await Image.LoadAsync<Rgba32>(imagePath))
            {
                switch (filterType.ToLower())
                {
                    case "grayscale":
                        image.Mutate(x => x.Grayscale());
                        break;
                    case "negative":
                        image.Mutate(x => x.Invert());
                        break;
                    case "sepia":
                        image.Mutate(x => x.Sepia());
                        break;
                    case "blur":
                        image.Mutate(x => x.GaussianBlur());
                        break;
                    default:
                        throw new ArgumentException("Invalid filter type");

                }
                var encoder = FormatHandler.GetEncoder(FormatHandler.GetFormat(contentType));
                await image.SaveAsync(imagePath, encoder);
            }

        }

        public async Task FilterHandler(MemoryStream stream, string filterType, string contentType)
        {

            using (var image = await Image.LoadAsync(stream))
            {
                switch (filterType.ToLower())
                {
                    case "grayscale":
                        image.Mutate(x => x.Grayscale());
                        break;
                    case "negative":
                        image.Mutate(x => x.Invert());
                        break;
                    case "sepia":
                        image.Mutate(x => x.Sepia());
                        break;
                    case "blur":
                        image.Mutate(x => x.GaussianBlur());
                        break;
                    default:
                        throw new ArgumentException("Invalid filter type");

                }

                    var encoder= FormatHandler.GetEncoder(FormatHandler.GetFormat(contentType));

                    await image.SaveAsync(stream, encoder);

                stream.Position = 0;
   
            }
        }
    }
}
