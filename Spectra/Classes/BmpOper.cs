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
        public static Task<Bitmap> MakePseudoColor(string path,UInt16[] band, UInt16 gain, int height)
        {
            return Task.Run(() => {
                if (band[0] > 160 || band[1] > 160 || band[2] > 160 || gain > 8)
                    return null;
                int width = 2048;
                if (height < 1)
                    return null;

                int splitHeight = 4096;                                             //图像分割单元
                int splitSum = (int)Math.Ceiling((double)height / splitHeight);     //图像分割总数

                byte[] bufBmp = new byte[width * height * 3];                       //RGB图像缓存
                byte[][] bufBand = new byte[3][];                                   //16bit单谱段图像缓存
                for (int i = 0; i < 3; i++)
                    bufBand[i] = new byte[width * height * 2];

                FileStream[] fBmp = new FileStream[3];
                if (band[0] == band[1] && band[1] == band[2])
                {
                    for (int i = 0; i < splitSum; i++)
                    {
                        if (i != splitSum - 1)
                            splitHeight = 4096;
                        else
                            splitHeight = height % 4096;
                        try
                        {
                            fBmp[0] = new FileStream($"{path}{i}\\{band[0]}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
                            byte[] tBuf = new byte[splitHeight * width * 2];
                            fBmp[0].Read(tBuf, 0, width * splitHeight * 2);
                            fBmp[0].Close();
                            Array.Copy(tBuf, 0, bufBand[0], i * 4096 * 2048 * 2, width * splitHeight * 2);
                        }
                        catch { }
                    }
                    bufBand[1] = bufBand[2] = bufBand[0];
                }
                else
                {
                    for (int b = 0; b < 3; b++)
                    {
                        for (int j = 0; j < splitSum; j++)
                        {
                            if (j != splitSum - 1)
                                splitHeight = 4096;
                            else
                                splitHeight = height % 4096;
                            try
                            {
                                fBmp[b] = new FileStream($"{path}{j}\\{band[b]}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
                                byte[] tBuf = new byte[splitHeight * width * 2];
                                fBmp[b].Read(tBuf, 0, width * splitHeight * 2);
                                fBmp[b].Close();
                                Array.Copy(tBuf, 0, bufBand[b], j * 4096 * 2048 * 2, width * splitHeight * 2);
                            }
                            catch { }
                        }
                    }
                }
                
                double max = 0;
                int maxIndex = 0;
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
                    ratioBand[i] =
                    ((double)DataProc.readU16_PIC(bufBand[maxIndex], height * width / 4) +
                    (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 3 / 4) +
                    (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width / 2) +
                    (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width) +
                    (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 5 / 4) +
                    (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 3 / 2) +
                    (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 7 / 4)) /
                    (DataProc.readU16_PIC(bufBand[i], height * width / 4) +
                    DataProc.readU16_PIC(bufBand[i], height * width * 3 / 4) +
                    DataProc.readU16_PIC(bufBand[i], height * width / 2) +
                    DataProc.readU16_PIC(bufBand[i], height * width) +
                    DataProc.readU16_PIC(bufBand[i], height * width * 5 / 4) +
                    DataProc.readU16_PIC(bufBand[i], height * width * 3 / 2) +
                    DataProc.readU16_PIC(bufBand[i], height * width * 7 / 4)
                    );
                }

                Parallel.For(0, width * height, i => {
                    for (int j = 0; j < 3; j++)
                        bufBmp[i * 3 + 2 - j] = (byte)Math.Min(((int)(DataProc.readU16_PIC(bufBand[j], i * 2) * ratioBand[j]) * 4 * gain >> 6), 255);
                });

                Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bmp.PixelFormat);
                Marshal.Copy(bufBmp, 0, bmpData.Scan0, width * height * 3);
                bmp.UnlockBits(bmpData);
                return bmp;
            });
        }

        /// <summary>
        /// 通过三个谱段（0-159）确定图像内容，这个只针对单个文件进行操作
        /// </summary>
        /// <param name="path">文件存放位置</param>
        /// <param name="band">3个谱段值，相同则为灰度图</param>
        /// <param name="low">亮度等级1-8(4为原始图像)</param>
        /// <returns></returns>
        public static Task<Bitmap> MakePseudoColor2(int sub, string path, UInt16[] band, UInt16 gain)
        {
            return Task.Run(() => {
                if (band[0] > 160 || band[1] > 160 || band[2] > 160 || gain > 8)
                    return null;
                if (ImageInfo.dtImgInfo == null)
                    return null;
                int height = ImageInfo.imgWidth, width = 2048;
                if (height < 1)
                    return null;

                int splitHeight = 4096;                                             //图像分割单元
                int splitSum = (int)Math.Ceiling((double)height / splitHeight);     //图像分割总数

                byte[] bufBmp = new byte[width * height * 3];                       //RGB图像缓存
                byte[][] bufBand = new byte[3][];                                   //16bit单谱段图像缓存
                for (int i = 0; i < 3; i++)
                    bufBand[i] = new byte[width * height * 2];

                FileStream[] fBmp = new FileStream[3];
                if (band[0] == band[1] && band[1] == band[2])
                {
                    for (int i = 0; i < splitSum; i++)
                    {
                        if (i != splitSum - 1)
                            splitHeight = 4096;
                        else
                            splitHeight = height % 4096;
                        try
                        {
                            fBmp[0] = new FileStream($"{path}{i}\\{band[0]}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
                            byte[] tBuf = new byte[splitHeight * width * 2];
                            fBmp[0].Read(tBuf, 0, width * splitHeight * 2);
                            fBmp[0].Close();
                            Array.Copy(tBuf, 0, bufBand[0], i * 4096 * 2048 * 2, width * splitHeight * 2);
                        }
                        catch { }
                    }
                    bufBand[1] = bufBand[2] = bufBand[0];
                }
                else
                {
                    for (int b = 0; b < 3; b++)
                    {
                        for (int j = 0; j < splitSum; j++)
                        {
                            if (j != splitSum - 1)
                                splitHeight = 4096;
                            else
                                splitHeight = height % 4096;
                            try
                            {
                                fBmp[b] = new FileStream($"{path}{j}\\{band[b]}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
                                byte[] tBuf = new byte[splitHeight * width * 2];
                                fBmp[b].Read(tBuf, 0, width * splitHeight * 2);
                                fBmp[b].Close();
                                Array.Copy(tBuf, 0, bufBand[b], j * 4096 * 2048 * 2, width * splitHeight * 2);
                            }
                            catch { }
                        }
                    }
                }
                
                //获得图像的灰度信息
                App.global_ImageBuffer[sub] = new ImageBuffer(ImageInfo.imgWidth, ImageInfo.imgHeight);
                if(sub<2)
                    App.global_ImageBuffer[sub].buffer = bufBand[0];
                else
                    App.global_ImageBuffer[sub].buffer = bufBand[sub-1];
                App.global_ImageBuffer[sub].getInfo();

                double max = 0;
                int maxIndex = 0;
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
                    ratioBand[i] =
                    ((double)DataProc.readU16_PIC(bufBand[maxIndex], height * width / 4) +
                    (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 3 / 4) +
                    (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width / 2) +
                    (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width) +
                    (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 5 / 4) +
                    (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 3 / 2) +
                    (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 7 / 4)) /
                    (DataProc.readU16_PIC(bufBand[i], height * width / 4) +
                    DataProc.readU16_PIC(bufBand[i], height * width * 3 / 4) +
                    DataProc.readU16_PIC(bufBand[i], height * width / 2) +
                    DataProc.readU16_PIC(bufBand[i], height * width) +
                    DataProc.readU16_PIC(bufBand[i], height * width * 5 / 4) +
                    DataProc.readU16_PIC(bufBand[i], height * width * 3 / 2) +
                    DataProc.readU16_PIC(bufBand[i], height * width * 7 / 4)
                    );


                    //ratioBand[i] += (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width / 4) / DataProc.readU16_PIC(bufBand[i], height * width / 4);
                    //ratioBand[i] += (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 3 / 4) / DataProc.readU16_PIC(bufBand[i], height * width * 3 / 4);
                    //ratioBand[i] += (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width / 2) / DataProc.readU16_PIC(bufBand[i], height * width / 2);
                    //ratioBand[i] += (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width) / DataProc.readU16_PIC(bufBand[i], height * width);
                    //ratioBand[i] += (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 5 / 4) / DataProc.readU16_PIC(bufBand[i], height * width * 5 / 4);
                    //ratioBand[i] += (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 2 / 3) / DataProc.readU16_PIC(bufBand[i], height * width * 2 / 3);
                    //ratioBand[i] += (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 7 / 4) / DataProc.readU16_PIC(bufBand[i], height * width * 7 / 4);
                    //ratioBand[i] = ratioBand[i] / 7;
                }

                Parallel.For(0, width * height, i => {
                    for (int j = 0; j < 3; j++)
                        bufBmp[i * 3 + 2 - j] = (byte)Math.Min(((int)(DataProc.readU16_PIC(bufBand[j], i * 2) * ratioBand[j]) * 4 * gain >> 6), 255);
                });

                Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bmp.PixelFormat);
                Marshal.Copy(bufBmp, 0, bmpData.Scan0, width * height * 3);
                bmp.UnlockBits(bmpData);
                return bmp;
            });
        }
        
        /// <summary>
        /// 按片生成伪彩图像
        /// </summary>
        /// <param name="path">文件存放位置</param>
        /// <param name="band">3个谱段值，相同则为灰度图</param>
        /// <param name="height">像高</param>
        /// <returns></returns>
        public static Bitmap MakePseudoColor(string path, UInt16[] band, int height)
        {
            if (band[0] > 160 || band[1] > 160 || band[2] > 160)
                return null;
            int width = 2048;
            if (height < 1)
                return null;
                
            byte[] bufBmp = new byte[width * height * 3];                       //RGB图像缓存
            byte[][] bufBand = new byte[3][];                                   //16bit单谱段图像缓存
            for (int i = 0; i < 3; i++)
                bufBand[i] = new byte[width * height * 2];

            FileStream[] fBmp = new FileStream[3];
            if (band[0] == band[1] && band[1] == band[2])
            {
                try
                {
                    fBmp[0] = new FileStream($"{path}{band[0]}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
                    byte[] tBuf = new byte[height * width * 2];
                    fBmp[0].Read(tBuf, 0, width * height * 2);
                    fBmp[0].Close();
                    Array.Copy(tBuf, 0, bufBand[0], 0, width * height * 2);
                }
                catch { }
                bufBand[1] = bufBand[2] = bufBand[0];
            }
            else
            {
                for (int b = 0; b < 3; b++)
                {
                    try
                    {
                        fBmp[b] = new FileStream($"{path}{band[b]}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
                        byte[] tBuf = new byte[height * width * 2];
                        fBmp[b].Read(tBuf, 0, width * height * 2);
                        fBmp[b].Close();
                        Array.Copy(tBuf, 0, bufBand[b], 0, width * height * 2);
                    }
                    catch { }
                }
            }

            double max = 0;
            int maxIndex = 0;
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
                ratioBand[i] =
                ((double)DataProc.readU16_PIC(bufBand[maxIndex], height * width / 4) +
                (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 3 / 4) +
                (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width / 2) +
                (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width) +
                (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 5 / 4) +
                (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 3 / 2) +
                (double)DataProc.readU16_PIC(bufBand[maxIndex], height * width * 7 / 4)) /
                (DataProc.readU16_PIC(bufBand[i], height * width / 4) +
                DataProc.readU16_PIC(bufBand[i], height * width * 3 / 4) +
                DataProc.readU16_PIC(bufBand[i], height * width / 2) +
                DataProc.readU16_PIC(bufBand[i], height * width) +
                DataProc.readU16_PIC(bufBand[i], height * width * 5 / 4) +
                DataProc.readU16_PIC(bufBand[i], height * width * 3 / 2) +
                DataProc.readU16_PIC(bufBand[i], height * width * 7 / 4)
                );
            }

            int gain = 4;
            Parallel.For(0, width * height, i => {
                for (int j = 0; j < 3; j++)
                    bufBmp[i * 3 + 2 - j] = (byte)Math.Min(((int)(DataProc.readU16_PIC(bufBand[j], i * 2) * ratioBand[j]) * 4 * gain >> 6), 255);
            });

            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bmp.PixelFormat);
            Marshal.Copy(bufBmp, 0, bmpData.Scan0, width * height * 3);
            bmp.UnlockBits(bmpData);
            return bmp;
        }

        /// <summary>
        /// 获取某帧的原始图像
        /// </summary>
        /// <param name="frm">帧号</param>
        /// <param name="height">图像总帧数</param>
        /// <returns>Bitmap图像</returns>
        public static Bitmap MakeFrameImage(int frm,int height)
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

            return bmp;
        }
    }
}
