using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Spectra
{
    public partial class DynamicImagingWindow_Win32 : Form
    {
        public Bitmap bmp;
        public BitmapData test_data=new BitmapData();
        byte[] buffer_R = new byte[2048 * 3];
        byte[] buffer_G = new byte[2048 * 3];
        byte[] buffer_B = new byte[2048 * 3];
        byte[] buffer_img = new byte[2048 * 1000* 3];
        
        public DynamicImagingWindow_Win32()
        {
            InitializeComponent();
            bmp = new Bitmap(2048,1000,System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            test_data = bmp.LockBits(new Rectangle(0, 0, 2048, 1000), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            pictureBox1.Image = new Bitmap(2048,1000,6144,PixelFormat.Format24bppRgb,test_data.Scan0);
        }


        public void StopTimer()
        {
            this.timer1.Stop();
        }


        private void timer1_Tick(object sender, EventArgs e)
        {
            Marshal.Copy(buffer_img, 0, test_data.Scan0, 2048 * 1000 * 3);
            pictureBox1.Image = new Bitmap(2048, 1000, 6144, PixelFormat.Format24bppRgb, test_data.Scan0);
        }

        private void DynamicImagingWindow_Win32_Shown(object sender, EventArgs e)
        {
            timer1.Start();
        }

        internal void Update(byte[] buf_Dynamic, ushort frameCount, byte chanel)
        {
            if (frameCount % 1000 == 0)
            {
                Parallel.For(0, 1000, r => {
                    Parallel.For(0, 512, c => {
                        buffer_img[r * 2048 * 3+ (chanel - 1) * 3 * 512 + c * 3] = buffer_img[r * 2048 * 3+ (chanel - 1) * 3 * 512 + c * 3 + 1] = buffer_img[r * 2048 * 3+ (chanel - 1) * 3 * 512 + c * 3 + 2] = 0;
                    });
                });
            }
            Parallel.For(0, 512, i =>
            {
                //buffer_img[(frameCount - 34152) * 2048 * 3 + (chanel - 1) * 3 * 512 + i * 3 + 2] = buffer_img[(frameCount - 34152) * 2048 * 3 + (chanel - 1) * 3 * 512 + i * 3 + 1] = buffer_img[(frameCount - 34152) * 2048 * 3 + (chanel - 1) * 3 * 512 + i * 3 + 0] = (byte)(Math.Floor(((double)DataProc.readU16_PIC(buf_Dynamic, i * 2) / 4096 * 256)));
                buffer_img[(frameCount%1000) * 2048 * 3 + (chanel - 1) * 3 * 512 + i * 3 + 2] = buffer_img[(frameCount%1000) * 2048 * 3 + (chanel - 1) * 3 * 512 + i * 3 + 1] = buffer_img[(frameCount%1000) * 2048 * 3 + (chanel - 1) * 3 * 512 + i * 3 + 0] = (byte)(Math.Floor(((double)DataProc.readU16_PIC(buf_Dynamic, i * 2) / 4096 * 256)));

            });
            
        }
    }
}
