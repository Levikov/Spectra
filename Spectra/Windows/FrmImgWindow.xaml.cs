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
        public FrmImgWindow()
        {
            InitializeComponent();
        }
        
        /// <summary>
        /// 显示帧号对应的原始单帧图像
        /// </summary>
        /// <param name="frm">帧号</param>
        /// <param name="freq">帧频</param>
        /// <param name="integral">积分设置</param>
        /// <param name="startRow">起始行</param>
        /// <param name="gain">增益设置</param>
        public void imgShow(int frm,double freq,int integral,int startRow,int gain)
        {
            Show();
            barFrameId.Content = frm.ToString();
            barFreq.Content = freq.ToString("F2");
            barIntegral.Content = integral.ToString();
            barStartRow.Content = startRow.ToString();
            barGain.Content = gain.ToString();
            showSingleFrm((UInt16)frm);
        }

        /// <summary>
        /// 生成单帧图像并绑定Image控件
        /// </summary>
        /// <param name="frm">帧号</param>
        public void showSingleFrm(UInt16 frm)
        {
            txtSetFrame.Text = frm.ToString();
            int height = ImageInfo.dtImgInfo.Rows.Count;
            frm = Convert.ToUInt16(frm - ImageInfo.minFrm);
            if (frm < height)
            {
                Bitmap bmp = BmpOper.MakeFrameImage(frm, height);

                MemoryStream ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                BitmapImage bmpSource = new BitmapImage();
                bmpSource.BeginInit();
                bmpSource.StreamSource = ms;
                bmpSource.EndInit();
                ImageSingleFrm.Source = bmpSource;
                return;
            }
            ImageSingleFrm.Source = null;
            return;
        }

        /// <summary>
        /// 永不关闭窗体，只隐藏
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
        }

        /// <summary>
        /// Press回车执行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtSetFrame_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                showSingleFrm(Convert.ToUInt16(txtSetFrame.Text));
        }
    }
}
