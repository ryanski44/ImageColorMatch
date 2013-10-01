﻿using System;
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
        private RawBitmapData displayImage;
        private object displayImageLock;
        private SelectMode mode;
        private List<SelectedArea> areas;
        private int[] customColors;

        private Queue<Tuple<WaitCallback, object>> jobs;
        private object jobsLock;

        private Thread backgroundThread;

        private List<Thread> workerThreads;

        public MainForm()
        {
            InitializeComponent();
            displayImageLock = new object();
            imageLock = new object();
            areas = new List<SelectedArea>();
            backgroundThread = new Thread(new ThreadStart(Background));
            backgroundThread.IsBackground = true;
            backgroundThread.Start();

            jobs = new Queue<Tuple<WaitCallback, object>>();
            jobsLock = new object();

            workerThreads = new List<Thread>();

            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                Thread workerThread = new Thread(new ThreadStart(Worker));
                workerThread.IsBackground = true;
                workerThread.Start();
                workerThreads.Add(workerThread);
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        public void Worker()
        {
            while (true)
            {
                Tuple<WaitCallback, object> job = new Tuple<WaitCallback, object>(null, null);
                lock (jobsLock)
                {
                    if (jobs.Count > 0)
                    {
                        job = jobs.Dequeue();
                    }
                }
                if (job.Item1 != null)
                {
                    job.Item1.Invoke(job.Item2);
                }
                else
                {
                    Thread.Sleep(500);
                }
            }
        }

        public void Background()
        {
            while (true)
            {
                lock (displayImageLock)
                {
                    if (displayImage != null && displayImage != pictureBox.Image)
                    {
                        pictureBox.Image = displayImage;
                    }
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
                        displayImage = new RawBitmapData(image);
                    }

                    if (oldImage != null)
                    {
                        oldImage.Dispose();
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
            if (pictureBox.Image != null)
            {
                g.DrawString(pictureBox.Image.GetHashCode().ToString("X"), SystemFonts.StatusFont, Brushes.Black, 0, 0);
            }
            g.DrawString(numberOfTasks.ToString(), SystemFonts.StatusFont, Brushes.Black, 0, 24);
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


            for (float t11 = 0.98f; t11 < 1.02f; t11 += 0.01f)
            {
                for (float t22 = 0.98f; t22 < 1.02f; t22 += 0.01f)
                {
                    for (float t33 = 0.98f; t33 < 1.02f; t33 += 0.01f)
                    {
                        for (float t12 = -0.02f; t12 < 0.02f; t12 += 0.01f)
                        {
                            for (float t13 = -0.02f; t13 < 0.02f; t13 += 0.01f)
                            {
                                for (float t21 = -0.02f; t21 < 0.02f; t21 += 0.01f)
                                {
                                    for (float t23 = -0.02f; t23 < 0.02f; t23 += 0.01f)
                                    {
                                        for (float t31 = -0.02f; t31 < 0.02f; t31 += 0.01f)
                                        {
                                            for (float t32 = -0.02f; t32 < 0.02f; t32 += 0.01f)
                                            {
                        //float t12 = (1f - t11) / 4;
                        //float t13 = (1f - t11) / 4;
                        //float t21 = (1f - t22) / 4;
                        //float t23 = (1f - t22) / 4;
                        //float t31 = (1f - t33) / 4;
                        //float t32 = (1f - t33) / 4;
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

                                                Tuple<RawBitmapData, DenseMatrix> stateTuple = new Tuple<RawBitmapData, DenseMatrix>(sourceData, matrix);

                                                ExecuteAsync(new WaitCallback(RunTransformation), stateTuple);
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

        public void RunTransformation(object state)
        {
            Tuple<RawBitmapData, DenseMatrix> stateTuple = (Tuple<RawBitmapData, DenseMatrix>)state;
            RawBitmapData sourceData = stateTuple.Item1;
            DenseMatrix matrix = stateTuple.Item2;

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

            //Bitmap transformed = output.GetBitmap();

            //lock (tempImagesLock)
            //{
            //    tempImages.Add(transformed);
            //}
            lock (displayImageLock)
            {
                displayImage = output;
                //Invoke((MethodInvoker)delegate
                //{
                //    pictureBox.Image = transformed;
                //});
            }
        }

        public static int numberOfTasks = 0;
        public static ManualResetEvent waitHandle = new ManualResetEvent(false);
        public static object initLock = new object();

        public void ExecuteAsync(WaitCallback wc, object state)
        {
            Tuple<WaitCallback, object> stateTuple = new Tuple<WaitCallback, object>(wc, state);
            Tuple<WaitCallback, object> wrapperStateTuple = new Tuple<WaitCallback, object>(new WaitCallback(ExecuteAndDecrement), stateTuple);
            Interlocked.Increment(ref numberOfTasks);
            lock (jobsLock)
            {
                jobs.Enqueue(wrapperStateTuple);
            }
        }

        public void ExecuteAndDecrement(object state)
        {
            Tuple<WaitCallback, object> stateTuple = (Tuple<WaitCallback, object>)state;
            WaitCallback wc = stateTuple.Item1;
            try
            {
                wc.Invoke(stateTuple.Item2);
            }
            finally
            {
                if (Interlocked.Decrement(ref numberOfTasks) == 0)
                {
                    waitHandle.Set();
                }
            }
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
