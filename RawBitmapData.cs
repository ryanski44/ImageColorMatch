using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageColorMatch
{
    public unsafe class RawBitmapData
    {
        private int[] imageData;
        private int width;
        private int height;

        public RawBitmapData(int width, int height)
        {
            this.width = width;
            this.height = height;
            imageData = new int[height * width];
        }

        public RawBitmapData(Bitmap b)
        {
            Bitmap toDispose = null;
            try
            {
                if (b.PixelFormat != PixelFormat.Format32bppArgb)
                {
                    Bitmap input = b;
                    b = new Bitmap(input.Width, input.Height, PixelFormat.Format32bppArgb);
                    b.SetResolution(input.HorizontalResolution, input.VerticalResolution);
                    toDispose = b;
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        g.DrawImage(input, 0, 0);
                    }
                }

                width = b.Width;
                height = b.Height;

                BitmapData lockData = b.LockBits(
                    new Rectangle(0, 0, width, height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppArgb);

                // Create an array to store image data
                imageData = new Int32[width * height];

                // Use the Marshal class to copy image data
                System.Runtime.InteropServices.Marshal.Copy(
                    lockData.Scan0, imageData, 0, imageData.Length);

                b.UnlockBits(lockData);
            }
            finally
            {
                if (toDispose != null)
                {
                    toDispose.Dispose();
                }
            }
        }

        public int Width { get { return width; } }
        public int Height { get { return height; } }

        public Color GetPixel(int x, int y)
        {
            int pixelValue = imageData[y * width + x];
            return Color.FromArgb(pixelValue);
        }

        public bool TryGetPixel(int x, int y, out Color color)
        {
            if (x >= width || y >= height || x < 0 || y < 0)
            {
                color = Color.Black;
                return false;
            }
            int pixelValue = imageData[y * width + x];
            color = Color.FromArgb(pixelValue);
            return true;
        }

        public bool TryGetRawARGBValue(int x, int y, out int argbValue)
        {
            if (x >= width || y >= height || x < 0 || y < 0)
            {
                argbValue = 0;
                return false;
            }
            argbValue = imageData[y * width + x];
            return true;
        }

        public int GetRawARGBValue(int x, int y)
        {
            return imageData[y * width + x];
        }

        public void SetRawARGBValue(int x, int y, int value)
        {
            imageData[y * width + x] = value;
        }

        public Bitmap GetBitmap()
        {
            Bitmap b = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            BitmapData lockData = b.LockBits(
                    new Rectangle(0, 0, width, height),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppArgb);

            // Use the Marshal class to copy image data
            System.Runtime.InteropServices.Marshal.Copy(
                imageData, 0, lockData.Scan0, imageData.Length);

            b.UnlockBits(lockData);

            return b;
        }
    }
}
