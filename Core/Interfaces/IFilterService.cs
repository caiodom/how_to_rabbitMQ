using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;

namespace Core.Interfaces
{
    public interface IFilterService
    {
        Task FilterHandler(string imagePath, string filterType,string contentType);
        Task FilterHandler(MemoryStream stream, string filterType,string contentType);

    }
}
