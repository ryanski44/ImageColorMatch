using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MathNet.Numerics.LinearAlgebra.Single;

namespace ImageColorMatch
{
    public partial class MainForm : Form
    {
        private Bitmap image;
        private object imageLock;
        private Image displayImage;
        private object displayImageLock;
        private SelectMode mode;
        private List<SelectedArea> areas;
        private int[] customColors;
        private List<Image> tempImages;
        private object tempImagesLock;

        private Thread backgroundThread;

        public MainForm()
        {
            InitializeComponent();
            displayImageLock = new object();
            imageLock = new object();
            areas = new List<SelectedArea>();
            tempImages = new List<Image>();
            tempImagesLock = new object();
            backgroundThread = new Thread(new ThreadStart(Background));
            backgroundThread.IsBackground = true;
            backgroundThread.Start();

        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        public void Background()
        {
            while (true)
            {
                //lock (displayImageLock)
                //{
                //    if (displayImage != null && displayImage != pictureBox.Image)
                //    {
                //        Invoke((MethodInvoker)delegate
                //        {
                //            pictureBox.Image = displayImage;
                //            pictureBox.Invalidate();
                //        });
                //    }
                //}
                lock (tempImagesLock)
                {
                    List<Image> newTempImages = new List<Image>();
                    foreach (Image tempImage in tempImages)
                    {
                        if (tempImage != pictureBox.Image && tempImage != pictureBox.RenderingImage)
                        {
                            tempImage.Dispose();
                        }
                        else
                        {
                            newTempImages.Add(tempImage);
                        }
                    }
                    tempImages = newTempImages;
                }
                Thread.Sleep(500);
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.DefaultExt = "*.jpg";
            if (ofd.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
            {
                if (File.Exists(ofd.FileName))
                {
                    Bitmap oldImage = image;
                    using (Image input = Image.FromFile(ofd.FileName))
                    {
                        image = new Bitmap(input.Width, input.Height, PixelFormat.Format32bppArgb);
                        image.SetResolution(input.HorizontalResolution, input.VerticalResolution);
                        using (Graphics g = Graphics.FromImage(image))
                        {
                            g.PageUnit = GraphicsUnit.Pixel;
                            g.DrawImage(input, 0, 0);
                        }
                    }

                    lock (displayImageLock)
                    {
                        displayImage = image;
                        pictureBox.Image = image;
                    }

                    lock (tempImagesLock)
                    {
                        if (oldImage != null)
                        {
                            tempImages.Add(oldImage);
                        }
                    }
                }
            }
        }

        public SelectMode Mode
        {
            get { return mode; }
            set
            {
                mode = value;
                switch (mode)
                {
                    case SelectMode.Add:
                        pictureBox.Cursor = Cursors.Cross;
                        break;
                    case SelectMode.Delete:
                        pictureBox.Cursor = Cursors.Arrow;
                        break;
                }
            }
        }

        private void tslAddExpectedColor_Click(object sender, EventArgs e)
        {
            Mode = SelectMode.Add;
        }

        private void tslRemoveExpectedColor_Click(object sender, EventArgs e)
        {
            Mode = SelectMode.Delete;
        }

        private bool dragging = false;
        private Point startDrag, currentDrag;

        private void pictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (!dragging && mode == SelectMode.Add)
            {
                dragging = true;
                startDrag = e.Location;
            }
        }

        private void pictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (dragging && mode == SelectMode.Add)
            {
                dragging = false;
                pictureBox.Invalidate();
                SelectedArea area = new SelectedArea();
                Rectangle screenRectangle = FromCorners(startDrag, e.Location);
                Point imageTopLeft = pictureBox.ToImageCoordinate(new Point(screenRectangle.X, screenRectangle.Y));
                Point imageBottomRight = pictureBox.ToImageCoordinate(new Point(screenRectangle.Right, screenRectangle.Bottom));
                area.X = imageTopLeft.X;
                area.Y = imageTopLeft.Y;
                area.Width = imageBottomRight.X - area.X;
                area.Height = imageBottomRight.Y - area.Y;

                ColorDialog cd = new ColorDialog();
                if (customColors != null)
                {
                    cd.CustomColors = customColors;
                }
                if (cd.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                {
                    customColors = cd.CustomColors;
                    area.ExpectedColor = cd.Color;
                    areas.Add(area);
                    pictureBox.Invalidate();
                }
            }
        }

        private void pictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging && mode == SelectMode.Add)
            {
                currentDrag = e.Location;
                pictureBox.Invalidate();
            }
        }

        private void pictureBox_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (dragging && mode == SelectMode.Add)
            {
                g.DrawRectangle(Pens.Black, FromCorners(startDrag, currentDrag));
            }
            foreach (var area in areas)
            {
                Point screenTopLeft = pictureBox.ToControlCoordinate(new Point(area.X, area.Y));
                Point screenBottomRight = pictureBox.ToControlCoordinate(new Point(area.Width + area.X, area.Height + area.Y));
                using (Brush b = new SolidBrush(InvertColor(area.ExpectedColor)))
                {
                    using (Pen p = new Pen(b))
                    {
                        g.DrawRectangle(p, FromCorners(screenTopLeft, screenBottomRight));
                    }
                }
            }
        }

        public Rectangle FromCorners(Point corner1, Point corner2)
        {
            int x = Math.Min(corner1.X, corner2.X);
            int y = Math.Min(corner1.Y, corner2.Y);
            int width = Math.Abs(corner2.X - corner1.X);
            int height = Math.Abs(corner2.Y - corner1.Y);
            return new Rectangle(x, y, width, height);
        }

        public Color InvertColor(Color c)
        {
            return Color.FromArgb(255 - c.R, 255 - c.G, 255 - c.B);
        }

        private void toolStripContainer1_TopToolStripPanel_Click(object sender, EventArgs e)
        {

        }

        private void tsbRun_Click(object sender, EventArgs e)
        {
            numberOfTasks = 1;
            waitHandle = new ManualResetEvent(false);

            RawBitmapData sourceData = new RawBitmapData(image);

            
                for (float t11 = 0.905f; t11 < 1.15f; t11 += 0.05f)
                {
                    for (float t22 = 0.905f; t22 < 1.15f; t22 += 0.05f)
                    {
                        for (float t33 = 0.905f; t33 < 1.15f; t33 += 0.05f)
                        {
                            for (float t12 = -0.105f; t12 < 0.15f; t12 += 0.05f)
                            {
                                for (float t13 = -0.105f; t13 < 0.15f; t13 += 0.05f)
                                {
                                    for (float t21 = -0.105f; t21 < 0.15f; t21 += 0.05f)
                                    {
                                        for (float t23 = -0.105f; t23 < 0.15f; t23 += 0.05f)
                                        {
                                            for (float t31 = -0.105f; t31 < 0.15f; t31 += 0.05f)
                                            {
                                                for (float t32 = -0.105f; t32 < 0.15f; t32 += 0.05f)
                                                {
                                                    ExecuteAsync(new WaitCallback(delegate(object state)
            {
                                                    DenseMatrix matrix = DenseMatrix.Create(3, 3, (row, col) => row == col ? 1 : 0);
                                                    matrix[0, 0] = t11;
                                                    matrix[0, 1] = t12;
                                                    matrix[0, 2] = t13;
                                                    matrix[1, 0] = t21;
                                                    matrix[1, 1] = t22;
                                                    matrix[1, 2] = t23;
                                                    matrix[2, 0] = t31;
                                                    matrix[2, 1] = t32;
                                                    matrix[2, 2] = t33;

                                                    RawBitmapData output = new RawBitmapData(sourceData.Width, sourceData.Height);

                                                    for (int x = 0; x < sourceData.Width; x++)
                                                    {
                                                        for (int y = 0; y < sourceData.Height; y++)
                                                        {
                                                            int rawargbValue = sourceData.GetRawARGBValue(x, y);
                                                            DenseVector vector = new DenseVector(new float[] { rawargbValue >> 16 & 0xFF, rawargbValue >> 8 & 0xFF, rawargbValue & 0xFF });
                                                            DenseVector converted = matrix * vector;
                                                            rawargbValue = 0xFF << 24 | converted[0].ToTrimmedByte() << 16 | converted[1].ToTrimmedByte() << 8 | converted[2].ToTrimmedByte();
                                                            output.SetRawARGBValue(x, y, rawargbValue);
                                                        }
                                                    }

                                                    Bitmap transformed = output.GetBitmap();

                                                    lock (tempImagesLock)
                                                    {
                                                        tempImages.Add(transformed);
                                                    }
                                                    lock (displayImageLock)
                                                    {
                                                        displayImage = transformed;
                                                        Invoke((MethodInvoker)delegate
                                                        {
                                                            pictureBox.Image = transformed;
                                                        });
                                                    }
            }));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static int numberOfTasks = 0;
        public static ManualResetEvent waitHandle = new ManualResetEvent(false);
        public static object initLock = new object();

        public static void ExecuteAsync(WaitCallback wc)
        {
            Interlocked.Increment(ref numberOfTasks);
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object state)
            {
                try
                {
                    wc.Invoke(state);
                }
                finally
                {
                    if (Interlocked.Decrement(ref numberOfTasks) == 0)
                    {
                        waitHandle.Set();
                    }
                }
            }));
        }
    }

    public enum SelectMode
    {
        Add,
        Delete
    }

    public class SelectedArea
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public Color ExpectedColor { get; set; }
    }
}
