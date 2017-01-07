using System;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;

namespace Spectra
{
    public static class FileInfo
    {
        public static string md5;                   /*文件的MD5值*/
        public static string srcFilePathName;       /*源文件路径全称*/
        public static string srcFileName;           /*源文件名称*/
        public static long srcFileLength;           /*源文件大小*/        
        public static bool isDecomp = false;        /*是否已解压*/        
        public static string decFilePathName;       /*解压后文件路径全称*/
        public static long frmSum;                  /*帧总数*/

        /*检查文件状态*/
        public static string checkFileState(string file)
        {
            FileInfo.srcFilePathName = file;                                                                                //文件路径名称
            FileInfo.srcFileName = FileInfo.srcFilePathName.Substring(FileInfo.srcFilePathName.LastIndexOf('\\') + 1);      //文件名称
            FileInfo.md5 = FileInfo.srcFileName.Substring(0, FileInfo.srcFileName.LastIndexOf('.'));                        //MD5
            FileInfo.srcFileLength = File.OpenRead(FileInfo.srcFilePathName).Length;                                        //文件大小
            /*汇报文件状态*/
            string strReport = DateTime.Now.ToString("HH:mm:ss") + " \"" + FileInfo.srcFileName.ToString() + "\" ";
            DataTable fileDetail = SQLiteFunc.SelectDTSQL($"SELECT * from FileDetails where MD5='{FileInfo.md5}'");
            if (fileDetail.Rows.Count == 0)
            {
                FileInfo.isDecomp = false;                                                                                  //未解压
                SQLiteFunc.ExcuteSQL("insert into FileDetails (文件名,文件路径,文件大小,是否已解压,MD5) values ('?','?','?','?','?')",
                    FileInfo.srcFileName, FileInfo.srcFilePathName, FileInfo.srcFileLength, "否", FileInfo.md5);
                strReport += "未解压";
            }
            else
            {
                if ((string)fileDetail.Rows[0]["是否已解压"] == "是")
                {
                    FileInfo.isDecomp = true;
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

        /// <summary>
        /// 获取所有需要的文件
        /// </summary>
        /// <param name="path">选定的路径</param>
        /// <returns>需要解压的文件列表</returns>
        public static string[] filterFiles(string path)
        {
            string[] files = getFiles(path);
            string name = "";
            int cnt = files.Length - 1;
            for (int i = cnt; i >=0 ; i--)
            {
                name = files[i];
                if (name.Substring(name.LastIndexOf('.')) != ".dat" || name.Substring(name.LastIndexOf('\\')+1).Substring(0,3) != "WN0")
                {
                    files = removeFiles(files, i);
                }
            }
            return files;
        }

        /// <summary>
        /// 获取路径下的所有文件
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns>文件列表</returns>
        public static string[] getFiles(string path)
        {
            string[] files = Directory.GetFiles(path);
            foreach (string dir in Directory.GetDirectories(path))
            {
                files = addFiles(files, getFiles(dir));
            }
            return files;
        }

        /// <summary>
        /// 将2个字符串数组拼接为1个
        /// </summary>
        /// <param name="f1">数组1</param>
        /// <param name="f2">数组2</param>
        /// <returns>拼接后数组</returns>
        public static string[] addFiles(string[] f1,string[] f2)
        {
            string[] files = new string[f1.Length+f2.Length];
            f1.CopyTo(files, 0);
            f2.CopyTo(files, f1.Length);
            return files;
        }

        public static string[] removeFiles(string[] f, int i)
        {
            string[] files = new string[f.Length - 1];
            int j = 0;
            for (int r = 0; r < f.Length; r++)
            {
                if (r == i)
                    continue;
                files[j++] = f[r];
            }
            return files;
        }
    }
}
