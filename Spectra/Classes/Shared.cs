using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Spectra
{
    public static class DataQuery
    {
        public static DataTable QueryResult = new DataTable();
    }

    public static class FileInfo
    {
        public static string md5;                   /*文件的MD5值*/

        public static string srcFilePathName;       /*源文件路径全称*/
        public static string srcFileName;           /*源文件名称*/
        public static long srcFileLength;           /*源文件大小*/

        public static bool isUnpack = false;        /*是否已解包*/
        public static bool isDecomp = false;        /*是否已解压*/

        public static string upkFilePathName;       /*解包后文件路径全称*/
        public static string decFilePathName;       /*解压后文件路径全称*/

        public static long frmSum;                  /*帧总数*/
        public static DateTime startTime;
        public static DateTime endTime;
        public static Coord startCoord = new Coord(0, 0);
        public static Coord endCoord = new Coord(0, 0);

        [DllImport("DLL\\DataOperation.dll", EntryPoint = "preProcess_File")]
        static extern int preProcess_File(string pathName, int len);
        public static string preProcessFile(bool isUpk,int packLen)
        {
            string path = "";
            if (isUpk)
            {
                if (preProcess_File(FileInfo.upkFilePathName, packLen) == -1)
                {
                    MessageBox.Show("输入文件错误", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return path;
                }
                string[] s = FileInfo.upkFilePathName.Split('.');
                path = s[0] + "_p." + s[1];
            }
            else
            {
                if (preProcess_File(FileInfo.srcFilePathName, packLen) == -1)
                {
                    MessageBox.Show("输入文件错误", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return path;
                }
                string[] s = FileInfo.srcFilePathName.Split('.');
                path = s[0] + "_p." + s[1];
            }
            return path;
        }
    }

    public static class Variables
    {
        public static List<ScreenParams> Screens = new List<ScreenParams>();
        public static Rectangle[] Screen_Locations;

        /*-------------------------------------------------数据库------------------------------------------------------*/
        public static string dbPath = Environment.CurrentDirectory + "\\DataBase\\db.sqlite";   //数据库文件地址
        public static string dbConString = "Data Source=" + dbPath;                             //数据库连接字符串        
        public static string str_pathWork = Environment.CurrentDirectory + "\\Work";
    }

    public enum WinFunc {Image,Curve,Cube,Map};

    public enum GridMode {One,Two,Three,Four};

    public class ScreenParams
    {
        ScreenType Type;
        public double Width;
        public double Height;
        public double X;
        public double Y;
        public string Name;
        public bool Primary;

        public ScreenParams(Rectangle workingArea, string deviceName, bool primary)
        {
            this.Width = workingArea.Width;
            this.Height = workingArea.Height;
            this.X = workingArea.X;
            this.Y = workingArea.Y;
            this.Name = deviceName;
            this.Primary = primary;
            if (Height > Width) Type = ScreenType.Portrait;
            else Type = ScreenType.Landscape;

        }
    }

    public enum ScreenType { Landscape, Portrait };

    public class Coord
    {
        public double Lat;
        public double Lon;

        public Coord(double v1, double v2)
        {
            this.Lat = v1;
            this.Lon = v2;
        }

        public void convertToCoord(string str)
        {
            str = str.Substring(1, str.Length - 2);
            string[] s = str.Split(',');
            this.Lat = Convert.ToDouble(s[0]);
            this.Lon = Convert.ToDouble(s[1]);
        }

        public string convertToString()
        {
            return $"({Lat},{Lon})";
        }
    }

    public class WinShowInfo
    {
        public static int WindowsCnt = 0;               //显示的窗体个数
        public static DataTable dtWinShowInfo;          //默认显示方式的列表
    }

    public class ModelShowInfo
    { 
        [DllImport("DLL\\DataOperation.dll", EntryPoint = "Split_Chanel")]
        static extern void Split_Chanel(string path, string outpath, int sum, string[] file);

        public static int WindowsCnt = 0;               //应用样式的窗口数量
        public static DataTable dtModelList;            //应用样式列表
        public static DataTable dtWinShowInfo;          //应用样式细则
        public static DateTime Time_Start = DateTime.Now;
        public static DateTime Time_End = DateTime.Now;
        public static Coord Coord_TL = new Coord(0, 0); //左上角坐标
        public static Coord Coord_DR = new Coord(0, 0); //右下角坐标
        public static int imgWidth=0;                   //像宽(行数)
        public static DataTable dtImgInfo;              //应用样式细则
        public static bool isMakeImage;                 //是否已生成图像
        public static string strFilesPath;              //文件存储的路径
        public static string MD5;                       //MD5

        public static int MakeImage()
        {
            if (imgWidth == 0)
                return 0;
            //512拼2048*N图
            string[] strFileName = new string[imgWidth];
            for (int i = 0; i < imgWidth; i++)
                strFileName[i] = $"{(Convert.ToUInt32(dtImgInfo.Rows[i][2])).ToString("D10")}_{(Convert.ToUInt32(dtImgInfo.Rows[i][17])).ToString("D10")}_";
            Split_Chanel($"{Environment.CurrentDirectory}\\channelFiles\\", strFilesPath, imgWidth, strFileName);
            return 1;
        }
    }

    public class MapInfo : DependencyObject
    {
        public static string RoadMapPath = @"D:\Amap\roadmap";
        public static string TerrainMapPath = "";
    }

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
        public static Coord startCoord = new Coord(0,0);
        public static Coord endCoord = new Coord(0, 0);

        //图像的宽和高
        public static int imgWidth;
        public static int imgHeight = 2048;

        public ImageInfo()  { }

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
            DateTime T0 = new DateTime(2010, 12, 1, 12, 0, 0);
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
        public static int ImgDetectAbnormal(int v,int subWid)
        {
            FileStream fTest;
            try
            {
                fTest = new FileStream($"{FileInfo.decFilePathName}{v}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch
            {
                return -1;
            }

            /*分割大小*/
            int subLen = 2048 * subWid * 2;
            int subLast = (int)(fTest.Length % subLen);
            ImageInfo.imgWidth = (int)fTest.Length / 2048 / 2;   /*像宽*/
            int subCnt = (int)Math.Ceiling(ImageInfo.imgWidth / (double)subWid); /*以subWid为单位分割图像*/

            SQLiteConnection conn = new SQLiteConnection(Variables.dbConString);
            conn.Open();
            
            for (int i = 0; i < subCnt - 1; i++)
            {
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
            return Convert.ToDouble(dtBandWave.Rows[band-1][1]);
        }
    }

    public class ImageBuffer
    {
        public int width, height, mean, max, min;
        public byte[] buffer;

        public ImageBuffer(int w,int h)
        {
            width = w;
            height = h;
        }

        public void getBuffer(string path,int band)
        {
            if (!File.Exists($"{path}{band}.raw"))
                return;
            FileStream file = new FileStream($"{path}{band}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length == 0) return;
            buffer = new byte[file.Length];
            file.Read(buffer, 0, (int)file.Length);
            long sum = 0;
            min = 4096;
            max = 0;
            for (UInt32 i = 0; i < file.Length / 2; i++)
            {
                sum += (UInt32)buffer[i * 2] + (UInt32)buffer[i * 2 + 1] * 256;
                if ((int)buffer[i * 2] + (int)buffer[i * 2 + 1] * 256 > max)
                    max = (int)buffer[i * 2] + (int)buffer[i * 2 + 1] * 256;
                if ((int)buffer[i * 2] + (int)buffer[i * 2 + 1] * 256 < min)
                    min = (int)buffer[i * 2] + (int)buffer[i * 2 + 1] * 256;
            }
            mean = (int)(sum / (file.Length / 2));
        }

        public int getValue(int row,int col)
        {
            return (int)buffer[row * height * 2 + col * 2] + (int)buffer[row * height * 2 + col * 2 + 1] * 256;
        }
    }

    public class ImageSection
    {
        public static bool beginSection;        //是否开始抠图
        public static int startFrm,endFrm;      //取图的帧起始和结束
    }
}
