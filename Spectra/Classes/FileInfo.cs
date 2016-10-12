using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Spectra
{
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

        /*检查文件状态*/
        public static string checkFileState()
        {
            //检查MD5
            string md5str;
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(FileInfo.srcFilePathName))
                {
                    md5str = BitConverter.ToString(md5.ComputeHash(stream));
                }
            }
            FileInfo.md5 = md5str;
            /*汇报文件状态*/
            string strReport = "";
            DataTable fileDetail = SQLiteFunc.SelectDTSQL($"SELECT * from FileDetails where MD5='{md5str}'");
            if (fileDetail.Rows.Count == 0)
            {
                FileInfo.isUnpack = false;                                                                                  //未解包
                FileInfo.isDecomp = false;                                                                                  //未解压
                SQLiteFunc.ExcuteSQL("insert into FileDetails (文件名,文件路径,文件大小,是否已解包,是否已解压,MD5) values ('?','?','?','?','?','?')",
                    FileInfo.srcFileName, FileInfo.srcFilePathName, File.OpenRead(FileInfo.srcFilePathName).Length, "否", "否", FileInfo.md5);
                SQLiteFunc.ExcuteSQL("insert into FileDetails_dec (MD5) values ('?')", FileInfo.md5);
                strReport = DateTime.Now.ToString("HH:mm:ss") + " 文件第一次导入,未解包,未解压";
            }
            else
            {
                FileInfo.srcFileLength = Convert.ToInt64(fileDetail.Rows[0][2]);       //文件大小
                strReport = DateTime.Now.ToString("HH:mm:ss") + " 文件";
                if ((string)fileDetail.Rows[0][3] == "是")
                {
                    FileInfo.isUnpack = true;
                    FileInfo.upkFilePathName = Convert.ToString(SQLiteFunc.SelectDTSQL($"SELECT * from FileDetails_dec where MD5='{md5str}'").Rows[0][7]);
                    strReport += "已解包,";
                }
                else
                {
                    FileInfo.isUnpack = false;
                    strReport += "未解包,";
                }
                if ((string)fileDetail.Rows[0][4] == "是")
                {
                    FileInfo.isDecomp = true;
                    DataTable dt = SQLiteFunc.SelectDTSQL($"SELECT * from FileDetails_dec where MD5='{md5str}'");
                    if (dt.Rows[0][1] != DBNull.Value) FileInfo.frmSum = Convert.ToInt64(dt.Rows[0][1]);           //帧总数
                    if (dt.Rows[0][2] != DBNull.Value) FileInfo.startTime = Convert.ToDateTime(dt.Rows[0][2]);     //起始时间
                    if (dt.Rows[0][3] != DBNull.Value) FileInfo.endTime = Convert.ToDateTime(dt.Rows[0][3]);       //结束时间
                    if (dt.Rows[0][4] != DBNull.Value) FileInfo.startCoord.convertToCoord((string)dt.Rows[0][4]);  //起始经纬
                    if (dt.Rows[0][5] != DBNull.Value) FileInfo.endCoord.convertToCoord((string)dt.Rows[0][5]);    //结束经纬
                    if (dt.Rows[0][9] != DBNull.Value) FileInfo.decFilePathName = (string)dt.Rows[0][9];           //解压后路径
                    strReport += "已解压";
                }
                else
                {
                    FileInfo.isDecomp = false;
                    strReport += "未解压";
                }
            }
            return strReport;
        }







        [DllImport("DLL\\DataOperation.dll", EntryPoint = "preProcess_File")]
        static extern int preProcess_File(string pathName, int len);
        public static string preProcessFile(bool isUpk, int packLen)
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
}
