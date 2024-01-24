using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace TTEDiskBuilder
{
    public enum PackingMethod
    {
        None,
        Shrinkler,
        ZX0,
        LZ,
        Deflate,
        Trim
    }

    public class DiskItem
    {
        public string Filename { get; set; }
        public string FileID { get; set; }
        public PackingMethod PackingMethod { get; set; }
        public bool Cacheable { get; set; }

        // internal props
        public int FileSize { get; set; }
        public int PackedSize { get; set; }
        public byte[] Data { get; set; }
        public byte[] PackedData { get; set; }
        public string Checksum { get; set; }
        public int DiskLocation { get; set; }
    }
}
