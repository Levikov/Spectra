﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Spectra
{
    /// <summary>
    /// Ctrl_ImageView.xaml 的交互逻辑
    /// </summary>
    public partial class Ctrl_ImageView : UserControl
    {
        /// <summary>
        /// 图片名称
        /// </summary>
        public string Title;
        public int ScreenIndex;
        public enum DisplayContent { Single,Pick,Sync};
        public int SpecNum;
        public int[] RgbSpec=new int[3];
        public int ImgW;
        public int ImgH;
        
        public Ctrl_ImageView()
        {
            InitializeComponent();
        }
        public Ctrl_ImageView(string title)
        {
            InitializeComponent();
        }

        public Ctrl_ImageView(string title,Bitmap bmp)
        {
            InitializeComponent();
           // this.IMG1.Source = bmp;
        }

        public async void Refresh(int band,ColorRenderMode cMode,string md5)
        {
            this.Busy.isBusy = true;
            
            Bitmap bmp = await DataProc.GetBmp(160 - band, cMode,md5);
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
            Band.Text = $"{SpecNum}";
            this.Busy.isBusy = false;
        }

        public Ctrl_ImageView(Bitmap bmp)
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
            {
                Domousemove(img, e);
            }
            else
            {
                System.Windows.Point p = Mouse.GetPosition(e.Source as FrameworkElement);
                p.X = (p.X / IMG1.ActualWidth);
                p.Y = (p.Y / IMG1.ActualHeight);
                tb_3DCoord.Text = $"({Math.Floor(p.X * ImgW)},{Math.Floor(p.Y * ImgH)},{SpecNum})";
                Col.Text = $"{Math.Floor(p.X * ImgW)}";
                Row.Text = $"{Math.Floor(p.Y * ImgH)}";
                Band.Text = $"{SpecNum}";
            }
        }

        private void IMG1_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var img = sender as ContentControl;
            if (img == null)
                return;
            var point = e.GetPosition(img);
            var group = IMG1.FindResource("Imageview") as TransformGroup;
            var delta = e.Delta * 0.01;
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
            IMG1.MouseDown -= IMG1_MouseDown;
            System.Windows.Point p = Mouse.GetPosition(e.Source as FrameworkElement);
            p.X = (p.X/IMG1.ActualWidth);
            p.Y = (p.Y / IMG1.ActualHeight);
            tb_3DCoord.Text = $"({Math.Floor(p.X*ImgW)},{Math.Floor(p.Y* ImgH)},{SpecNum})";
            Col.Text = $"{Math.Floor(p.X * ImgW)}";
            Row.Text = $"{Math.Floor(p.Y * ImgH)}";
            Band.Text = $"{SpecNum}";

            foreach (MultiFuncWindow w in App.global_Windows)
            {
                foreach (UserControl u in w.UserControls)
                {
                    if (u is Ctrl_SpecCurv)
                        ((Ctrl_SpecCurv)u).Draw(p);
                }
            }
            IMG1.MouseDown += IMG1_MouseDown;
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
