using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace DirectionalFilter
{

    public static class Helper
    {
        static public int GetComponentsNumber(System.Drawing.Imaging.PixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
                    return 1;

                case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                    return 3;

                case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                    return 4;

                default:
                    Debug.Assert(false);
                    return 0;
            }
        }
    }


    public static class MyExtensions
    {
        public static int Clamp0_255(this int value)
        {
            return (value < 0) ? 0 : (value > 255) ? 255 : value;
        }
    }

    public class BitmapInfo
    {
        public int Width;
        public int Height;
        public int Stride;
        public int Components;

        public BitmapInfo(BitmapData bitmapData)
        {
            Width = bitmapData.Width;
            Height = bitmapData.Height;
            Stride = Math.Abs(bitmapData.Stride);
            Components = Helper.GetComponentsNumber(bitmapData.PixelFormat);
        }
    }

    public class PreciseColor
    {
        public int A;
        public int R;
        public int G;
        public int B;

        public PreciseColor(int a, int r, int g, int b)
        {
            A = a;
            R = r;
            G = g;
            B = b;
        }

        public static PreciseColor FromArgb(int a, int r, int g, int b)
        {
            return new PreciseColor(a, r, g, b);
        }

        /// <summary>
        /// Covert to 32bpp Color
        /// </summary>
        /// <returns></returns>
        public Color ToColor32()
        {
            return Color.FromArgb(
                Convert.ToByte(A.Clamp0_255()),
                Convert.ToByte(R.Clamp0_255()),
                Convert.ToByte(G.Clamp0_255()),
                Convert.ToByte(B.Clamp0_255()));
        }

        public static PreciseColor operator +(PreciseColor c1, PreciseColor c2)
        {
            return PreciseColor.FromArgb(c1.A + c2.A, c1.R + c2.R, c1.G + c2.G, c1.B + c2.B);
        }

        public static PreciseColor operator -(PreciseColor c1, PreciseColor c2)
        {
            return PreciseColor.FromArgb(c1.A - c2.A, c1.R - c2.R, c1.G - c2.G, c1.B - c2.B);
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        String ImageSourceFileName;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            String infoMsg = Window_OnDrop_Sub(e);
            if (infoMsg!=null)
            {
                MessageBox.Show(infoMsg);
            }
        }

        private String Window_OnDrop_Sub(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return "Not a file!";

            String[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 1)
                return "Too many files!";

            ImageSourceFileName = files[0];

            if (!File.Exists(ImageSourceFileName))
                return "Not a file!";

            FileStream fs = null;
            try
            {
                fs = File.Open(ImageSourceFileName, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                if (fs != null)
                    fs.Close();
                return "File already in use!";
            }


            Bitmap bitmapSource = null;
            try
            {
                bitmapSource = new Bitmap(fs);
            }
            catch (System.Exception /*ex*/)
            {
                bitmapSource.Dispose();
                return "Not an image!";
            }

            ImageSource.Source =
                Imaging.CreateBitmapSourceFromHBitmap(
                    bitmapSource.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            int bitmapWidth = bitmapSource.Width;
            int bitmapHeight = bitmapSource.Height;
            Rectangle rect = Rectangle.FromLTRB(0, 0, bitmapWidth, bitmapHeight);

            BitmapData bitmapDataSource = bitmapSource.LockBits(rect,
                ImageLockMode.WriteOnly, bitmapSource.PixelFormat);

            BitmapInfo bitmapInfo = new BitmapInfo(bitmapDataSource);

            int bitmapStride_abs = Math.Abs(bitmapDataSource.Stride);
            int bitmapComponents = Helper.GetComponentsNumber(bitmapDataSource.PixelFormat);
            int dataBytesSize = bitmapStride_abs * bitmapHeight;

            byte[] rgbaValuesBufferSource = new byte[dataBytesSize];
            Marshal.Copy(bitmapDataSource.Scan0, rgbaValuesBufferSource, 0, dataBytesSize);



            // we do not use this matrix, but here it is
            int[,] coefficients = new int[,]
            {
                { 1, 0, -1 },
                { 1, 0, -1 },
                { 1, 0, -1 }
            };

            Bitmap bitmapBlurred = new Bitmap(bitmapWidth, bitmapHeight);
            BitmapData bitmapDataBlurred = bitmapBlurred.LockBits(rect,
                ImageLockMode.WriteOnly, bitmapSource.PixelFormat);
            byte[] rgbaValuesBufferBlurred = new byte[dataBytesSize];
            for (int y = 0; y < bitmapHeight; y++)
            {
                for (int x = 0; x < bitmapWidth; x++)
                {
                    PreciseColor colorLeft1 = GetPixelColorFromArray(rgbaValuesBufferSource, x - 1, y - 1, bitmapInfo);
                    PreciseColor colorLeft2 = GetPixelColorFromArray(rgbaValuesBufferSource, x - 1, y + 0, bitmapInfo);
                    PreciseColor colorLeft3 = GetPixelColorFromArray(rgbaValuesBufferSource, x - 1, y + 1, bitmapInfo);

                    PreciseColor colorRight1 = GetPixelColorFromArray(rgbaValuesBufferSource, x + 1, y - 1, bitmapInfo);
                    PreciseColor colorRight2 = GetPixelColorFromArray(rgbaValuesBufferSource, x + 1, y + 0, bitmapInfo);
                    PreciseColor colorRight3 = GetPixelColorFromArray(rgbaValuesBufferSource, x + 1, y + 1, bitmapInfo);

                    PreciseColor color = colorLeft1 + colorLeft2 + colorLeft3 - colorRight1 - colorRight2 - colorRight3;

                    SetPixelColorInArray(rgbaValuesBufferBlurred, x, y, color.ToColor32(), bitmapInfo);
                }
            }
            Marshal.Copy(rgbaValuesBufferBlurred, 0, bitmapDataBlurred.Scan0, dataBytesSize);
            bitmapBlurred.UnlockBits(bitmapDataBlurred);

            ImageBlurred.Source =
                Imaging.CreateBitmapSourceFromHBitmap(
                    bitmapBlurred.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());


            return null;
        }





 

        static private PreciseColor GetPixelColorFromArray(
            byte[] pixelsArray, int x, int y, BitmapInfo bitmapInfo)
        {
            if (x < 0 || y < 0 || x >= bitmapInfo.Width || y >= bitmapInfo.Height)
                return PreciseColor.FromArgb(0,0,0,0);

            int indexDithered = (bitmapInfo.Stride * y) + (bitmapInfo.Components * x);
            byte A = (bitmapInfo.Components == 4) ? pixelsArray[indexDithered + 3] : (byte)255;
            byte R = pixelsArray[indexDithered + 2];
            byte G = pixelsArray[indexDithered + 1];
            byte B = pixelsArray[indexDithered + 0];

            return PreciseColor.FromArgb(A, R, G, B);
        }

        static private void SetPixelColorInArray(
            byte[] pixelsArray, int x, int y, System.Drawing.Color color, BitmapInfo bitmapInfo)
        {
            if (x < 0 || y < 0 || x >= bitmapInfo.Width || y >= bitmapInfo.Height)
                return;

            int indexDithered = (bitmapInfo.Stride * y) + (bitmapInfo.Components * x);
            pixelsArray[indexDithered + 0] = color.B;  // B
            pixelsArray[indexDithered + 1] = color.G;  // G
            pixelsArray[indexDithered + 2] = color.R;  // R
            if (bitmapInfo.Components == 4)
                pixelsArray[indexDithered + 3] = color.A;  // A
        }




    }
}
