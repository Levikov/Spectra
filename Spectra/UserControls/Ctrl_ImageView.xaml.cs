using System;
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

        public System.Windows.Point MousePoint;     //鼠标在窗体的位置
        public double realRatio;                    //图像显示的真实比例（scale为1时）
        public double realX, realY;                 //IMG1在窗体的真实位置
        public double curXinImg, curYinImg;         //当前鼠标值IMG1的位置

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

        public async void Refresh(int band,ColorRenderMode cMode,string path)
        {
            this.Busy.isBusy = true;
            
            Bitmap bmp = await DataProc.GetBmp(path,160 - band, cMode);
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
            Wave.Text = ImageInfo.getWave(SpecNum).ToString("F0");
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

        //点下鼠标
        private void IMG1_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var img = sender as ContentControl;
            if (img == null)
                return;
            //img.CaptureMouse();   //不知道为什么，这句话会影响鼠标点下之后的坐标信息
            mouseDown = true;
            mouseXY = e.GetPosition(img);
        }

        //松开鼠标
        private void IMG1_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var img = sender as ContentControl;
            if (img == null)
                return;
            img.ReleaseMouseCapture();
            mouseDown = false;
        }

        //鼠标滑过IMG控件
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
                curXinImg = p.X * ImgW;
                curYinImg = p.Y * ImgH;
                tb_3DCoord.Text = $"({Math.Floor(curXinImg)},{Math.Floor(curYinImg)},{SpecNum})";
                Col.Text = $"{Math.Floor(curXinImg)}";
                Row.Text = $"{Math.Floor(curYinImg)}";
            }
        }

        //鼠标移动图像位置
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

        //对IMG使用滚轮
        private void IMG1_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var img = sender as ContentControl;
            if (img == null)
                return;
            var point = e.GetPosition(img);
            var group = IMG1.FindResource("Imageview") as TransformGroup;
            var delta = e.Delta > 0 ? 1 : -1;
            DowheelZoom(group, point, delta);
        }

        //设置图像比例、位置
        private void DowheelZoom(TransformGroup group, System.Windows.Point point, double delta)
        {
            //var pointToContent = group.Inverse.Transform(point);
            var transform = group.Children[0] as ScaleTransform;
            var transform1 = group.Children[1] as TranslateTransform;

            if (transform.ScaleX + delta <= 1)
            {
                transform.ScaleX = 1;
                transform.ScaleY = 1;

                transform1.X = 0;
                transform1.Y = 0;
            }
            else
            {
                transform.ScaleX += delta;
                transform.ScaleY += delta;

                transform1.X = -1 * (curXinImg * transform.ScaleX * realRatio - MousePoint.X + realX + 5);  //这里加了偏置5,不知道为什么计算的不准
                transform1.Y = -1 * (curYinImg * transform.ScaleY * realRatio - MousePoint.Y + realY - 68); //这里加了偏置68,只能通过加偏置处理
            }
        }

        //鼠标滑过窗体，刷新像素位置
        private void UserControl_MouseMove(object sender, MouseEventArgs e)
        {
            MousePoint = e.GetPosition(e.Source as FrameworkElement);

            realRatio = IMG1.ActualWidth / 2048;

            if (IMG1.ActualHeight > IMG1.ActualWidth)
            {
                realX = (ActualWidth - IMG1.ActualWidth * realRatio)/2;
                realY = 0;
            }
            else
            {
                realX = 0;
                realY = (ActualHeight - IMG1.ActualHeight * realRatio) / 2;
            }

            //System.Windows.Point p = Mouse.GetPosition(e.Source as FrameworkElement);
            //tX.Text = $"{Math.Floor(p.X)}";
            //tY.Text = $"{Math.Floor(p.Y)}";
        }

        //双击后放大到16:1像元大小
        private const int DOUBLERATIO = 16;
        private void IMG1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            IMG1.MouseDown -= IMG1_MouseDown;

            if (e.ClickCount == 2)
            {
                var group = IMG1.FindResource("Imageview") as TransformGroup;

                var transform = group.Children[0] as ScaleTransform;
                if (transform.ScaleX != DOUBLERATIO / realRatio)
                {
                    transform.ScaleX = DOUBLERATIO / realRatio;
                    transform.ScaleY = DOUBLERATIO / realRatio;

                    var transform1 = group.Children[1] as TranslateTransform;
                    transform1.X = -1 * (curXinImg * DOUBLERATIO - MousePoint.X + realX);
                    transform1.Y = -1 * (curYinImg * DOUBLERATIO - MousePoint.Y + realY);
                }
                else
                {
                    transform.ScaleX = 1;
                    transform.ScaleY = 1;

                    var transform1 = group.Children[1] as TranslateTransform;
                    transform1.X = 0;
                    transform1.Y = 0;
                }
            }

            IMG1.MouseDown += IMG1_MouseDown;
        }

    }

}
