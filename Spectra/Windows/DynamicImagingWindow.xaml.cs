﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Spectra
{
    /// <summary>
    /// DynamicImagingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class DynamicImagingWindow : Window
    {


        /// <summary>
        /// 图片名称
        /// </summary>
        public int ScreenIndex;
        public Timer t;
        public enum DisplayContent { Single, Pick, Sync };

        TimerCallback tc;
        public int SpecNum;
        public int[] RgbSpec = new int[3];
        public int ImgW;
        public int ImgH;
        public static byte[] img_buffer;
        IProgress<Bitmap> prog;
        public DynamicImagingWindow()
        {
            InitializeComponent();
            img_buffer = new byte[3 * 600 * 2048];
            prog = new Progress<Bitmap>(b => { this.Refresh(b); });
            tc = new TimerCallback(timercall);
            t = new Timer(tc, prog, 10, Timeout.Infinite);
            t.Change(0, 100);
            
        }



        void timercall(object o)
        {
            IProgress<Bitmap> bb = o as IProgress<Bitmap>;
            Bitmap bmp = new Bitmap(2048, 600, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            BitmapData bmpdata = bmp.LockBits(new System.Drawing.Rectangle(0, 0, 2048, 600), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            Marshal.Copy(img_buffer, 0, bmpdata.Scan0, 3 * 2048 * 600);
            bmp.UnlockBits(bmpdata);
            bb.Report(bmp);

        }

        public DynamicImagingWindow(string title, Bitmap bmp)
        {
            InitializeComponent();
            // this.IMG1.Source = bmp;
        }

        public async void Refresh(int band, ColorRenderMode cMode)
        {

            Bitmap bmp = await DataProc.GetBmp(160 - band + 1, cMode);
            MemoryStream ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            ImgW = bmp.Width;
            ImgH = bmp.Height;
            BitmapImage bmpSource = new BitmapImage();
            bmpSource.BeginInit();
            bmpSource.StreamSource = ms;
            bmpSource.EndInit();
            this.IMG1.Source = bmpSource;
            SpecNum = band;


        }

        public void Refresh(Bitmap bmp)
        {

            MemoryStream ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            BitmapImage bmpSource = new BitmapImage();
            bmpSource.BeginInit();
            bmpSource.StreamSource = ms;
            bmpSource.EndInit();
            this.IMG1.Source = bmpSource;


        }

        public DynamicImagingWindow(Bitmap bmp)
        {
            InitializeComponent();
            MemoryStream ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            BitmapImage bmpSource = new BitmapImage();
            bmpSource.BeginInit();
            bmpSource.StreamSource = ms;
            bmpSource.EndInit();
            this.IMG1.Source = bmpSource;

        }
        private bool mouseDown;
        private System.Windows.Point mouseXY;

        private void IMG1_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var img = sender as ContentControl;
            if (img == null)
                return;
            img.CaptureMouse(); 
            mouseDown = true;
            mouseXY = e.GetPosition(img);
        }

        private void IMG1_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var img = sender as ContentControl;
            if (img == null)
                return;
            img.ReleaseMouseCapture();
            mouseDown = false;
        }

        private void IMG1_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var img = sender as ContentControl;
            if (img == null)
                return;
            if (mouseDown)
                Domousemove(img, e);
        }

        internal void Update(byte[] buf_Dynamic, ushort frameCount, byte chanel)
        {
            Parallel.For(0, 512, i =>
            {
                //img_buffer[(frameCount - 34152) * 2048 * 3 + (chanel - 1) * 3 * 512 + i * 3 + 2] = img_buffer[(frameCount - 34152) * 2048 * 3 + (chanel - 1) * 3 * 512 + i * 3 + 1] = img_buffer[(frameCount - 34152) * 2048 * 3 + (chanel - 1) * 3 * 512 + i * 3 + 0] = (byte)(Math.Floor(((double)DataProc.readU16_PIC(buf_Dynamic, i * 2) / 4096 * 256)));
                img_buffer[(frameCount - 34150) * 2048 * 3 + (chanel - 1) * 3 * 512 + i * 3 + 2] = img_buffer[(frameCount - 34150) * 2048 * 3 + (chanel - 1) * 3 * 512 + i * 3 + 1] = img_buffer[(frameCount - 34150) * 2048 * 3 + (chanel - 1) * 3 * 512 + i * 3 + 0] = (byte)(Math.Floor(((double)DataProc.readU16_PIC(buf_Dynamic, i * 2) / 4096 * 256)));
            });
        }

        private void IMG1_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var img = sender as ContentControl;
            if (img == null)
                return;
            var point = e.GetPosition(img);
            var group = IMG1.FindResource("Imageview") as TransformGroup;
            var delta = e.Delta * 0.001;
            DowheelZoom(group, point, delta);
        }

        private void Domousemove(ContentControl img, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;
            var group = IMG1.FindResource("Imageview") as TransformGroup;
            var transform = group.Children[1] as TranslateTransform;
            var position = e.GetPosition(img);
            transform.X -= mouseXY.X - position.X;
            transform.Y -= mouseXY.Y - position.Y;
            mouseXY = position;
        }

        private void DowheelZoom(TransformGroup group, System.Windows.Point point, double delta)
        {
            var pointToContent = group.Inverse.Transform(point);
            var transform = group.Children[0] as ScaleTransform;
            if (transform.ScaleX + delta < 0.1) return;
            transform.ScaleX += delta;
            transform.ScaleY += delta;
            var transform1 = group.Children[1] as TranslateTransform;
            transform1.X = -1 * ((pointToContent.X * transform.ScaleX) - point.X);
            transform1.Y = -1 * ((pointToContent.Y * transform.ScaleY) - point.Y);
        }

        private void IMG1_MouseDown(object sender, MouseButtonEventArgs e)
        {



        }



        /*

        private async void image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {

           
            dtsChart1st = new ObservableDataSource<System.Windows.Point>();
            foreach (System.Windows.Point point in points)
            {
                dtsChart1st.AppendAsync(base.Dispatcher, point);
            }
            chart1st.Children.Remove(lm.LineGraph);
            chart1st.Children.Remove(lm.MarkerGraph);
            initChart();
            this.image.MouseLeftButtonUp += image_MouseLeftButtonUp;
        }

    */
    }
}