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

    public static class Variables
    {
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
        public static string MapPath = @"C:\Program Files\GMap\satellite";
        public static string MapType = "jpg";
        public static Coord LT_Coord = new Coord(0, 0);
        public static Coord RB_Coord = new Coord(0, 0);
    }
}
