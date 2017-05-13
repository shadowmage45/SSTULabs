using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace SSTUUtilDDS
{
    public class Program
    {
        private static Device device;

        public static void Main(string[] args)
        {
            Console.Title = "SSTU - KSP DDS Image Converter";
            //System.Console.WriteLine("Resize Images?");
            //String val = System.Console.ReadLine();
            //val = val.ToLower().Trim();
            //if (val.Equals("true") || val.Equals("yes") || val.Equals("y"))
            //{
            //    System.Console.WriteLine("Resize Denominator: ");
            //    String denom = Console.ReadLine();
            //    System.Console.WriteLine("Resizing images to: 1/" + denom);                
            //}
            Form control = new TestForm();
            device = new Device(0, DeviceType.Hardware, control, CreateFlags.HardwareVertexProcessing, new PresentParameters { Windowed = true, SwapEffect = SwapEffect.Discard });
            convertFolderTextures();
            System.Console.WriteLine("Conversion Finished, press <any key> to continue.");
            System.Console.ReadKey();
        }

        public static void convertFolderTextures()
        {
            string basePath = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
            string folderPath = basePath + Path.DirectorySeparatorChar + "img";
            if (!Directory.Exists(folderPath))
            {
                System.Console.WriteLine("Creating image processing folder: "+folderPath);
                Directory.CreateDirectory(folderPath);
            }
            String[] fileNames = Directory.GetFiles(folderPath);
            int len = fileNames.Length;
            for (int i = 0; i < len; i++)
            {
                if (fileNames[i].ToLower().EndsWith(".png"))
                {
                    convertToDDS(fileNames[i]);
                }
            }
        }

        public static void convertToDDS(String fileName)
        {
            int outFormat = checkFormat(fileName);
            Format format = Format.Dxt1;

            Image img = Image.FromFile(fileName);
            img.RotateFlip(RotateFlipType.RotateNoneFlipY);
            Bitmap bmp = new Bitmap(img);
            int width = bmp.Width;
            int height = bmp.Height;
            if (outFormat == 1) { format = Format.Dxt1; }
            else if (outFormat == 5 || outFormat==6) { format = Format.Dxt5; }
            if (outFormat == 6)
            {
                swizzle(bmp);
            }
            Stream st = new MemoryStream();
            bmp.Save(st, ImageFormat.Png);
            bmp.Dispose();
            st.Position = 0;

            //if (outFormat == 6)
            //{
            //    st = swizzleTga(st, width, height);
            //}

            ImageInformation info = TextureLoader.ImageInformationFromFile(fileName);
            Texture tex = TextureLoader.FromStream(device, st, info.Width, info.Height, 0, Usage.None, format, Pool.SystemMemory, Filter.Triangle | Filter.DitherDiffusion, Filter.Triangle | Filter.DitherDiffusion, 0);
            string outputName = fileName.Substring(0, fileName.LastIndexOf('.')) + ".dds";
            TextureLoader.Save(outputName, ImageFileFormat.Dds, tex);
            st.Close();
            File.Delete(fileName);
            if (outFormat == 6)
            {
                markNormalMap(outputName);
            }
        }

        private static void markNormalMap(string ddsFilePath)
        {
            //I could write a proper class to read DDS header and set all arguments properly, but this works just as well, and it's quite fast
            var fs = File.Open(ddsFilePath, FileMode.Open, FileAccess.ReadWrite);
            fs.Position = 0x53;
            var b = (byte)fs.ReadByte();
            fs.Position = 0x53;
            fs.WriteByte((byte)(b | 0x80));
            fs.Close();
            System.Console.WriteLine("Marking as normalMap: " + ddsFilePath);
        }

        private static GraphicsStream swizzleTga(Stream st, int width, int height)
        {
            System.Console.WriteLine("Swizzling normal map");
            st.Position = 0;
            Texture tex = TextureLoader.FromStream(device, st);
            GraphicsStream gs = TextureLoader.SaveToStream(ImageFileFormat.Tga, tex);
            
            var bOri = new byte[gs.Length];
            //byte array from original stream
            var bSwizzled = new byte[gs.Length];
            //swizzled array
            gs.Read(bOri, 0, (int)gs.Length);
            const int headerSize = 0x1f;
            //a header for a tga file is usually 18bytes long, but for a reason I don't get, here it start at 0x1F...
            // Dim headerSize As Integer = 18
            for (var i = 0; i <= headerSize - 1; i++)
            {
                bSwizzled[i] = bOri[i];
            }
            //there's probably a better way to do that, but this one is self-explanatory...
            for (var y = 0; y <= height - 1; y++)
            {
                for (var x = 0; x <= width - 1; x++)
                {
                    var pos = (((y * width) + x) * 4) + headerSize;
                    //b = bOri(pos)
                    var g = bOri[pos + 1];
                    var r = bOri[pos + 2];
                    //a = bOri(pos + 3)
                    bSwizzled[pos] = 255;
                    bSwizzled[pos + 1] = g;
                    bSwizzled[pos + 2] = 255;
                    bSwizzled[pos + 3] = r;
                }
            }
            gs.Position = 0;
            gs.Write(bSwizzled, 0, bSwizzled.Length);
            gs.Position = 0;
            st.Dispose();
            st.Close();            
            System.Console.WriteLine("Swizzle done");
            return gs;
        }

        private static void swizzle(Bitmap bmp)
        {
            byte r, g, b;
            byte oa, or, og, ob;
            Color c;
            for (int x = 0; x < bmp.Width; x++)
            {
                for (int y = 0; y < bmp.Height; y++)
                {
                    c = bmp.GetPixel(x, y);
                    r = c.R;
                    g = c.G;
                    b = c.B;
                    oa = r;
                    or = 255;
                    og = g;
                    ob = 255;
                    c = Color.FromArgb(oa, or, og, ob);
                    bmp.SetPixel(x, y, c);
                }
            }
        }

        private static void SwizzleImage(GraphicsStream gs, int width, int height, bool b32BPP)
        {
        }

        private static void flipImage(Bitmap bmp)
        {
            bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
        }

        private static int checkFormat(string fileName)
        {            
            fileName = fileName.Substring(fileName.LastIndexOf("\\")+1);
            string fileNameShort = fileName.Substring(0, fileName.LastIndexOf("."));
            if (fileNameShort.ToLower().EndsWith("-nrm"))
            {
                Console.WriteLine("Detected normal map from -NRM file suffix, auto-setting format to DXT5NM-xGxR swizzled.");
                return 6;
            }
            Console.Write("Format (dxt1/1, dxt5/5, dxt5nrm/nrm/6) for "+fileName+":  ");
            string val = Console.ReadLine().ToLower().Trim();
            if (val.Equals("1") || val.Equals("dxt1"))
            {
                return 1;
            }
            else if (val.Equals("5") || val.Equals("dxt5"))
            {
                return 5;
            }
            else if (val.Equals("6") || val.Equals("nrm") || val.Equals("dxt5nrm") || val.Equals("dxt5nm"))
            {
                return 6;
            }
            return 1;
        }

        /// <summary>
        /// TODO -- this method should examine the bitmap image to see if it has an alpha channel (dxt5 vs dxt1),
        /// TODO -- this method should examine the texture name to see if it is a normal map (ends with -NRM)
        /// </summary>
        /// <param name="bmp"></param>
        /// <returns></returns>
        private static int checkFormat(Bitmap bmp)
        {
            return 1;
        }

    }

    /// <summary>
    /// Dummy form/control for the Device handle to use.  No clue why this is necessary at all for a CONSOLE application.<para/>
    /// Doesn't appear that the control needs to do anything... it just can't be NULL
    /// </summary>
    public class TestForm : System.Windows.Forms.Form
    {

    }
}
