using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spectra
{
    class BmpOper
    {
        [DllImport("DLL\\DataOperation.dll", EntryPoint = "GetRGBFromBand")]
        public static extern void GetRGBFromBand(int band, out double R, out double G, out double B);

        /// <summary>
        /// 通过三个谱段确定图像内容
        /// </summary>
        /// <param name="path">文件存放位置</param>
        /// <param name="band">3个谱段值，相同则为灰度图</param>
        /// <param name="low">亮度等级1-8(4为原始图像)</param>
        /// <returns></returns>
        public static Task<Bitmap> MakePseudoColor(string path,UInt16[] band, UInt16 gain)
        {
            return Task.Run(() => {
                if (band[0] > 160 || band[1] > 160 || band[2] > 160 || gain > 8)
                    return null;
                if (!File.Exists($"{path}{band[0]}.raw") || !File.Exists($"{path}{band[1]}.raw") || !File.Exists($"{path}{band[2]}.raw"))
                    return null;
                if (ImageInfo.dtImgInfo == null)
                    return null;
                int height = ImageInfo.dtImgInfo.Rows.Count, width = 2048;
                if (height < 1)
                    return null;

                byte[] bufBmp = new byte[width * height * 3];
                byte[][] bufBand = new byte[3][];
                for (int i = 0; i < 3; i++)
                    bufBand[i] = new byte[width * height * 2];
                
                FileStream[] fBmp = new FileStream[3];
                if (band[0] == band[1] && band[1] == band[2])
                {
                    fBmp[0]  = new FileStream($"{path}{band[0]}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
                    fBmp[0].Read(bufBand[0], 0, width * height * 2);
                    bufBand[1] = bufBand[2] = bufBand[0];
                    fBmp[0].Close();
                }
                else
                    for (int i = 0; i < 3; i++)
                    {
                        fBmp[i] = new FileStream($"{path}{band[i]}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
                        fBmp[i].Read(bufBand[i], 0, width * height * 2);
                        fBmp[i].Close();
                    }

                double max = 0;
                int maxIndex =0;
                for (int i = 0; i < 3; i++)
                {
                    if (DataProc.readU16_PIC(bufBand[i], height * width) > max)
                    {
                        max = DataProc.readU16_PIC(bufBand[i], height * width);
                        maxIndex = i;
                    }
                }
                double[] ratioBand = new double[3];
                for (int i = 0; i < 3; i++)
                {
                    ratioBand[i] += (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width / 4) / DataProc.readU16_PIC(bufBand[i], height * width / 4);
                    ratioBand[i] += (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 3 / 4) / DataProc.readU16_PIC(bufBand[i], height * width * 3 / 4);
                    ratioBand[i] += (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width / 2) / DataProc.readU16_PIC(bufBand[i], height * width / 2);
                    ratioBand[i] += (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width) / DataProc.readU16_PIC(bufBand[i], height * width);
                    ratioBand[i] += (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 5 / 4) / DataProc.readU16_PIC(bufBand[i], height * width * 5 / 4);
                    ratioBand[i] += (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 2 / 3) / DataProc.readU16_PIC(bufBand[i], height * width * 2 / 3);
                    ratioBand[i] += (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 7 / 4) / DataProc.readU16_PIC(bufBand[i], height * width * 7 / 4);
                    ratioBand[i] = ratioBand[i] / 7;
                }

                Parallel.For(0, width * height, i => {
                    for(int j=0;j<3; j++)
                        bufBmp[i * 3 + 2 - j] = (byte)Math.Min(((int)(DataProc.readU16_PIC(bufBand[j], i * 2) * ratioBand[j]) * 4 * gain >> 6), 255);
                });

                Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bmp.PixelFormat);
                Marshal.Copy(bufBmp, 0, bmpData.Scan0, width * height * 3);
                bmp.UnlockBits(bmpData);
                return bmp;
            });
        }
    }
}
