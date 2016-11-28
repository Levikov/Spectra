using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Spectra
{
    /// <summary>
    /// Ctrl_ImageView.xaml 的交互逻辑
    /// </summary>
    public partial class Ctrl_ImageView : UserControl
    {
        public string Title;
        public int SubWinIndex;
        public enum DisplayContent { Single,Pick,Sync};

        public int ScreenIndex;
        public int ImgW,ImgH;
        public ColorRenderMode colorMode = ColorRenderMode.Grayscale;
        public UInt16[] colorBand = { 40, 40, 40 };
        public UInt16[] colorWave = { 698, 698, 698};

        public System.Windows.Point MousePoint;     //鼠标在窗体的位置
        public double realRatio;                    //图像显示的真实比例（scale为1时）
        public double realX, realY;                 //IMG1在窗体的真实位置
        public double curXinImg, curYinImg;         //当前鼠标值IMG1的位置
        public Coord coo = new Coord(0,0);          //点位置的经纬

        public Ctrl_ImageView()
        {
            InitializeComponent();
        }
        
        public async void RefreshPseudoColor(int sub, string path,UInt16 gain,UInt16[] band, ColorRenderMode cMode)
        {
            this.Busy.isBusy = true;

            SubWinIndex = sub;
            colorBand = band;
            colorMode = cMode;
            for (int i = 0; i < 3; i++)
                colorWave[i] = (UInt16)ImageInfo.getWave(colorBand[i]);

            band[0] -= 1; band[1] -= 1; band[2] -= 1;
            Bitmap bmp = await BmpOper.MakePseudoColor2(sub, path, band, gain);
            band[0] += 1; band[1] += 1; band[2] += 1;
            if (bmp == null) return;
            bmp.RotateFlip(RotateFlipType.Rotate90FlipX);
            MemoryStream ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            ImgW = bmp.Width;
            ImgH = bmp.Height;

            BitmapImage bmpSource = new BitmapImage();
            bmpSource.BeginInit();
            bmpSource.StreamSource = ms;
            bmpSource.EndInit();
            this.IMG1.Source = bmpSource;
            viewSet();

            this.Busy.isBusy = false;
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
            if (ImageSection.beginSection)
            {
                borderSection.Visibility = Visibility.Visible;
                ImageSection.startFrm = (int)curXinImg;
                Thickness mov = new Thickness();
                mov = borderSection.Margin;
                mov.Left = MousePoint.X;
                borderSection.Margin = mov;
            }
            mouseDown = false;
        }

        //鼠标滑过IMG控件
        private void IMG1_MouseMove(object sender, MouseEventArgs e)
        {
            try
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
                    coo = new Coord(Convert.ToDouble(ImageInfo.dtImgInfo.Rows[(int)curXinImg][7]), Convert.ToDouble(ImageInfo.dtImgInfo.Rows[(int)curXinImg][6]));
                    viewSet();
                }
            }
            catch
            { }
        }

        //界面显示内容
        public void viewSet()
        {
            ImageWidth.Text = ImgW.ToString();
            Col.Text = $"{Math.Floor(curXinImg)}";
            Row.Text = $"{Math.Floor(curYinImg)}";
            Lat.Text = coo.Lat.ToString("F4");
            Lon.Text = coo.Lon.ToString("F4");
            if (colorMode == ColorRenderMode.Grayscale)
            {
                //tb_3DCoord.Text = $"({Math.Floor(curXinImg)},{Math.Floor(curYinImg)},{SpecNum})";
                Band.Text = $"{colorBand[0]}";
                Wave.Text = $"{colorWave[0]}";

                lblgrayValue.Visibility = Visibility.Visible;
                lblMinValue.Visibility = Visibility.Visible;
                lblMaxValue.Visibility = Visibility.Visible;
                lblMeanValue.Visibility = Visibility.Visible;
                grayValue.Visibility = Visibility.Visible;
                MinValue.Visibility = Visibility.Visible;
                MaxValue.Visibility = Visibility.Visible;
                MeanValue.Visibility = Visibility.Visible;

                if (App.global_ImageBuffer[SubWinIndex] != null)
                {
                    grayValue.Text = App.global_ImageBuffer[SubWinIndex].getValue((int)curXinImg, (int)curYinImg).ToString();
                    MinValue.Text = App.global_ImageBuffer[SubWinIndex].min.ToString();
                    MaxValue.Text = App.global_ImageBuffer[SubWinIndex].max.ToString();
                    MeanValue.Text = App.global_ImageBuffer[SubWinIndex].mean.ToString();
                }
            }
            else
            {
                Band.Text = $"{colorBand[0]},{colorBand[1]},{colorBand[2]}";
                Wave.Text = $"{colorWave[0]},{colorWave[1]},{colorWave[2]}";

                lblgrayValue.Visibility = Visibility.Collapsed;
                lblMinValue.Visibility = Visibility.Collapsed;
                lblMaxValue.Visibility = Visibility.Collapsed;
                lblMeanValue.Visibility = Visibility.Collapsed;
                grayValue.Visibility = Visibility.Collapsed;
                MinValue.Visibility = Visibility.Collapsed;
                MaxValue.Visibility = Visibility.Collapsed;
                MeanValue.Visibility = Visibility.Collapsed;
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

                transform1.X = -1 * (curXinImg * transform.ScaleX * realRatio - MousePoint.X + realX - 95);  //这里加了偏置5,不知道为什么计算的不准
                transform1.Y = -1 * (curYinImg * transform.ScaleY * realRatio - MousePoint.Y + realY); //这里加了偏置68,只能通过加偏置处理
            }
        }

        //鼠标滑过窗体，刷新像素位置
        private void UserControl_MouseMove(object sender, MouseEventArgs e)
        {
            MousePoint = e.GetPosition(e.Source as FrameworkElement);

            realRatio = IMG1.ActualHeight / 2048;

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

        //右键点击显示曲线
        private void IMG1_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                System.Windows.Point p = new System.Windows.Point((int)curXinImg, (int)curYinImg);
                getParams(p);
                if (((MultiFuncWindow)App.global_Windows[5]).UserControls[0] != null)
                {
                    if (ImageInfo.chartMode)
                    {
                        if (ImageInfo.chartShowCnt % ImageInfo.chartShowSum == 0)
                            ((MultiFuncWindow)App.global_Windows[5]).Refresh(null, 0, WinFunc.Curve);
                        ((Ctrl_SpecCurv)((MultiFuncWindow)App.global_Windows[5]).UserControls[0]).Draw1(p, coo, ImageInfo.chartShowCnt % ImageInfo.chartShowSum);
                    }
                    else
                        ((Ctrl_SpecCurv)((MultiFuncWindow)App.global_Windows[5]).UserControls[ImageInfo.chartShowCnt % 4]).Draw4(p, coo);
                }
                if (++ImageInfo.chartShowCnt == 25200)
                    ImageInfo.chartShowCnt = 0;
                if (ImageSection.beginSection)
                {
                    borderSection.Visibility = Visibility.Visible;
                    ImageSection.endFrm = (int)curXinImg;
                    Thickness mov = new Thickness();
                    mov = borderSection.Margin;
                    mov.Right = ActualWidth - MousePoint.X + 1;
                    borderSection.Margin = mov;
                }
            }
            catch
            { }
        }

        private bool isFirst = true;    //防止第一次界面打开时刷新图像
        private void sldLow_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isFirst)
                isFirst = false;
            else
                RefreshPseudoColor(SubWinIndex, ImageInfo.strFilesPath, (UInt16)sldLow.Value,colorBand,colorMode);
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

        private void getParams(System.Windows.Point p)
        {
            DataRow dr = ImageInfo.dtImgInfo.Rows[(int)p.X];

            int frm = Convert.ToInt32(dr["FrameId"]);
            double freq = Convert.ToDouble(dr["Freq"]);
            int integral = Convert.ToInt32(dr["Integral"]);
            int startRow = Convert.ToInt32(dr["StartRow"]);
            int gain = Convert.ToInt32(dr["Gain"]);

            barFrameId.Content = frm.ToString();
            barFreq.Content = freq.ToString("F2");
            barIntegral.Content = integral.ToString();
            barStartRow.Content = startRow.ToString();
            barGain.Content = gain.ToString();
            
            App.global_FrmImgWindow.imgShow(frm, freq, integral, startRow, gain);
        }
    }

}
