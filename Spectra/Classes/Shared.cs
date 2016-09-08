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

        public static int MakeImage()
        {
            if (imgWidth == 0)
                return 0;
            //512拼2048*N图
            string[] strFileName = new string[imgWidth];
            for (int i = 0; i < imgWidth; i++)
                strFileName[i] = $"{(Convert.ToUInt32(dtImgInfo.Rows[i][2])).ToString("D10")}_{(Convert.ToUInt32(dtImgInfo.Rows[i][17])).ToString("D10")}_";
            Split_Chanel($"{Environment.CurrentDirectory}\\channelFiles\\", $"{Environment.CurrentDirectory}\\showFiles\\", imgWidth, strFileName);
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

        public static long import_id = 0;
        public static int noise_value = 0;

        public static DataTable dtImgInfo;
        public static string[] strFileName;
        public static string channelFilesPath = Environment.CurrentDirectory + "\\channelFiles\\";
        public static string strFilesPath = Environment.CurrentDirectory + "\\showFiles\\";

        public static int minFrm;
        public static int maxFrm;
        public static double startSec;
        public static double endSec;
        public static DateTime startTime;
        public static DateTime endTime;
        public static Coord startCoord = new Coord(0,0);
        public static Coord endCoord = new Coord(0, 0);

        public static int imgWidth;
        public static int imgHeight = 2048;

        public ImageInfo()  { }

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
        /// <param name="Prog"></param>
        /// <returns></returns>
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
    }
}
