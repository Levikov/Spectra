using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Spectra
{
    public partial class FrmImgWindow : Window
    {
        private bool isShow = false;

        public FrmImgWindow()
        {
            InitializeComponent();
        }

        /*显示器对象 显示器编号   窗口编号*/
        public void imgShow(int frm,int freq,int integral,int startRow,int gain)
        {
            if (isShow == false)
            {
                this.Show();
                this.isShow = true;
            }
            barFrameId.Content = frm.ToString();
            barFreq.Content = freq.ToString();
            barIntegral.Content = integral.ToString();
            barStartRow.Content = startRow.ToString();
            barGain.Content = gain.ToString();
            showSingleFrm((UInt16)frm);
        }

        public BitmapImage showSingleFrm(UInt16 frm)
        {
            txtSetFrame.Text = frm.ToString();
            int height = ImageInfo.dtImgInfo.Rows.Count;
            if (frm < height)
            {
                byte[] frmImage = new byte[2048 * 160 * 3];
                byte[] grayImage = new byte[2048 * 160 * 2];

                for (int b = 0; b < 160; b++)
                {
                    byte[] buf = new byte[4096];
                    FileStream inFile = new FileStream($"{ImageInfo.strFilesPath}{height / 4096}\\{b}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            isShow = false;
            this.Hide();
            e.Cancel = true;
        }

        private void txtSetFrame_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                showSingleFrm(Convert.ToUInt16(txtSetFrame.Text));
        }
    }
}
