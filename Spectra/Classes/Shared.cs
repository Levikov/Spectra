using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Spectra
{
    public static class DataQuery
    {
        public static DataTable QueryResult = new DataTable();
    }

    public static class FileInfo
    {
        public static string srcFilePathName;       /*源文件全称*/
        public static long sizeSrcFile;             /*源文件大小*/
        public static string upkFilePathName;       /*解包后文件全称*/
        public static long sizeUpkFile;             /*解包后文件大小*/
        public static bool isUnpack = false;        /*是否已解包*/
        public static bool isDecomp = false;        /*是否已解压*/
        public static bool decFilePath;             /*解压后文件路径*/
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
    }

    public class WinShowInfo
    {
        public static int WindowsCnt = 0;               //显示的窗体个数
        public static DataTable dtWinShowInfo;          //默认显示方式的列表
    }
    public class ModelShowInfo
    { 
        public static int WindowsCnt = 0;               //应用样式的窗口数量
        public static DataTable dtModelList;            //应用样式列表
        public static DataTable dtWinShowInfo;          //应用样式细则
        public static DateTime Time_Start = DateTime.Now;
        public static DateTime Time_End = DateTime.Now;
        public static Coord Coord_TL = new Coord(0, 0); //左上角坐标
        public static Coord Coord_DR = new Coord(0, 0); //右下角坐标
    }

    public class ImageInfo : DependencyObject
    {
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

        public static byte[] imgAbnDetect;

        public ImageInfo()  { }

        public static void GetImgInfo(DataTable dt)
        {
            minFrm = Convert.ToInt32(dt.Compute("min(FrameId)", ""));
            maxFrm = Convert.ToInt32(dt.Compute("max(FrameId)", ""));
            imgWidth = maxFrm - minFrm + 1;

            startSec = Convert.ToDouble(dt.Compute("min(GST)", ""));
            endSec = Convert.ToDouble(dt.Compute("max(GST)", ""));
            startCoord.Lat = Convert.ToDouble(dt.Compute("min(Lat)", ""));
            startCoord.Lon = Convert.ToDouble(dt.Compute("min(Lon)", ""));
            endCoord.Lat = Convert.ToDouble(dt.Compute("max(Lat)", ""));
            endCoord.Lon = Convert.ToDouble(dt.Compute("max(Lon)", ""));
            DateTime T0 = new DateTime(1970, 1, 1, 0, 0, 0);
            startTime = T0.AddSeconds(startSec);
            endTime = T0.AddSeconds(endSec);
        }
    }
}
