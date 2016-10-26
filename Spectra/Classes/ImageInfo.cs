using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Spectra
{
    public class ImageInfo : DependencyObject
    {
        [DllImport("DLL\\DataOperation.dll", EntryPoint = "Split_Chanel")]
        static extern void Split_Chanel(string path, string outpath, int sum, string[] file);
        [DllImport("DLL\\DataOperation.dll", EntryPoint = "GetCurrentPosition")]
        static extern double GetCurrentPosition();

        public static int noise_value = 0;      //噪声值

        public static bool chartMode;           //曲线模式1或4
        public static int chartShowCnt = 0;     //显示的曲线计数
        public static int chartShowSum = 10;    //显示的曲线总数

        public static DataTable dtBandWave;     //谱段和波长的对应表，该表在系统运行过程中为固定值
        public static DataTable dtImgInfo;      //图像信息对应的DataTable
        public static string[] strFileName;     //图像对应的文件名称
        public static string channelFilesPath = Environment.CurrentDirectory + "\\channelFiles\\";  //通道图像的存储位置
        public static string strFilesPath = Environment.CurrentDirectory + "\\showFiles\\";         //显示图像的存储位置

        //图像检索结果对应的图像信息
        public static int minFrm;
        public static int maxFrm;
        public static double startSec;
        public static double endSec;
        public static DateTime startTime;
        public static DateTime endTime;
        public static Coord startCoord = new Coord(0, 0);
        public static Coord endCoord = new Coord(0, 0);

        //图像的宽和高
        public static int imgWidth;
        public static int imgHeight = 2048;

        public ImageInfo() { }

        /// <summary>
        /// 获取图像信息
        /// </summary>
        public static void GetImgInfo()
        {
            minFrm = Convert.ToInt32(dtImgInfo.Compute("min(FrameId)", ""));
            maxFrm = Convert.ToInt32(dtImgInfo.Compute("max(FrameId)", ""));
            imgWidth = dtImgInfo.Rows.Count;

            startSec = Convert.ToDouble(dtImgInfo.Compute("min(GST)", ""));
            endSec = Convert.ToDouble(dtImgInfo.Compute("max(GST)", ""));
            startCoord.Lat = Convert.ToDouble(dtImgInfo.Compute("min(Lat)", ""));
            startCoord.Lon = Convert.ToDouble(dtImgInfo.Compute("min(Lon)", ""));
            endCoord.Lat = Convert.ToDouble(dtImgInfo.Compute("max(Lat)", ""));
            endCoord.Lon = Convert.ToDouble(dtImgInfo.Compute("max(Lon)", ""));
            DateTime T0 = new DateTime(2012, 1, 1, 0, 0, 0);
            startTime = T0.AddSeconds(startSec);
            endTime = T0.AddSeconds(endSec);
        }

        /// <summary>
        /// 生成图像
        /// </summary>
        /// <param name="Prog">进度条</param>
        /// <returns>进度条的值</returns>
        public static Task<int> MakeImage(IProgress<double> Prog)
        {
            return Task.Run(() => {
                //512拼2048*N图
                Timer t = new Timer((o) =>
                {
                    IProgress<double> a = o as IProgress<double>;
                    a.Report(GetCurrentPosition());
                }, Prog, 0, 10);
                strFileName = new string[imgWidth];
                for (int i = 0; i < imgWidth; i++)
                    strFileName[i] = $"{(Convert.ToUInt32(dtImgInfo.Rows[i][2])).ToString("D10")}_{(Convert.ToUInt32(dtImgInfo.Rows[i][17])).ToString("D10")}_";
                Split_Chanel($"{Environment.CurrentDirectory}\\channelFiles\\", $"{Environment.CurrentDirectory}\\showFiles\\", imgWidth, strFileName);
                return 1;
            });
        }

        /// <summary>
        /// 图像检测异常
        /// </summary>
        /// <param name="v">谱段号</param>
        /// <param name="subWid">片宽</param>
        /// <returns>
        /// -1:表示打开文件出错
        /// </returns>
        public static int ImgDetectAbnormal(int v, int subWid)
        {
            FileStream fTest = null;
            //try
            //{
            //    fTest = new FileStream($"{FileInfo.decFilePathName}{v}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
            //}
            //catch
            //{
            //    return -1;
            //}

            /*分割大小*/
            int subLen = 2048 * subWid * 2;
            int subLast = (int)(imgWidth % (4096 / subWid)) * 2048 * 2;
            int subCnt = (int)Math.Ceiling(ImageInfo.imgWidth / (double)subWid); /*以subWid为单位分割图像*/

            SQLiteConnection conn = new SQLiteConnection(Global.dbConString);
            conn.Open();

            for (int i = 0; i < subCnt - 1; i++)
            {
                if (i % (4096 / subWid) == 0)
                    fTest = new FileStream($"{FileInfo.decFilePathName}{i / (4096 / subWid)}\\{v}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] buf_full = new byte[subLen];
                fTest.Read(buf_full, 0, subLen);
                for (int j = 0; j < subLen;)
                {
                    if (buf_full[j++] + buf_full[j++] * 256 > ImageInfo.noise_value)
                    {
                        SQLiteCommand cmmd = new SQLiteCommand("", conn);
                        cmmd.CommandText = $"insert into Detect_Abnormal (谱段号,片号,片尺寸) values ({v},{i},{subWid})";
                        cmmd.ExecuteNonQuery();
                        break;
                    }
                }
            }

            byte[] buf_last = new byte[subLen];
            fTest.Read(buf_last, 0, subLast);
            for (int j = 0; j < subLast;)
            {
                if (buf_last[j++] + buf_last[j++] * 256 > ImageInfo.noise_value)
                {
                    SQLiteCommand cmmd = new SQLiteCommand("", conn);
                    cmmd.CommandText = $"insert into Detect_Abnormal (谱段号,片号,片尺寸) values ({v},{subCnt},{subWid})";
                    cmmd.ExecuteNonQuery();
                    break;
                }
            }
            fTest.Close();
            conn.Close();

            return 160;
        }

        /// <summary>
        /// 通过波长返回谱段
        /// </summary>
        /// <param name="wave">波长</param>
        /// <returns>谱段</returns>
        public static UInt16 getBand(double wave)
        {
            double min = 2000;
            int index = 0;
            for (int i = 0; i < dtBandWave.Rows.Count; i++)
            {
                if (Math.Abs(wave - Convert.ToDouble(dtBandWave.Rows[i][1])) < min)
                {
                    index = i;
                    min = Math.Abs(wave - Convert.ToDouble(dtBandWave.Rows[i][1]));
                }
            }
            return Convert.ToUInt16(dtBandWave.Rows[index][0]);
        }

        /// <summary>
        /// 通过谱段返回波长
        /// </summary>
        /// <param name="band">谱段</param>
        /// <returns>波长</returns>
        public static double getWave(int band)
        {
            return Convert.ToDouble(dtBandWave.Rows[band - 1][1]);
        }
    }

    public class ImageBuffer
    {
        public int width, height, mean, max, min;
        public byte[] buffer;

        public ImageBuffer(int w, int h)
        {
            width = w;
            height = h;
            buffer = new byte[width * height * 2];
        }

        public void getInfo()
        {
            int[] colSum = new int[width];
            int[] colMax = new int[width];
            int[] colMin = new int[width];
            int[] colMean = new int[width];
            int[] curValue = new int[width];

            //for (int c = 0; c < width; c++)
            Parallel.For(0,width,c=>
            {
                colSum[c] = 0;
                for (int i = 0; i < height; i++)
                {
                    curValue[c] = (int)buffer[i * width * 2 + c * 2] + (int)buffer[i * width * 2 + c * 2 + 1] * 256;
                    colSum[c] += curValue[c];
                    if (i == 0)
                    {
                        colMax[c] = curValue[c];
                        colMin[c] = curValue[c];
                    }
                    else
                    {
                        if (curValue[c] > colMax[c])
                            colMax[c] = curValue[c];
                        if (curValue[c] < colMin[c])
                            colMin[c] = curValue[c];
                    }
                }
                colMean[c] = colSum[c]/height;
            });

            int sum = 0;
            for (int c = 0; c < width; c++)
            {
                sum += colMean[c];
                if (c == 0)
                {
                    max = colMax[c];
                    min = colMin[c];
                }
                else
                {
                    if (colMax[c]> max)
                        max=colMax[c];
                    if (colMin[c]<min)
                        min = colMin[c];
                }
            }
            mean = sum / width;
        }

        public void getBuffer(string path, int band)
        {
            int splitSum = (int)Math.Ceiling((double)height / 4096);
            int splitHeight = 4096;

            for (int i = 0; i < splitSum; i++)
            {
                if (i != splitSum - 1)
                    splitHeight = 4096;
                else
                    splitHeight = height % 4096;
                FileStream file = new FileStream($"{path}{i}\\{band}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] tBuf = new byte[splitHeight * width * 2];
                file.Read(tBuf, 0, width * splitHeight * 2);
                file.Close();
                Array.Copy(tBuf, 0, buffer, i * 4096 * 2048 * 2, width * splitHeight * 2);
            }

            long sum = 0;
            min = 4096;
            max = 0;
            for (UInt32 i = 0; i < width * height; i++)
            {
                sum += (UInt32)buffer[i * 2] + (UInt32)buffer[i * 2 + 1] * 256;
                if ((int)buffer[i * 2] + (int)buffer[i * 2 + 1] * 256 > max)
                    max = (int)buffer[i * 2] + (int)buffer[i * 2 + 1] * 256;
                if ((int)buffer[i * 2] + (int)buffer[i * 2 + 1] * 256 < min)
                    min = (int)buffer[i * 2] + (int)buffer[i * 2 + 1] * 256;
            }
            mean = (int)(sum / width * height);
        }

        public int getValue(int row, int col)
        {
            return (int)buffer[row * height * 2 + col * 2] + (int)buffer[row * height * 2 + col * 2 + 1] * 256;
        }
    }

    public class ImageSection
    {
        public static bool beginSection;        //是否开始抠图
        public static int startFrm, endFrm;      //取图的帧起始和结束
    }
}
