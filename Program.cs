using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TTEDiskBuilder
{
    public class FileDupe
    {
        public string Basefile { get; set; }    
        public List<string> Files { get; set; } = new List<string>();   
        public override string ToString()
        {
            return $"{Basefile} - {Files.Count}";
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(@".__.  .__.  .______.______________________ .");
            Console.WriteLine(@"\\ |__|  |__|  ___//                      \\");
            Console.WriteLine(@"| ____/ ____/ ___/:    THE TWITCH ELITE   //");
            Console.WriteLine(@"|  |__|  |__|  |  |      ..PRESENTS..     \\");
            Console.WriteLine(@"|  |  |  |  | :|  |                       //");
            Console.WriteLine(@">> |  | :|  |  `  >>  Disk Builder - C#  << ");
            Console.WriteLine(@"| :|  |  |  |_____|                       \\");
            Console.WriteLine(@"|  |  |  `  |::.tHE                       //");
            Console.WriteLine(@"|  `  |_____|tWITCH                       \\");
            Console.WriteLine(@"//____|::::::.eLITE::.________________fZn_//");
            Console.WriteLine();

            var folder = args[0];
            Console.WriteLine($"Opening folder - {folder}");

            try
            {
                BuildDisk(folder, true);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }

        static void BuildDisk(string sourcePath, bool repack)
        {
            var jsonFile = Path.Combine(sourcePath, "disk.json");
            if (!File.Exists(jsonFile))
            {
                Console.WriteLine("Cannot find disk.json!");
                return;
            }

            var json = File.ReadAllText(jsonFile);
            var diskItems = JsonConvert.DeserializeObject<List<DiskItem>>(json);


            /*
             * disabled parallel file compressor as salvador is quicker!
            Parallel.ForEach(diskItems, diskItem =>
            {
                LoadDiskItem(diskItem, sourcePath, repack);
            });
            */

            Console.WriteLine("Adding files...");
            foreach (var diskItem in diskItems)
            {
                LoadDiskItem(diskItem, sourcePath, repack);
            }

            /*
             * disabled json write as no longer need to track files
            json = JsonConvert.SerializeObject(diskItems, Formatting.Indented);
            File.WriteAllText(Path.Combine(sourcePath, "disk.json"), json);
            */

            var diskData = MergeData(diskItems);

            var disk = MakeDisk(diskItems, diskData, sourcePath);

            File.WriteAllBytes(Path.Combine(sourcePath, "final.adf"), disk);
        }


        static void BootBlockCheckSum(byte[] bootBlock)
        {
            bootBlock[4] = 0;
            bootBlock[5] = 0;
            bootBlock[6] = 0;
            bootBlock[7] = 0;


            uint checksum = 0;
            uint precsum = 0;

            for (int i = 0; i < 0x100; i++)   // 0x100 = 1024 byte bootblock / 4
            {
                precsum = checksum;
                if ((checksum += (uint)(((bootBlock[i * 4]) << 24) | ((bootBlock[(i * 4) + 1]) << 16) | ((bootBlock[(i * 4) + 2]) << 8) | bootBlock[(i * 4) + 3])) < precsum)
                    ++checksum;
            }
            checksum = ~checksum;

            var bytes = BitConverter.GetBytes(checksum);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            bootBlock[4] = bytes[0];
            bootBlock[5] = bytes[1];
            bootBlock[6] = bytes[2];
            bootBlock[7] = bytes[3];

        }

        static byte[] MakeDisk(List<DiskItem> diskItems, byte[] data, string path)
        {
            var fileTableSize = diskItems.Count * 4 * 4;
            var offset = 0x400 + fileTableSize;

            using (var writer = new BigEndianWriter(new MemoryStream()))
            {
                //var bootBlock = new byte[0x400];
                var bootblockFile = Path.Combine(path, "bootblock");
                if (!File.Exists(bootblockFile))
                {
                    throw new Exception("missing bootblock file");
                }
                var bootBlock = File.ReadAllBytes(Path.Combine(path, "bootblock"));
                if (bootBlock.Length != 0x400)
                {
                    throw new Exception("bootblock incorrect size");
                }

                BootBlockCheckSum(bootBlock);
                writer.Write(bootBlock);    
                foreach (var diskItem in diskItems)
                {
                    writer.WriteAscii(diskItem.FileID, 4);
                    writer.WriteInt32(diskItem.DiskLocation + offset);
                    var packedSize = diskItem.PackedSize;
                    if ((packedSize & 1) == 1)
                    {
                        packedSize++;
                    }

                    if (diskItem.Cacheable)
                    {
                        packedSize |= 1 << 24;
                    }

                    packedSize |= (int)diskItem.PackingMethod << 28;

                    writer.WriteInt32(packedSize);
                    writer.WriteInt32(diskItem.FileSize);
                }

                writer.Write(data);

                var spaceNeeded = 0xdc000 - writer.Position;
                if (spaceNeeded < 0)
                {
                    throw new Exception($"disk is {-spaceNeeded} bytes over budget!");
                }
                var spacer = new byte[spaceNeeded];
                writer.Write(spacer);
                Console.WriteLine($"FYI: you have {spaceNeeded} bytes remaining on this disk");
                return writer.ToArray();
            }
        }

        static byte[] MergeData(List<DiskItem> diskItems)
        {
            var pos = 0;
            using (var writer = new BigEndianWriter(new MemoryStream()))
            {
                foreach (var diskItem in diskItems)
                { 
                    diskItem.DiskLocation = pos;
                    writer.Write(diskItem.PackedData);
                    pos += diskItem.PackedSize;
                }
                return writer.ToArray();
            }
        }

        static void LoadDiskItem(DiskItem diskItem, string sourcePath, bool repack)
        {
            var sourceFile = Path.Combine(sourcePath, diskItem.Filename);
            var data = File.ReadAllBytes(sourceFile);
            /*
            var checksum = GetChecksum(data, diskItem.PackingMethod);
            if (!repack)
            {
                if (diskItem.Checksum == checksum)
                {
                    Console.WriteLine($"Skipping - {diskItem.Filename}");
                    return;
                }
            }

            diskItem.Checksum = checksum;
            */
            var filetag = $"{diskItem.FileID} - [{diskItem.Filename}]";
            diskItem.Data = null;
            Console.WriteLine($"Packing {diskItem.PackingMethod} - {filetag}");
            diskItem.FileSize = data.Length;

            switch (diskItem.PackingMethod)
            {
                case PackingMethod.Shrinkler:
                    data = ShrinkPack(data);
                    break;
                case PackingMethod.ZX0:
                    data = ZXPack(data);
                    break;
                case PackingMethod.Deflate:
                    data = DeflatePack(data);
                    break;
                case PackingMethod.None:
                    break;
                case PackingMethod.Trim:
                    data = Trim(data);
                    break;
                default:
                    throw new Exception($"Invalid packing method for {filetag}");
            }
            diskItem.PackedData = data;
            diskItem.PackedSize = data.Length;
            Console.WriteLine($"Finished - {filetag}");
        }

        static byte[] Trim(byte[] data)
        {
            var tail = 0;
            var pos = data.Length - 1;
            while (pos > 0)
            {
                if (data[pos] != 0x00)
                {
                    break;
                }
                tail++;
                pos--;
            }

            return data.Take(data.Length - tail).ToArray();
        }

        static string GetChecksum(byte[] data, PackingMethod packMethod)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = new MemoryStream(data))
                {
                    var temp = md5.ComputeHash(stream);
                    var md5String = "";
                    var packByte = (byte)packMethod;
                    foreach (var checkByte in temp)
                    {
                        md5String += $"{packByte:X2}{checkByte:X2}";
                    }

                    return md5String;
                }
            }
        }

        public static void ZXPack(string filename)
        {
            var p = new Process();
            p.StartInfo.FileName = "zx0.exe";
            p.StartInfo.Arguments = $"\"{filename}\" \"{filename}.zx0\"";
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;

            p.Start();

            while (true)
            {
                if (p.WaitForExit(1000))
                {
                    break;
                }
            }
        }


        public static void ApultaPack(string filename)
        {
            var p = new Process();
            p.StartInfo.FileName = "apultra.exe";
            p.StartInfo.Arguments = $"\"{filename}\" \"{filename}.ap\"";
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;

            p.Start();

            while (true)
            {
                if (p.WaitForExit(1000))
                {
                    break;
                }
            }
        }

        public static byte[] ZXPack(byte[] data)
        {
            var infile = Path.GetTempFileName();
            var outfile = Path.GetTempFileName();

            if (File.Exists(infile))
            {
                File.Delete(infile);
            }

            if (File.Exists(outfile))
            {
                File.Delete(outfile);
            }

            File.WriteAllBytes(infile, data);

            var p = new Process();
            //p.StartInfo.FileName = "zx0.exe";
            p.StartInfo.FileName = "salvador.exe";

            p.StartInfo.Arguments = $"\"{infile}\" \"{outfile}\"";
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;

            p.Start();

            while (true)
            {
                if (p.WaitForExit(1000))
                {
                    break;
                }
            }

            var packed = File.ReadAllBytes(outfile);

            if (File.Exists(infile))
            {
                File.Delete(infile);
            }

            if (File.Exists(outfile))
            {
                File.Delete(outfile);
            }

            return packed;
        }

        public static byte[] DeflatePack(byte[] data)
        {
            var infile = Path.GetTempFileName();
            var outfile = infile + ".deflate";

            if (File.Exists(infile))
            {
                File.Delete(infile);
            }

            if (File.Exists(outfile))
            {
                File.Delete(outfile);
            }

            File.WriteAllBytes(infile, data);

            var p = new Process();
            p.StartInfo.FileName = "zopfli.exe";
            p.StartInfo.Arguments = $"--deflate \"{infile}\"";
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;

            p.Start();

            while (true)
            {
                if (p.WaitForExit(1000))
                {
                    break;
                }
            }

            var packed = File.ReadAllBytes(outfile);

            if (File.Exists(infile))
            {
                File.Delete(infile);
            }

            if (File.Exists(outfile))
            {
                File.Delete(outfile);
            }

            return packed;
        }


        public static byte[] ShrinkPack(byte[] data)
        {
            var infile = Path.GetTempFileName();
            var outfile = Path.GetTempFileName();

            if (File.Exists(infile))
            {
                File.Delete(infile);
            }

            if (File.Exists(outfile))
            {
                File.Delete(outfile);
            }

            File.WriteAllBytes(infile, data);

            var p = new Process();
            p.StartInfo.FileName = "shrinkler.exe";
            p.StartInfo.Arguments = $"-d \"{infile}\" \"{outfile}\"";
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;

            p.Start();

            p.WaitForExit(1000 * 100);

            var packed = File.ReadAllBytes(outfile);

            if (File.Exists(infile))
            {
                File.Delete(infile);
            }

            if (File.Exists(outfile))
            {
                File.Delete(outfile);
            }

            return packed;
        }
    }


    public class BigEndianWriter : BinaryWriter
    {
        public BigEndianWriter(Stream stream) : base(stream) { }

        public void WriteAscii(string text, int length)
        {
            var output = new byte[length];
            var convertedText = Encoding.ASCII.GetBytes(text);
            for (var i = 0; i < length; i++)
            {
                if (i < convertedText.Length)
                    output[i] = convertedText[i];
                else
                    output[i] = 0;
            }

            this.Write(output);
        }

        public void WriteInt16(int value)
        {
            var data = BitConverter.GetBytes(value);
            Array.Reverse(data);
            Write(data[2]);
            Write(data[3]);
        }

        public void WriteInt32(int value)
        {
            var data = BitConverter.GetBytes(value);
            Array.Reverse(data);
            Write(data);
        }


        public byte[] ToArray()
        {
            return ((MemoryStream)BaseStream).ToArray();
        }

        public int Position
        {
            get { return (int)BaseStream.Position; }
        }
    }

    public class BigEndianReader : BinaryReader
    {
        public BigEndianReader(Stream stream) : base(stream) { }

        public override int ReadInt32()
        {
            var data = base.ReadBytes(4);
            Array.Reverse(data);
            return BitConverter.ToInt32(data, 0);
        }

        public override Int16 ReadInt16()
        {
            var data = base.ReadBytes(2);
            Array.Reverse(data);
            return BitConverter.ToInt16(data, 0);
        }

        public override UInt16 ReadUInt16()
        {
            var data = base.ReadBytes(2);
            Array.Reverse(data);
            return BitConverter.ToUInt16(data, 0);
        }

        public override Int64 ReadInt64()
        {
            var data = base.ReadBytes(8);
            Array.Reverse(data);
            return BitConverter.ToInt64(data, 0);
        }

        public override UInt32 ReadUInt32()
        {
            var data = base.ReadBytes(4);
            Array.Reverse(data);
            return BitConverter.ToUInt32(data, 0);
        }

        public string ReadAscii(int length)
        {
            var data = base.ReadBytes(length);
            return Encoding.ASCII.GetString(data);
        }

        public bool EndOfStream()
        {
            if (BaseStream.Position == BaseStream.Length)
            {
                return true;
            }
            return false;
        }
    }
}

