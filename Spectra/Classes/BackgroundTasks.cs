using FreeImageAPI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Spectra
{
    public class DataProc
    {
        #region 打开&解包
        /*解包*/
        public static Task<int> unpackFile(IProgress<DataView> IProg_DataView,IProgress<double> IProg_Bar,IProgress<string> IProg_Cmd)
        {
            return Task.Run(()=>
            {
                string strReport = "";
                srcFileSolve(IProg_Bar);
                FileInfo.isUnpack = true;
                strReport = DateTime.Now.ToString("HH:mm:ss") + " 文件已解包,未解压";
                IProg_Cmd.Report(strReport);
                //显示错误信息
                IProg_DataView.Report(SQLiteFunc.SelectDTSQL("select * from FileErrors where MD5='" + FileInfo.md5 + "'").DefaultView);
                return 1;
            });
        }
        /*解包-从原始数据以1024B为单元解包*/
        public static void srcFileSolve(IProgress<double>IProg_Bar)
        {
            string outPath = $"{Environment.CurrentDirectory}\\srcFiles\\{FileInfo.md5}.dat";
            FileStream srcFile = new FileStream(FileInfo.srcFilePathName, FileMode.Open, FileAccess.Read, FileShare.Read);
            FileStream outFile = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read);

            byte[] bufPack = new byte[1024];
            long errStart = 0, errEnd = 0;               //错误开始位置、错误结束位置
            bool isHaveErr = false, isInSql = false;     //是否有错、是否已经插入数据库
            //解包并判断帧头是否正确，不正确则写数据库
            while (srcFile.Position < srcFile.Length)
            {
                srcFile.Read(bufPack, 0, 1024);
                if (bufPack[0] == 0x1A && bufPack[1] == 0xCF && bufPack[2] == 0xFC && bufPack[3] == 0x1D)
                {
                    isInSql = true;
                    errEnd = srcFile.Position - 1024;
                    if (bufPack[12] == 0xAA && bufPack[13] == 0xAA && bufPack[14] == 0xAA && bufPack[15] == 0xAA)
                        continue;
                    outFile.Write(bufPack, 12, 884);
                    outFile.Flush();
                }
                else
                {
                    if (isInSql && isHaveErr)
                    {
                        SQLiteFunc.insertFileErrors(FileInfo.md5, errStart, "解包帧头错误");
                        isHaveErr = false;
                        isInSql = false;
                    }
                    if (!isHaveErr)
                    {
                        errStart = srcFile.Position - 1024;
                        isHaveErr = true;
                    }
                }

                if(srcFile.Position%(1024*1024*10)==0)
                IProg_Bar.Report((double)srcFile.Position/(double)srcFile.Length);
            }
            IProg_Bar.Report(1);
            if (isHaveErr)
            {
                SQLiteFunc.insertFileErrors(FileInfo.md5, errStart, "解包帧头错误");
                isInSql = false;
            }
            //关闭文件
            srcFile.Close();
            outFile.Close();
            //标记解包完成
            SQLiteFunc.ExcuteSQL("update FileDetails_dec set 解包时间='" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "',解包后文件路径='" + outPath + "' where MD5='" + FileInfo.md5 + "'");
            SQLiteFunc.ExcuteSQL("update FileDetails set 是否已解包='是' where MD5='" + FileInfo.md5 + "'");
            FileInfo.upkFilePathName = outPath;
        }
        #endregion

        #region 解压
        [DllImport("DLL\\DataOperation.dll", EntryPoint = "Split_Chanel")]
        static extern int Split_Chanel(string path, string outpath, int sum, string[] file);
        [DllImport("DLL\\DataOperation.dll", EntryPoint = "GetCurrentPosition")]
        static extern double GetCurrentPosition();
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        [DllImport("DLL\\DataOperation.dll", EntryPoint = "GetRGBFromBand")]
        public static extern void GetRGBFromBand(int band, out double R, out double G, out double B);
        [DllImport("DLL\\DataOperation.dll", EntryPoint = "Get3DRaw")]
        public static extern int Get3DRaw(string path, string outpath, int startFrm, int endFrm);

        public static void Split_Chanel(string path, string outpath, string[] file,IProgress<double>Prog, IProgress<string> List)
        {
            try
            {
                byte[][] buffer = new byte[160][];
                string cmdline = $"{DateTime.Now.ToString("HH:mm:ss")} 开始合并图像 ";
                List.Report(cmdline);
                for (int i = 0; i < 160; i++)
                {
                    buffer[i] = new byte[file.Count()*4096];
                }
                int sum = 0;
                Parallel.For(0, file.Count(), i => 
                {
                    Prog.Report((sum++) / (double)file.Count());
                    Parallel.For(0, 4, j => 
                    {
                        try
                        {
                            byte[] buf_file = File.ReadAllBytes($"{path}{file[i]}{j + 1}.raw");
                            for (int k = 0; k < 160; k++)
                            {
                                Array.Copy(buf_file, 1024 * k, buffer[k], i * 4096 + j* 1024, 1024);
                            }
                        }
                        catch(Exception e)
                        {

                        }
                    });
                });
                sum = 0;
                Parallel.For(0, 160, i => 
                {
                    Prog.Report((sum++) / 160.0);
                    File.WriteAllBytes($"{outpath}{i}.raw",buffer[i]);
                });

                Make_PseudoColor(outpath, outpath, file.Count(), new int[3] { 120, 83, 33 });
                DataProc.Get3DRaw(outpath, outpath, 0, file.Count() - 1);

                         
            }
            catch (Exception e)
            {
                throw e;
            }
       
        }

        public static void Make_PseudoColor(string path, string outpath, int sum, int[]band)
        {
            byte[] buffer_RGB = new byte[sum * 4096 * 3];
            double[][] buffer_Var = new double[3][];
            double[] factor_RGB = new double[3];
            Parallel.For(0,3,i=>
            {
                byte[] buffer_File = File.ReadAllBytes($"{path}{band[i]}.raw");
                buffer_Var[i] = new double[sum*2048];
                Parallel.For(0, sum*2048, j => { buffer_Var[i][j] = BitConverter.ToUInt16(buffer_File,2*j); });
                factor_RGB[i] = buffer_Var[i].Average();
            });
            Parallel.For(0, 3, i =>
            {
                double factor = factor_RGB.Max() / factor_RGB[i];
                Parallel.For(0, sum*2048, j => 
                {
                    buffer_RGB[6 * j + 2 * i] = (byte)((ushort)(factor * buffer_Var[i][j]) & 0x0f);
                    buffer_RGB[6 * j + 2 * i+1] = (byte)((ushort)(factor * buffer_Var[i][j]) >>8);

                });
            });
            File.WriteAllBytes($"{outpath}160.raw", buffer_RGB);
            
        }



        public static Task<string> Import_5(int PACK_LEN,IProgress<double> Prog, IProgress<string> List, CancellationToken cancel)
        {
            return Task.Run(()=> {

                string cmdline = "";
                string srcPath = FileInfo.preProcessFile(FileInfo.isUnpack, PACK_LEN);   //预处理
                if (srcPath == "")
                    return "输入文件错误";
                else
                    cmdline = "文件预处理完成!";

                IntPtr hModule = LoadLibrary("DLL\\DataOperation.dll");
                IntPtr hVariable = GetProcAddress(hModule, "progress");
                SQLiteDatabase sqlExcute = new SQLiteDatabase(Variables.dbPath);
                
                long import_id = 1;

                //如果输出路径不存在，则创建
                if (!Directory.Exists($"{Environment.CurrentDirectory}\\decFiles\\{FileInfo.md5}"))
                {
                    Directory.CreateDirectory($"{Environment.CurrentDirectory}\\channelFiles");                                     //存储每个通道解压后的文件
                    Directory.CreateDirectory($"{Environment.CurrentDirectory}\\showFiles");                                        //存储要显示的文件（即检索结果）
                    Directory.CreateDirectory($"{Environment.CurrentDirectory}\\decFiles\\{FileInfo.md5}\\jp2");
                    Directory.CreateDirectory($"{Environment.CurrentDirectory}\\decFiles\\{FileInfo.md5}\\result");
                }

                //分包并解压
                Parallel.For(0, 4, i =>
                {
                    FileStream fs_split = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read);  //打开源文件
                    FileStream fs_out = new FileStream($"test_{i}", FileMode.Create);
                    AuxDataRow adr_last = new AuxDataRow(new byte[PACK_LEN], 0);                                                     //最近一包的行结构，新建时为0
                    byte[] buf_split = new byte[PACK_LEN];                                                                           //一包数据
                    while (fs_split.Position < fs_split.Length)
                    {
                        fs_split.Read(buf_split, 0, PACK_LEN);
                        int sum = 0;
                        ROW checkrow = new ROW(buf_split, import_id);
                        int resultCheck = checkrow.isValid();
                        if (resultCheck != 1)                                                                                    //判断数据格式及校验和是否正确
                        {
                            cmdline = $"{DateTime.Now.ToString("HH:mm:ss")} 位置:{fs_split.Position} 错误代号:{resultCheck}";
                            continue;
                        }

                        if (buf_split[5] != i + 1)                                                                                      //只对该通道处理
                            continue;
                        if (buf_split[4] == 0x0A)                                                                                       //若是有效数据
                        {
                            RealDataRow rdr = new RealDataRow(buf_split, import_id);
                            if (rdr.FrameCount == adr_last.FrameCount)                                                                  //帧号正确则写文件
                            {
                                rdr.Insert(fs_out);
                            }
                        }
                        else if (buf_split[4] == 0x08)                                                                                  //若是辅助数据
                        {
                            AuxDataRow adr = new AuxDataRow(buf_split, import_id);                                                      //最新一包的行结构
                            if (adr.FrameCount != adr_last.FrameCount)
                            {
                                fs_out.Close();
                                sum++;
                                if (i == 0) cmdline = $"{DateTime.Now.ToString("HH:mm:ss")} 帧号:{adr_last.FrameCount} ";
                                try
                                {
                                    FIBITMAP fibmp = FreeImage.LoadEx($"{Environment.CurrentDirectory}\\decFiles\\{FileInfo.md5}\\jp2\\{adr_last.CapTimeS.ToString("D10")}_{adr_last.CapTimeUS.ToString("D10")}_{adr_last.Chanel}.jp2");
                                    if (!fibmp.IsNull)
                                    {
                                        byte[] buf_JP2 = new byte[512 * 160 * 2];
                                        byte[] buf_Dynamic = new byte[512 * 2];
                                        Marshal.Copy(FreeImage.GetBits(fibmp), buf_JP2, 0, 512 * 160 * 2);
                                        Array.Copy(buf_JP2, 40 * 512 * 2, buf_Dynamic, 0, 1024);
                                        //App.global_Win_Dynamic.Update(buf_Dynamic, adr_last.FrameCount, adr_last.Chanel);
                                        FreeImage.Unload(fibmp);
                                        FileStream fs_out_raw = new FileStream($"{Environment.CurrentDirectory}\\channelFiles\\{adr_last.CapTimeS.ToString("D10")}_{adr_last.CapTimeUS.ToString("D10")}_{adr_last.Chanel}.raw", FileMode.Create);
                                        fs_out_raw.Write(buf_JP2, 0, 512 * 160 * 2);
                                        fs_out_raw.Close();
                                        if (i == 0) cmdline += "解压成功！";
                                    }
                                    else
                                    {
                                        cmdline += $"通道{i}解压失败";
                                    }
                                }
                                catch { }
                                //更新界面
                                if (i == 0)
                                {
                                    Prog.Report((double)fs_split.Position / (double)fs_split.Length);
                                    List.Report(cmdline);
                                }
                                //新建.jp2文件
                                fs_out = new FileStream($"{Environment.CurrentDirectory}\\decFiles\\{FileInfo.md5}\\jp2\\{adr.CapTimeS.ToString("D10")}_{adr.CapTimeUS.ToString("D10")}_{adr.Chanel}.jp2", FileMode.Create, FileAccess.Write, FileShare.Write);
                                adr_last = adr;
                            }
                        }
                    }
                    fs_out.Close();
                });

                //App.global_Win_Dynamic.StopTimer();

                //解压完成后才对数据库进行操作
                List.Report($"{DateTime.Now.ToString("HH:mm:ss")} 开始写数据库");
                /*需要添加清除该MD5数据的代码*/
                sqlExcute.ExecuteNonQuery("delete from AuxData where MD5=@MD5",
                    new List<SQLiteParameter>()
                        {
                            new SQLiteParameter("MD5",FileInfo.md5)
                        });
                sqlExcute.BeginInsert();
                FileStream fs_chanel = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] buf_row1 = new byte[PACK_LEN * 1024 * 1024];
                bool isErrWrDB = true;
                while (fs_chanel.Position < fs_chanel.Length)
                {
                    fs_chanel.Read(buf_row1, 0, PACK_LEN);
                    ROW checkrow = new ROW(buf_row1, import_id);
                    if (checkrow.isValid() != 1 && isErrWrDB)
                    {
                        isErrWrDB = false;
                        checkrow.InsertError(fs_chanel.Position, sqlExcute);
                    }
                    if ((buf_row1[4] == 0x08) && (buf_row1[5] == 0x01))
                    {
                        AuxDataRow adr = new AuxDataRow(buf_row1, import_id);
                        bool flag = true;

                        Parallel.For(1, 5, i =>
                        {
                            if (File.Exists($"{Environment.CurrentDirectory}\\channelFiles\\{adr.CapTimeS.ToString("D10")}_{adr.CapTimeUS.ToString("D10")}_{adr.Chanel}.raw")) flag = flag & true;
                            else flag = flag & false;
                        });
                        if (flag)
                            adr.Insert(sqlExcute);
                    }
                    if (fs_chanel.Position % (PACK_LEN * 1024) == 0)
                    {
                        Prog.Report((double)fs_chanel.Position / fs_chanel.Length);
                    }
                }
                fs_chanel.Close();
                try
                {
                    File.Delete(srcPath);
                }
                catch { }
                List.Report($"{DateTime.Now.ToString("HH:mm:ss")} 写数据库完成");

                //下面这两句会报错,不知为什么
                sqlExcute.EndInsert();
                sqlExcute.cnn.Open();

                //更新文件信息
                FileInfo.frmSum = (long)sqlExcute.ExecuteScalar($"SELECT COUNT(*) FROM AuxData WHERE MD5='{FileInfo.md5}'");                                    //帧总数
                DateTime T0 = new DateTime(2010, 12, 1, 12, 0, 0);
                FileInfo.startTime = T0.AddSeconds((double)sqlExcute.ExecuteScalar($"SELECT GST FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId ASC"));//起始时间
                FileInfo.endTime = T0.AddSeconds((double)sqlExcute.ExecuteScalar($"SELECT GST FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId DESC")); //结束时间
                double lat = (double)sqlExcute.ExecuteScalar($"SELECT Lat FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId ASC");
                double lon = (double)sqlExcute.ExecuteScalar($"SELECT Lon FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId ASC");
                FileInfo.startCoord.convertToCoord($"({lat},{lon})");                                                                                           //起始经纬
                lat = (double)sqlExcute.ExecuteScalar($"SELECT Lat FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId DESC");
                lon = (double)sqlExcute.ExecuteScalar($"SELECT Lon FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId DESC");
                FileInfo.endCoord.convertToCoord($"({lat},{lon})");                                                                                             //结束经纬
                long Frm_Start = (long)sqlExcute.ExecuteScalar($"SELECT FrameId FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId ASC");                 //帧起始

                DataTable dtGST = sqlExcute.GetDataTable("select GST,GST_US from AuxData where MD5=@MD5",
                    new List<SQLiteParameter>()
                        {
                            new SQLiteParameter("MD5",FileInfo.md5)
                        });
                string[] strGST = new string[dtGST.Rows.Count];
                for (int i = 0; i < dtGST.Rows.Count; i++)
                    strGST[i] = $"{(Convert.ToUInt32(dtGST.Rows[i][0])).ToString("D10")}_{(Convert.ToUInt32(dtGST.Rows[i][1])).ToString("D10")}_";


                //512拼2048*N图
                DataProc.Split_Chanel($"{Environment.CurrentDirectory}\\channelFiles\\", $"{Environment.CurrentDirectory}\\decFiles\\{FileInfo.md5}\\result\\", strGST,Prog,List);
                Prog.Report(1);

                return "成功！";
            });

        }
        #endregion

        #region Bitmap Operations
        /// <summary>
        /// 通过raw文件转为bmp进行显示
        /// </summary>
        /// <param name="path">图像文件的路径</param>
        /// <param name="v">谱段号</param>
        /// <param name="cMode">显示的模式:灰度、伪彩、真彩</param>
        /// <returns></returns>
        public static Task<Bitmap> GetBmp(string path, int v, ColorRenderMode cMode)
        {
            return Task.Run(() =>
            {
                int Height = ImageInfo.dtImgInfo.Rows.Count;
                int Width = 2048;
                int chanel = 1;
                if (v == 161 || v == 162) Height = 128;
                if (v == 163 || v == 164) Width = 128;
                if (cMode ==ColorRenderMode.RealColor) chanel = 3; 

                byte[] buf_full = new byte[Width * Height * 3];
                byte[] buf_band;
                buf_band = new byte[Width * Height * 2*chanel];

                if (!File.Exists($"{path}{v}.raw") || Height < 1) return null;
                FileStream fs = new FileStream($"{path}{v}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs == null) return null;
                fs.Read(buf_band, 0, Width * Height * 2*chanel);
                Parallel.For(0, Width * Height, i =>
                {
                    switch (cMode)
                    {
                        case ColorRenderMode.Grayscale:
                            {
                                buf_full[3 * i] = buf_full[3 * i + 1] = buf_full[3 * i + 2] = (byte)(Math.Floor((double)(readU16_PIC(buf_band, 2 * i)) / 4096 * 256));
                            }
                            break;
                        case ColorRenderMode.ArtColor:
                            {

                                double fR = 0;
                                double fG = 0;
                                double fB = 0;
                                GetRGBFromBand(i / 2048+27, out fR,out fG, out fB);
                                double R = (double)(readU16_PIC(buf_band, i * 2)) / 4096 * fR * 256;
                                double G = (double)(readU16_PIC(buf_band, i * 2)) / 4096 * fG * 256;
                                double B = (double)(readU16_PIC(buf_band, i * 2)) / 4096 * fB * 256;

                                buf_full[3 * i + 2] = (byte)(Math.Floor(R));
                                buf_full[3 * i + 1] = (byte)(Math.Floor(G));
                                buf_full[3 * i + 0] = (byte)(Math.Floor(B));
                            }
                            
                            break;
                        case ColorRenderMode.RealColor:
                            {
                                double R = (double)(readU16_PIC(buf_band, i * 6+4)) / 4096  * 256;
                                double G = (double)(readU16_PIC(buf_band, i * 6+2)) / 4096  * 256;
                                double B = (double)(readU16_PIC(buf_band, i * 6)) / 4096  * 256;

                                buf_full[3 * i + 2] = (byte)(Math.Floor(R));
                                buf_full[3 * i + 1] = (byte)(Math.Floor(G));
                                buf_full[3 * i + 0] = (byte)(Math.Floor(B));
                            }

                            break;
                        case ColorRenderMode.ArtColorSide:
                            {

                                double fR = 0;
                                double fG = 0;
                                double fB = 0;
                                GetRGBFromBand(i%Width+27, out fR, out fG, out fB);
                                double R = (double)(readU16_PIC(buf_band, i * 2)) / 4096 * fR * 256;
                                double G = (double)(readU16_PIC(buf_band, i * 2)) / 4096 * fG * 256;
                                double B = (double)(readU16_PIC(buf_band, i * 2)) / 4096 * fB * 256;

                                buf_full[3 * i + 2] = (byte)(Math.Floor(R));
                                buf_full[3 * i + 1] = (byte)(Math.Floor(G));
                                buf_full[3 * i + 0] = (byte)(Math.Floor(B));
                            }

                            break;
                        case ColorRenderMode.TrueColor:
                            {
                                int band = v;
                                if (v == 165) v = 27;
                                if (v == 166) v = 154;

                                double fR = 0;
                                double fG = 0;
                                double fB = 0;
                                GetRGBFromBand(v, out fR, out fG, out fB);
                                double R = (double)(readU16_PIC(buf_band, i * 2)) / 4096 * fR * 256;
                                double G = (double)(readU16_PIC(buf_band, i * 2)) / 4096 * fG * 256;
                                double B = (double)(readU16_PIC(buf_band, i * 2)) / 4096 * fB * 256;

                                buf_full[3 * i + 2] = (byte)(Math.Floor(R));
                                buf_full[3 * i + 1] = (byte)(Math.Floor(G));
                                buf_full[3 * i + 0] = (byte)(Math.Floor(B));
                            }
                            break;
                        default:
                            break;
                    }
                });
                fs.Close();

                Bitmap bmpTop = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                BitmapData bmpData = bmpTop.LockBits(new System.Drawing.Rectangle(0, 0, Width, Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bmpTop.PixelFormat);
                Marshal.Copy(buf_full, 0, bmpData.Scan0, Width * Height * 3);
                bmpTop.UnlockBits(bmpData);
                return bmpTop;
            });
        }
        public static Task<Bitmap> GetRealColorBmp(string path)
        {
            return Task.Run(() =>
            {
                int Height = ImageInfo.dtImgInfo.Rows.Count;
                int Width = 2048;

                byte[] buf_full = new byte[Width * Height * 2 * 3];
                byte[] buf_band = new byte[Width * Height * 2];
                if (!File.Exists($"{path}{160}.raw") || Height < 1) return null;
                FileStream fs = new FileStream($"{path}{160}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs == null) return null;
                fs.Read(buf_full, 0, Width * Height * 2 * 3);
                Bitmap bmpTop = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format48bppRgb);
                BitmapData bmpData = bmpTop.LockBits(new System.Drawing.Rectangle(0, 0, Width, Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bmpTop.PixelFormat);
                Marshal.Copy(buf_full, 0, bmpData.Scan0, Width * Height * 2 * 3);
                bmpTop.UnlockBits(bmpData);
                return bmpTop;
            });
        }
        /// <summary>
        /// 取三维立方体
        /// </summary>
        /// <param name="path">raw文件的存储位置</param>
        /// <returns>bmp图像</returns>
        public static Task<Bitmap[]> GetBmp3D(string path)
        {
            return Task.Run(async () =>
            {
                Bitmap[] r = new Bitmap[6];
                r[0] = await BmpOper.MakePseudoColor(path, new UInt16[] { 40, 77, 127 }, 4);
                r[1] = await BmpOper.MakePseudoColor(path, new UInt16[] { 40, 40, 40 }, 4);
                r[3] = await GetBmp(path, 161, ColorRenderMode.ArtColor);
                r[2] = await GetBmp(path, 162, ColorRenderMode.ArtColor);
                r[4] = await GetBmp(path, 163, ColorRenderMode.ArtColorSide);
                r[5] = await GetBmp(path, 164, ColorRenderMode.ArtColorSide);
                return r;
            });
        }
        #endregion

        #region Spectrum Curves
        public static Task<double[,]> GetChart()
        {
            return Task.Run(() => {
                double[,] result = new double[160, 2];
                return result;
            });
        }
        #endregion

        #region 图像检索
        public static Task<DataTable> QueryResult(string md5,bool isChecked1, bool isChecked2, bool isChecked3, DateTime start_time, DateTime end_time, long start_FrmCnt, long end_FrmCnt, Coord coord_TL, Coord coord_DR)
        {
            return Task.Run(() =>
            {
                SQLiteConnection conn = new SQLiteConnection(Variables.dbConString);
                conn.Open();
                SQLiteCommand cmmd = new SQLiteCommand("", conn);

                string command;
                if (md5 != null && md5 != "")
                    command = $"SELECT * FROM AuxData WHERE MD5='{md5}'";
                else
                    command = $"SELECT * FROM AuxData";
                if ((bool)isChecked2)
                {
                    command += " AND Lat>=" + (coord_DR.Lat.ToString()) + " AND Lat<=" + coord_TL.Lat.ToString() + " AND Lon>=" + (coord_TL.Lon.ToString()) + " AND Lon<=" + coord_DR.Lon.ToString();
                }
                if ((bool)isChecked1)
                {
                    DateTime selectedStartDate = start_time;
                    DateTime selectedEndDate = end_time;
                    DateTime T0 = new DateTime(2010, 12, 1, 12, 0, 0);
                    TimeSpan ts_Start = selectedStartDate.Subtract(T0);
                    TimeSpan ts_End = selectedEndDate.Subtract(T0);
                    command += " AND GST>=" + (ts_Start.TotalSeconds.ToString()) + " AND GST<=" + ts_End.TotalSeconds.ToString();
                }
                if ((bool)isChecked3)
                {
                    command += " AND FrameId>=" + start_FrmCnt.ToString() + " AND FrameId<=" + end_FrmCnt.ToString();
                }
                
                SQLiteDatabase db = new SQLiteDatabase(Variables.dbPath);
                return db.GetDataTable(command);
            });
        }
        #endregion

        #region 处理
        public static UInt32 readU32(byte[] buf_row, int addr)
        {
            byte[] conv = new byte[4] { buf_row[addr + 3], buf_row[addr + 2], buf_row[addr + 1], buf_row[addr] };
            return BitConverter.ToUInt32(conv, 0);
        }
        public static Int32 readI32(byte[] buf_row, int addr)
        {
            byte[] conv = new byte[4] { buf_row[addr + 3], buf_row[addr + 2], buf_row[addr + 1], buf_row[addr] };
            return BitConverter.ToInt32(conv, 0);
        }
        public static UInt16 readU16(byte[] buf_row, int addr)
        {
            byte[] conv = new byte[2] { buf_row[addr + 1], buf_row[addr] };
            return BitConverter.ToUInt16(conv, 0);
        }
        public static UInt16 readU16_PIC(byte[] buf_row, int addr)
        {
            byte[] conv = new byte[2] { buf_row[addr], buf_row[addr+1] };
            return BitConverter.ToUInt16(conv, 0);
        }
        public static byte readU8(byte[] buf_row, int addr)
        {
            return buf_row[addr];
        }
        public static float readF32(byte[] buf_row, int addr)
        {
            byte[] conv = new byte[4] { buf_row[addr + 3], buf_row[addr + 2], buf_row[addr + 1], buf_row[addr] };
            int a = BitConverter.ToInt32(conv, 0);
            float result = (float)a / 1000;
            return result;
        }
        public static float readLength(byte[] buf_row, int addr)
        {
            float result = (float)readI32(buf_row, addr) / 1000;
            return result;
        }
        public static float readDegree(byte[] buf_row, int addr)
        {
            float result = (float)((float)readI32(buf_row, addr) / 1000 * 180 / Math.PI);
            return result;
        }
        #endregion
    }

    #region FRAME class

    public class ROW
    {
        public UInt32 CapTimeS;     //捕获时间（时间码秒值）
        public UInt32 CapTimeUS;    //捕获时间（时间码微秒值）
        public UInt16 FrameCount;
        public UInt16 PackCount;
        public byte Chanel;
        public long ImportId = 0;
        public byte[] buf_Row;
        public ROW(byte[] ROW, long id)
        {
            CapTimeS = DataProc.readU32(ROW, 17);
            CapTimeUS = DataProc.readU32(ROW, 104);
            FrameCount = DataProc.readU16(ROW, 6);
            PackCount = DataProc.readU16(ROW, 8);
            Chanel = DataProc.readU8(ROW, 5);
            buf_Row = ROW;
            ImportId = id;
        }

        public void InsertTemp(SQLiteDatabase sqlExcute, long row_addr)
        {
            var sql = "insert into temp values(@import_id,@frame_id,@pkg_id,@chanel,@row_addr);";
            var cmdparams = new List<SQLiteParameter>()
                {
                    new SQLiteParameter("import_id", ImportId),
                    new SQLiteParameter("frame_id",FrameCount),
                    new SQLiteParameter("pkg_id",PackCount),
                    new SQLiteParameter("chanel",Chanel),
                    new SQLiteParameter("row_addr",row_addr),
                };
            try
            {
                sqlExcute.ExecuteNonQuery(sql, cmdparams);
            }
            catch (Exception e)
            {
                //Do any logging operation here if necessary
                throw e;
            }
        }

        public int isValid()
        {
            //帧头
            if (DataProc.readU32(buf_Row, 0) != 0xEB905716)
                return -1;
            //帧尾
            if (DataProc.readU32(buf_Row, 276) != 0x13AB13AB)
                return -2;

            UInt32 i = 0, sum = 0;
            //计算校验和
            for (i = 0; i < 272;)
            {
                sum += (UInt32)(buf_Row[i + 0] << 24 | buf_Row[i + 1] << 16 | buf_Row[i + 2] << 8 | buf_Row[i + 3]);
                i += 4;
            }
            //判断校验和
            if (sum == (UInt32)(buf_Row[272 + 0] << 24 | buf_Row[272 + 1] << 16 | buf_Row[272 + 2] << 8 | buf_Row[272 + 3]))
                return 1;
            else
                return -3;
        }

        public void InsertError(long position,SQLiteDatabase sql)
        {
            switch (isValid())
            {
                case -1:
                    sql.ExecuteNonQuery("insert into FileErrors (MD5,错误位置,错误类型) values (@MD5,@errorpos,@error)",
                        new List<SQLiteParameter>()
                        {
                            new SQLiteParameter("MD5",FileInfo.md5),
                            new SQLiteParameter("errorpos",position),
                            new SQLiteParameter("error","解压帧头错误")
                        }); ; break;
                case -2:
                    sql.ExecuteNonQuery("insert into FileErrors (MD5,错误位置,错误类型) values (@MD5,@errorpos,@error)",
                    new List<SQLiteParameter>()
                    {
                            new SQLiteParameter("MD5",FileInfo.md5),
                            new SQLiteParameter("errorpos",position),
                            new SQLiteParameter("error","解压帧尾错误")
                    }); ; break;
                case -3:
                    sql.ExecuteNonQuery("insert into FileErrors (MD5,错误位置,错误类型) values (@MD5,@errorpos,@error)",
                   new List<SQLiteParameter>()
                   {
                            new SQLiteParameter("MD5",FileInfo.md5),
                            new SQLiteParameter("errorpos",position),
                            new SQLiteParameter("error","解压校验和错误")
                   }); ; break;
                default: break;
            }
        }
    }

    public class RealDataRow : ROW
    {
        public bool isHead = false;
        public bool isTail = false;
        public RealDataRow(byte[] ROW, long id) : base(ROW, id)
        {
            if ((UInt32)(ROW[32] << 24 | ROW[33] << 16 | ROW[34] << 8 | ROW[35]) == 0xFF4FFF51)
            {
                isHead = true;
            }
        }

        public void Insert()
        {
            FileStream fs = new FileStream(Variables.str_pathWork + "\\" + ImportId.ToString() + "_" + FrameCount.ToString() + "_" + Chanel.ToString() + ".jp2", FileMode.Append, FileAccess.Write, FileShare.Write);
            if (isHead) fs.Write(buf_Row, 32, 240);
            else fs.Write(buf_Row, 16, 256);
            fs.Close();
        }

        public void Insert(FileStream fs)
        {
            if (isHead) fs.Write(buf_Row, 32, 240);
            else fs.Write(buf_Row, 16, 256);
        }
    }

    public class AuxDataRow : ROW
    {
        protected double GST;
        protected long GST_US;
        protected double Lat;
        protected double Lon;
        protected double X;
        protected double Y;
        protected double Z;
        protected double Vx;
        protected double Vy;
        protected double Vz;
        protected double Ox;
        protected double Oy;
        protected double Oz;
        protected double Q1;
        protected double Q2;
        protected double Q3;
        protected double Q4;
        public AuxDataRow(byte[] ROW, long id) : base(ROW, id)
        {
            //X = 7000 * Math.Cos(Math.PI * ((double)(FrameCount % 360) / 180));//readLength(32);
            //Y = 7000 * Math.Sin(Math.PI * ((double)(FrameCount % 360) / 180));//readLength(36);
            //Z = 0;//readLength(40);
            //Vx = 7.546 * Math.Cos(Math.PI * ((double)(FrameCount % 360) / 180) + Math.PI / 2);//readLength(44);
            //Vy = 7.546 * Math.Sin(Math.PI * ((double)(FrameCount % 360) / 180) + Math.PI / 2);//readLength();
            //Vz = 0;//0;//
            GST = DataProc.readU32(ROW, 17);
            GST_US = DataProc.readU32(ROW, 104);

            X = DataProc.readI32(ROW, 32) / (double)1000;
            Y = DataProc.readI32(ROW, 36) / (double)1000;
            Z = DataProc.readI32(ROW, 40) / (double)1000;
            double[] latlon = new double[2];
            latlon = OrbitCalc.CalEarthLonLat(new double[3] { X, Y, Z }, GST);
            Lat = double.IsNaN(latlon[1]) ? 0 : latlon[1];
            Lon = double.IsNaN(latlon[0]) ? 0 : latlon[0];

            Vx = DataProc.readI32(ROW, 44) / (double)1000;
            Vy = DataProc.readI32(ROW, 48) / (double)1000;
            Vz = DataProc.readI32(ROW, 52) / (double)1000;
            Ox = DataProc.readI32(ROW, 81) / (double)10000 * 57.3;
            Oy = DataProc.readI32(ROW, 85) / (double)10000 * 57.3;
            Oz = DataProc.readI32(ROW, 89) / (double)10000 * 57.3;
            Q1 = DataProc.readI32(ROW, 65) / (double)10000 * 57.3;
            Q2 = DataProc.readI32(ROW, 69) / (double)10000 * 57.3;
            Q3 = DataProc.readI32(ROW, 73) / (double)10000 * 57.3;
            Q4 = DataProc.readI32(ROW, 77) / (double)10000 * 57.3;
        }

        public void Insert(SQLiteDatabase sqlExcute)
        {
            var sql = "insert into AuxData values(@FrameId,@SatelliteId,@GST,@Lat,@Lon,@X,@Y,@Z,@Vx,@Vy,@Vz,@Ox,@Oy,@Oz,@ImportId,@Chanel,@MD5,@GST_US,@Q1,@Q2,@Q3,@Q4);";
            var cmdparams = new List<SQLiteParameter>()
                {
                    new SQLiteParameter("FrameId", FrameCount),
                    new SQLiteParameter("SatelliteId","MicroSat"),
                    new SQLiteParameter("GST",GST),
                    new SQLiteParameter("Lat",Lat),
                    new SQLiteParameter("Lon",Lon),
                    new SQLiteParameter("X",X),
                    new SQLiteParameter("Y",Y),
                    new SQLiteParameter("Z",Z),
                    new SQLiteParameter("Vx",Vx),
                    new SQLiteParameter("Vy",Vy),
                    new SQLiteParameter("Vz",Vz),
                    new SQLiteParameter("Ox",Ox),
                    new SQLiteParameter("Oy",Oy),
                    new SQLiteParameter("Oz",Oz),
                    new SQLiteParameter("ImportId",ImportId),
                    new SQLiteParameter("Chanel",Chanel),
                    new SQLiteParameter("MD5",FileInfo.md5),
                    new SQLiteParameter("GST_US",GST_US),
                    new SQLiteParameter("Q1",Q1),
                    new SQLiteParameter("Q2",Q2),
                    new SQLiteParameter("Q3",Q3),
                    new SQLiteParameter("Q4",Q4)
                };

            try
            {
                sqlExcute.ExecuteNonQuery(sql, cmdparams);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }
    }

    public class FRAME
    {
        public List<RealDataRow>[] ItemList;

        public ushort FrmCnt;

        public FRAME()
        {
            ItemList = new List<RealDataRow>[4];
            FrmCnt = 0;
        }

        public override bool Equals(object obj)
        {
            if (obj is FRAME)
            {
                FRAME f = obj as FRAME;
                if (f.FrmCnt == FrmCnt) return true;
                else return false;

            }

            return false;
        }

        public FRAME(ushort frameCount)
        {
            this.FrmCnt = frameCount;
            ItemList = new List<RealDataRow>[4];

        }

        public void InsertRow(RealDataRow row)
        {
            ItemList[row.Chanel - 1].Add(row);
            FrmCnt = row.FrameCount;
            //ItemList.Sort(byFrmCnt);
        }

        private int byFrmCnt(RealDataRow T1, RealDataRow T2)
        {
            if (T1.PackCount > T2.PackCount) return 1;
            else return -1;
        }

        public void WriteFile()
        {

            for (int i = 0; i < 4; i++)
            {

                ThreadPool.QueueUserWorkItem(o => {
                    FileStream fs = new FileStream(Variables.str_pathWork + "\\" + ItemList[0][0].ImportId.ToString() + "_" + this.FrmCnt.ToString() + "_" + (i + 1).ToString() + ".jp2", FileMode.Append, FileAccess.Write, FileShare.Write);

                    foreach (RealDataRow rdr in ItemList[i])
                    {
                        if (rdr.isHead) fs.Write(rdr.buf_Row, 32, 240);
                        else fs.Write(rdr.buf_Row, 16, 256);
                    }

                    fs.Close();

                });
            }


        }




    }

    #endregion

    #region Orbit Calculation Class
    public class OrbitCalc
    {


        const int POSE_GST0TIME = 63417600;
        const float POSE_GST0 = 5.986782214F;
        const float POSE_WE = 7.29211514667e-5F;
        const float POSE_FZERO = 0.0000001F;
        public OrbitCalc()
        { }
        public static double[] CalEarthLonLat(double[] cuR, double fgst)
        {
            double[] clonlat = new double[2];
            double temp, sra, lon;
            temp = Math.Sqrt(cuR[0] * cuR[0] + cuR[1] * cuR[1]);             //|R|
            if (Math.Abs(temp) < POSE_FZERO)                             //R==0
            {
                clonlat[1] = Math.PI * 0.5 * cuR[2] / Math.Abs(cuR[2]);    //Recs[2]>0.0,则fLati = PI05
                sra = 0.0F;
            }
            else
            {
                clonlat[1] = Math.Atan(cuR[2] / temp);             //得到纬度[-PI05 PI05]
                sra = Math.Atan2(cuR[1], cuR[0]);                  //得到经度[-PI PI]
            }
            lon = sra - fgst;
            lon = lon - (int)(lon / Math.PI / 2) * Math.PI * 2;                       //POSE_MODF规整到给定范围

            if (lon > Math.PI)
                lon = lon - Math.PI * 2;
            else if (lon < -Math.PI)
                lon = lon + Math.PI * 2;
            else { }
            clonlat[0] = lon;                                       //经度
            return clonlat;
        }

        public static float CalGST(POSE_TIME cTime)
        {
            float lfgst;
            float LfDelt;
            POSE_TIME LgstT = new POSE_TIME();

            LgstT.S = POSE_GST0TIME;    //秒值为宏
            LgstT.M = 0;                //微秒为0

            LfDelt = CalDeltTime(cTime, LgstT);
            lfgst = POSE_GST0 + LfDelt * POSE_WE;
            return lfgst;
        }

        public static float CalDeltTime(POSE_TIME cTime, POSE_TIME cTime1)
        {
            float LfDelt, temp;

            temp = (cTime.M) * 0.001F;
            if (cTime1.S > cTime.S)
            {
                LfDelt = 1.0F * (cTime.S - cTime1.S) - temp;
                LfDelt = -LfDelt;
            }
            else
                LfDelt = 1.0F * (cTime.S - cTime1.S) + temp;
            return LfDelt;
        }

        public class POSE_TIME
        {
            public int S;
            public int M;
            private DateTime now;
            public POSE_TIME()
            {
                S = 0;
                M = 0;
            }
            public POSE_TIME(int a, int b)
            {
                S = a;
                M = b;
            }
            public POSE_TIME(DateTime now)
            {
                this.now = now;
                S = (int)(now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
                M = (int)((now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds - (int)(now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds)) * 1000000);
            }
        }

        
    }

    #endregion
    public enum ColorRenderMode { Grayscale, ArtColor, TrueColor,ArtColorSide,
        RealColor
    }

}
