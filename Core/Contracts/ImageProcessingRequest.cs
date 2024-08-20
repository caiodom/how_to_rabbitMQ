using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Contracts
{
    public class ImageProcessingRequest
    {
        public string ImageUrl { get; set; }
        public string FilterType { get; set; }
        public string ContentType { get; set; }
    }
}
