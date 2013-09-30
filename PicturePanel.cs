﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageColorMatch
{
    public class PicturePanel : Control
    {
        private volatile Image image;
        private volatile Image currentImage;
        private object imageLock;
        private object paintLock;

        public PicturePanel()
        {
            imageLock = new object();
            paintLock = new object();
            this.DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            currentImage = image;

            base.OnPaint(e);
            if (currentImage != null)
            {
                Graphics g = e.Graphics;
                g.DrawImage(currentImage, 0, 0);
            }
        }

        public Image RenderingImage
        {
            get { return currentImage; }
        }

        public Image Image
        {
            get
            {
                return image;
            }
            set
            {
                image = value;
                Invalidate();
            }
        }

        public Point ToImageCoordinate(Point controlCoordinate)
        {
            var pictureBox = this;
            Image image = pictureBox.Image;
            // test to make sure our image is not null
            if (image == null) return controlCoordinate;
            // Make sure our control width and height are not 0 and our 
            // image width and height are not 0
            if (pictureBox.Width == 0 || pictureBox.Height == 0 || image.Width == 0 || image.Height == 0) return controlCoordinate;
            // This is the one that gets a little tricky. Essentially, need to check 
            // the aspect ratio of the image to the aspect ratio of the control
            // to determine how it is being rendered
            float imageAspect = (float)image.Width / image.Height;
            float controlAspect = (float)pictureBox.Width / pictureBox.Height;
            float newX = controlCoordinate.X;
            float newY = controlCoordinate.Y;
            if (imageAspect > controlAspect)
            {
                // This means that we are limited by width, 
                // meaning the image fills up the entire control from left to right
                float ratioWidth = (float)image.Width / pictureBox.Width;
                newX *= ratioWidth;
                float scale = (float)pictureBox.Width / image.Width;
                float displayHeight = scale * image.Height;
                float diffHeight = pictureBox.Height - displayHeight;
                diffHeight /= 2;
                newY -= diffHeight;
                newY /= scale;
            }
            else
            {
                // This means that we are limited by height, 
                // meaning the image fills up the entire control from top to bottom
                float ratioHeight = (float)image.Height / pictureBox.Height;
                newY *= ratioHeight;
                float scale = (float)pictureBox.Height / image.Height;
                float displayWidth = scale * image.Width;
                float diffWidth = pictureBox.Width - displayWidth;
                diffWidth /= 2;
                newX -= diffWidth;
                newX /= scale;
            }
            return new Point((int)newX, (int)newY);
        }

        public Point ToControlCoordinate(Point imageCoordinate)
        {
            var pictureBox = this;
            Image image = pictureBox.Image;
            // test to make sure our image is not null
            if (image == null) return imageCoordinate;
            // Make sure our control width and height are not 0 and our 
            // image width and height are not 0
            if (pictureBox.Width == 0 || pictureBox.Height == 0 || image.Width == 0 || image.Height == 0) return imageCoordinate;
            // This is the one that gets a little tricky. Essentially, need to check 
            // the aspect ratio of the image to the aspect ratio of the control
            // to determine how it is being rendered
            float imageAspect = (float)image.Width / image.Height;
            float controlAspect = (float)pictureBox.Width / pictureBox.Height;
            float newX = imageCoordinate.X;
            float newY = imageCoordinate.Y;
            if (imageAspect > controlAspect)
            {
                // This means that we are limited by width, 
                // meaning the image fills up the entire control from left to right
                float ratioWidth = (float)pictureBox.Width / image.Width;
                newX *= ratioWidth;
                float scale = (float)image.Width / pictureBox.Width;
                float displayHeight = scale * pictureBox.Height;
                float diffHeight = image.Height - displayHeight;
                diffHeight /= 2;
                newY -= diffHeight;
                newY /= scale;
            }
            else
            {
                // This means that we are limited by height, 
                // meaning the image fills up the entire control from top to bottom
                float ratioHeight = (float)pictureBox.Height / image.Height;
                newY *= ratioHeight;
                float scale = (float)image.Height / pictureBox.Height;
                float displayWidth = scale * pictureBox.Width;
                float diffWidth = image.Width - displayWidth;
                diffWidth /= 2;
                newX -= diffWidth;
                newX /= scale;
            }
            return new Point((int)newX, (int)newY);
        }
    }
}