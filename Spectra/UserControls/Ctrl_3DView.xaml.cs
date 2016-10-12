using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WFTools3D;

namespace Spectra
{
    /// <summary>
    /// Ctrl_3DView.xaml 的交互逻辑
    /// </summary>
    public partial class Ctrl_3DView : UserControl
    {
        GeometryModel3D gm3d_Top;
        GeometryModel3D gm3d_Bottom;
        GeometryModel3D gm3d_Up;
        GeometryModel3D gm3d_Down;
        GeometryModel3D gm3d_Right;
        GeometryModel3D gm3d_Left;
        GeometryModel3D gm3d_Active;

        double imheight = 0;
        public Ctrl_3DView(double lines,System.Drawing.Bitmap[] bmpArray)
        {
            InitializeComponent();
            imheight = lines;
            RenderBox(lines, bmpArray);
            InitializeCameras();
        }

        public Ctrl_3DView()
        {
            InitializeComponent();
            gm3d_Top = Resources["Top"] as GeometryModel3D;
            gm3d_Bottom = Resources["Bottom"] as GeometryModel3D;
            gm3d_Up = Resources["Up"] as GeometryModel3D;
            gm3d_Down = Resources["Down"] as GeometryModel3D;
            gm3d_Left = Resources["Left"] as GeometryModel3D;
            gm3d_Right = Resources["Right"] as GeometryModel3D;
            gm3d_Active = Resources["Active"] as GeometryModel3D;
            ModelVisual3D mv3d = new ModelVisual3D();
            Model3DGroup m3dg = new Model3DGroup();
            RegisterName("Top", gm3d_Top);
            RegisterName("Bottom", gm3d_Bottom);
            RegisterName("Up", gm3d_Up);
            RegisterName("Down", gm3d_Down);
            RegisterName("Right", gm3d_Right);
            RegisterName("Left", gm3d_Left);
            m3dg.Children.Add(gm3d_Top);
            m3dg.Children.Add(gm3d_Bottom);
            m3dg.Children.Add(gm3d_Up);
            m3dg.Children.Add(gm3d_Down);
            m3dg.Children.Add(gm3d_Left);
            m3dg.Children.Add(gm3d_Right);
            m3dg.Children.Add(gm3d_Active);
            mv3d.Content = m3dg;
            scene.Viewport.Children.Add(mv3d);
        }

        private void RenderBox(double lines, Bitmap[] bmpArray)
        {
            try
            {
                GeometryModel3D gm3d_Top = FindName("Top") as GeometryModel3D;
                GeometryModel3D gm3d_Bottom = FindName("Bottom") as GeometryModel3D;
                GeometryModel3D gm3d_Up = FindName("Up") as GeometryModel3D;
                GeometryModel3D gm3d_Down = FindName("Down") as GeometryModel3D;
                GeometryModel3D gm3d_Right = FindName("Right") as GeometryModel3D;
                GeometryModel3D gm3d_Left = FindName("Left") as GeometryModel3D;

                scene.Viewport.Children[2].Transform = new ScaleTransform3D(1, imheight / 600, 1);

                BitmapImage[] bmpSource = new BitmapImage[6];
                for (int i = 0; i < 6; i++)
                {
                    if (File.Exists($"{Environment.CurrentDirectory}\\cube_{i}.bmp")) ;
                    File.Delete($"{Environment.CurrentDirectory}\\cube_{i}.bmp");
                    bmpArray[i].Save($"cube_{i}.bmp");
                    byte[] buffer = File.ReadAllBytes($"{Environment.CurrentDirectory}\\cube_{i}.bmp");
                    MemoryStream ms = new MemoryStream(buffer);
                    bmpSource[i] = new BitmapImage();
                    bmpSource[i].BeginInit();
                    bmpSource[i].StreamSource = ms;
                    bmpSource[i].EndInit();
                };

                gm3d_Top.Material = new DiffuseMaterial(new ImageBrush(bmpSource[0]));
                gm3d_Bottom.Material = new DiffuseMaterial(new ImageBrush(bmpSource[1]));
                gm3d_Up.Material = new DiffuseMaterial(new ImageBrush(bmpSource[2]));
                gm3d_Down.Material = new DiffuseMaterial(new ImageBrush(bmpSource[3]));
                gm3d_Right.Material = new DiffuseMaterial(new ImageBrush(bmpSource[4]));
                gm3d_Left.Material = new DiffuseMaterial(new ImageBrush(bmpSource[5]));

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        internal async void Refresh(string path)
        {
            if (ImageInfo.dtImgInfo == null) return;
            imheight = ImageInfo.dtImgInfo.Rows.Count;
            if (imheight < 1) return;
            this.Busy.isBusy = true;
            Bitmap[] bmp = await DataProc.GetBmp3D(path);
            if (bmp[0] == null || bmp[1] == null || bmp[2] == null || bmp[3] == null || bmp[4] == null || bmp[5] == null)
                return;
            bmp[0].RotateFlip(RotateFlipType.Rotate180FlipX);
            bmp[1].RotateFlip(RotateFlipType.Rotate180FlipY);
            bmp[1].RotateFlip(RotateFlipType.Rotate180FlipX);
            bmp[2].RotateFlip(RotateFlipType.Rotate180FlipX);
            bmp[3].RotateFlip(RotateFlipType.Rotate180FlipX);
            bmp[4].RotateFlip(RotateFlipType.Rotate180FlipY);
            bmp[4].RotateFlip(RotateFlipType.Rotate180FlipX);
            bmp[5].RotateFlip(RotateFlipType.Rotate180FlipY);
            bmp[5].RotateFlip(RotateFlipType.Rotate180FlipX);
            RenderBox(imheight, bmp);
            InitializeCameras();
            this.Busy.isBusy=false;
        }

        void InitializeCameras()
        {
            double scalar = 0.1;
            scene.Camera.Position = new Point3D(scalar*2048, scalar * imheight, scalar * 2048);
            scene.Camera.LookDirection = new Vector3D(-2048, -imheight , -2048);
            scene.Camera.UpDirection = Math3D.UnitY;
            scene.Camera.FieldOfView = 60;
            scene.Camera.Speed = 0;
        }

        private void scene_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            { 
                int X = (int)(scene.touchPoint.X * 10) +1024;
                int Y = (int)(scene.touchPoint.Y * 10+imheight / 2) ;
                int Z = (int)(scene.touchPoint.Z * 10 / 4+80);
                this.tb_3DCoord.Text = $"({X},{Y},{Z})";
                this.Col.Content = $"{X}";
                this.Band.Content = $"{Z}";
                if((bool)cbOriShow.IsChecked)
                    showSingleFrm((UInt16)Y);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        public BitmapImage showSingleFrm(UInt16 frm)
        {
            this.Row.Content = $"{frm}";
            int height = ImageInfo.dtImgInfo.Rows.Count;
            if (frm < height)
            {
                //string strFile = $"{ImageInfo.channelFilesPath}{(Convert.ToUInt32(ImageInfo.dtImgInfo.Rows[frm][2])).ToString("D10")}_{(Convert.ToUInt32(ImageInfo.dtImgInfo.Rows[frm][17])).ToString("D10")}_";
                byte[] frmImage = new byte[2048 * 160 * 3];
                byte[] grayImage = new byte[2048 * 160 * 2];

                for (int b = 0; b < 160; b++)
                {
                    byte[] buf = new byte[4096];
                    FileStream inFile = new FileStream($"{ImageInfo.strFilesPath}{height/4096}\\{b}.raw",FileMode.Open,FileAccess.Read,FileShare.Read);
                    inFile.Seek(frm % 4096 * 4096, SeekOrigin.Begin);
                    inFile.Read(buf, 0, 4096);
                    inFile.Close();
                    Array.Copy(buf, 0, grayImage, b * 4096, 4096);
                }

                //for (int c = 0; c < 2048; c++)
                //    for (int r = 0; r < 160; r++)
                Parallel.For(0, 2048, c =>
                {
                    Parallel.For(0, 160, r =>
                    {
                        frmImage[r * 2048 * 3 + c * 3] = frmImage[r * 2048 * 3 + c * 3 + 1]
                            = frmImage[r * 2048 * 3 + c * 3 + 2] = (byte)(grayImage[r * 2048 * 2 + c * 2] / 16 + grayImage[r * 2048 * 2 + c * 2 + 1] * 16);
                    });
                });
                int x = frmImage.Max();
                for (int c = 0; c < 2048 * 160 * 3; c++)
                {
                    frmImage[c] = (byte)(frmImage[c] * (double)256 / x);
                }
                Bitmap bmp = new Bitmap(2048, 160, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                BitmapData bmpData = bmp.LockBits(new System.Drawing.Rectangle(0, 0, 2048, 160), ImageLockMode.WriteOnly, bmp.PixelFormat);
                Marshal.Copy(frmImage, 0, bmpData.Scan0, 2048 * 160 * 3);
                bmp.UnlockBits(bmpData);
                //bmp.Save("D:\\1.bmp");

                MemoryStream ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                BitmapImage bmpSource = new BitmapImage();
                bmpSource.BeginInit();
                bmpSource.StreamSource = ms;
                bmpSource.EndInit();
                ImageSingleFrm.Source = bmpSource;
                return bmpSource;
            }
            ImageSingleFrm.Source = null;
            return null;
        }

        private bool IsValid(ref int x, ref int y, ref int z)
        {
            if (x < 0 || x > 2048) return false;
            if (y < 0 || y > imheight) return false;
            if (z < 0 || z > 160) return false;
            if (x==2048) x = 2047;
            if (y == imheight) y = (int)imheight-1;
            if (z == 160) z = 159;
            return true;
        }

        private void scene_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (scene.touchPoint.X != 0 || scene.touchPoint.Y != 0 || scene.touchPoint.Z != 0)
                {
                    int X = (int)(scene.touchPoint.X * 10) + 1024;
                    int Y = (int)(scene.touchPoint.Y * 10 + imheight / 2);
                    UInt16 Z = (UInt16)(scene.touchPoint.Z * 10 / 4 + 80);
                    pickSingleImage(Z);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        public async void pickSingleImage(UInt16 Z)
        {
            UInt16[] band = { Z, Z, Z };
            if ((Ctrl_ImageView)((MultiFuncWindow)App.global_Windows[0]).UserControls[0] != null)
                ((Ctrl_ImageView)((MultiFuncWindow)App.global_Windows[0]).UserControls[0]).RefreshPseudoColor(0, ImageInfo.strFilesPath, 4, band, ColorRenderMode.Grayscale);

            if (Z < 1 || Z > 160) return;
            //Bitmap bmp = await DataProc.GetBmp(ImageInfo.strFilesPath, Z - 1, ColorRenderMode.Grayscale);
            Bitmap bmp = await BmpOper.MakePseudoColor(ImageInfo.strFilesPath, band, 4,ImageInfo.imgWidth);
            if (bmp == null) return;
            bmp.RotateFlip(RotateFlipType.Rotate180FlipNone);
            bmp.RotateFlip(RotateFlipType.Rotate180FlipY);
            BitmapImage bmpSource = new BitmapImage();

            if (!File.Exists($"bmpFiles\\{Z}.bmp"))
                bmp.Save($"bmpFiles\\{Z}.bmp");
            bmpSource = new BitmapImage(new Uri($"{Environment.CurrentDirectory}\\bmpFiles\\{Z}.bmp"));

            gm3d_Active.Material = new DiffuseMaterial(new System.Windows.Media.ImageBrush(bmpSource));
            DoubleAnimation day = new DoubleAnimation { From = 0, To = imheight / 10 / (imheight / 600), Duration = new Duration(TimeSpan.FromSeconds(1)) };
            //TranslateTransform3D transtrans3d = new TranslateTransform3D(0, 0, scene.touchPoint.Z);
            TranslateTransform3D transtrans3d = new TranslateTransform3D(0, 0, ((Z-80)*0.4));
            gm3d_Active.Transform = transtrans3d;
            transtrans3d.BeginAnimation(TranslateTransform3D.OffsetYProperty, day);
            curBand.Content = Z.ToString();
        }
    }
}
