using FreeImageAPI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spectra
{
    /// <summary>
    /// 数据解压类
    /// </summary>
    public class DataOper
    {
        /// <summary>
        /// 源文件路径全称
        /// </summary>
        public string srcFilePathName = "";

        /// <summary>
        /// 源文件名（不含后缀）
        /// </summary>
        public string srcFileName = "";

        /// <summary>
        /// 源文件MD5码
        /// </summary>
        public string md5 = "";

        /// <summary>
        /// 每个通道的数据信息，主要包含了所有数据的列表清单以及辅助数据的计算
        /// </summary>
        public DataChannel[] dataChannel = new DataChannel[4];

        /// <summary>
        /// 该文件所有错误信息列表
        /// </summary>
        public ErrorInfo errInfo = new ErrorInfo();

        /// <summary>
        /// 构造函数，获取源文件路径、文件名、MD5
        /// </summary>
        /// <param name="s">源文件路径全称</param>
        /// <param name="m">源文件的MD5</param>
        public DataOper(string s, string m)
        {
            md5 = m;
            srcFilePathName = s;
            srcFileName = s.Substring(s.LastIndexOf('\\') + 1, s.LastIndexOf('.') - s.LastIndexOf('\\') - 1);
            for (int c = 0; c < 4; c++)
                dataChannel[c] = new DataChannel(md5);
        }

        /// <summary>
        /// 数据解压主程序
        /// </summary>
        /// <param name="IProg_Bar"></param>
        /// <param name="IProg_Cmd"></param>
        /// <returns></returns>
        public Task main(IProgress<double> IProg_Bar, IProgress<string> IProg_Cmd)
        {
            return Task.Run(() =>
            {
                try
                {
                    string strSrc = preData(unpackData(srcFilePathName, IProg_Bar, IProg_Cmd), IProg_Bar, IProg_Cmd);
                    if (strSrc == string.Empty)
                    {
                        IProg_Cmd.Report(DateTime.Now.ToString("HH:mm:ss") + " 文件不正确，已停止该文件操作！");
                        return;
                    }
                    strSrc = decData(splitFile(strSrc, IProg_Bar, IProg_Cmd), IProg_Bar, IProg_Cmd);
                    new Thread(() => { sqlInsert(); }).Start();
                    mergeImage(strSrc, IProg_Bar, IProg_Cmd);
                    IProg_Cmd.Report(DateTime.Now.ToString("HH:mm:ss") + " 开始清理冗余文件！");
                    Task.Run(() => { Directory.Delete($"{Environment.CurrentDirectory}\\channelFiles", true); }).Wait();
                    Task.Run(() => { Directory.Delete($"{Environment.CurrentDirectory}\\srcFiles", true); }).Wait();
                    new Thread(() => { Get3DRaw($"{Environment.CurrentDirectory}\\decFiles\\{md5}\\"); }).Start();
                    IProg_Cmd.Report(DateTime.Now.ToString("HH:mm:ss") + " 操作完成！");
                }
                catch// (Exception ex)
                {
                    //MessageBox.Show(ex.ToString());
                }
            });
        }

        //解包
        public string unpackData(string inFilePathName, IProgress<double> IProg_Bar, IProgress<string> IProg_Cmd)
        {
            Directory.CreateDirectory($"{Environment.CurrentDirectory}\\srcFiles");
            if (!File.Exists(inFilePathName))
                return string.Empty;
            FileStream inFileStream = new FileStream(inFilePathName, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (inFileStream == null)
                return string.Empty;
            byte[] inBuf = new byte[1024];
            inFileStream.Read(inBuf, 0, 1024);
            if (inBuf[0] == 0x1A && inBuf[1] == 0xCF && inBuf[2] == 0xFC && inBuf[3] == 0x1D)
            {
                IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 开始解包.");
                IProg_Bar.Report(0);

                long rowCnt = inFileStream.Length / 1024;
                byte[] outBuf = new byte[rowCnt * 884];
                inBuf = new byte[inFileStream.Length];

                inFileStream.Read(inBuf, 0, Convert.ToInt32(inFileStream.Length));
                inFileStream.Close();

                IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 文件读取完成.");
                for (int i = 0; i < rowCnt; i++)
                {
                    Array.Copy(inBuf, i * 1024 + 12, outBuf, i * 884, 884);
                    if (i % (rowCnt / 10) == 0)
                        IProg_Bar.Report((double)i / rowCnt);
                }

                IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 数据开始写入文件.");
                FileStream outFileStream = new FileStream($"{Environment.CurrentDirectory}\\srcFiles\\{srcFileName}_u.dat", FileMode.Create);
                outFileStream.Write(outBuf, 0, outBuf.Length);
                outFileStream.Close();

                IProg_Bar.Report(1);
                IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 解包完成！");
                return $"{Environment.CurrentDirectory}\\srcFiles\\{srcFileName}_u.dat";
            }
            else
            {
                inFileStream.Close();
                return inFilePathName;
            }
        }

        //预处理
        public string preData(string inFilePathName, IProgress<double> IProg_Bar, IProgress<string> IProg_Cmd)
        {
            Directory.CreateDirectory($"{Environment.CurrentDirectory}\\srcFiles");
            if (!File.Exists(inFilePathName))
                return string.Empty;
            FileStream inFileStream = new FileStream(inFilePathName, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (inFileStream == null)
                return string.Empty;
            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 开始预处理.");
            IProg_Bar.Report(0);
            byte[] inBuf = new byte[inFileStream.Length];
            inFileStream.Read(inBuf, 0, inBuf.Length);
            inFileStream.Close();
            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 文件读取完成.");

            byte[] outBuf = new byte[inBuf.Length];
            int position = 0, packCnt = 0;
            while (position < inBuf.Length - 4)
            {
                if (inBuf[position] == 0xEB && inBuf[position + 1] == 0x90 && inBuf[position + 2] == 0x57 && inBuf[position + 3] == 0x16)
                {
                    if (inBuf.Length - position < 280)
                        break;
                    Array.Copy(inBuf, position, outBuf, 280 * packCnt, 280);
                    position += 280;
                    packCnt++;
                }
                else
                    position++;
            }

            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 文件开始写入.");
            FileStream outFileStream = new FileStream($"{Environment.CurrentDirectory}\\srcFiles\\{srcFileName}_p.dat", FileMode.Create);
            outFileStream.Write(outBuf, 0, packCnt * 280);
            outFileStream.Close();
            IProg_Bar.Report(1);
            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 预处理完成！");

            return $"{Environment.CurrentDirectory}\\srcFiles\\{srcFileName}_p.dat";
        }

        //分包并记录
        public string splitFile(string inFilePathName, IProgress<double> IProg_Bar, IProgress<string> IProg_Cmd)
        { 
            Directory.CreateDirectory($"{Environment.CurrentDirectory}\\srcFiles");
            if (!File.Exists(inFilePathName))
                return string.Empty;
            FileStream inFileStream = new FileStream(inFilePathName, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (inFileStream == null)
                return string.Empty;

            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 开始分包.");
            IProg_Bar.Report(0);
            byte[] inBuf = new byte[inFileStream.Length];
            inFileStream.Read(inBuf, 0, inBuf.Length);
            int packSum = (int)(inFileStream.Length / 280);
            inFileStream.Close();
            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 文件读取完成.");

            FileStream[] outFileStream = new FileStream[4];

            //Parallel.For(0,4,c=> {
            for (int c = 0; c < 4; c++)
            {
                int row = 0, ID = 0;
                byte[] buf = new byte[280];
                outFileStream[c] = new FileStream($"{Environment.CurrentDirectory}\\srcFiles\\{md5}_{c}.dat", FileMode.Create, FileAccess.Write, FileShare.Read);
                for (int i = 0; i < packSum; i++)
                {
                    if (inBuf[i * 280 + 5] == c + 1)
                    {
                        Array.Copy(inBuf, i * 280, buf, 0, 280);
                        if (inBuf[i * 280 + 4] == 0x08)
                        {
                            dataChannel[c].add(ID, row, buf);
                            ID++;
                        }
                        outFileStream[c].Write(buf, 0, 280);
                        row++;
                    }
                    if ((i + 1) % (packSum / 2) == 0)
                        IProg_Bar.Report((double)(i + 1) / packSum + 0.25 * c);
                }
                outFileStream[c].Close();
            }
            IProg_Bar.Report(1);
            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 分包完成!");
            info();

            return $"{Environment.CurrentDirectory}\\srcFiles\\{md5}_";
        }

        //获取图像的起止
        public void info()
        {
            for (int c = 0; c < 4; c++)
                dataChannel[c].getInfo();

            int S = Math.Max(Math.Max(dataChannel[0].frmS, dataChannel[1].frmS), Math.Max(dataChannel[2].frmS, dataChannel[3].frmS));
            int E = Math.Min(Math.Min(dataChannel[0].frmE, dataChannel[1].frmE), Math.Min(dataChannel[2].frmE, dataChannel[3].frmE));

            for (int c = 0; c < 4; c++)
                dataChannel[c].modify(S, E);
        }

        //解压
        public string decData(string inFilePathName, IProgress<double> IProg_Bar, IProgress<string> IProg_Cmd)
        {
            Directory.CreateDirectory($"{Environment.CurrentDirectory}\\channelFiles");
            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 开始解压.");
            IProg_Bar.Report(0);

            //Parallel.For(0, 4, c =>
            for (int c = 0; c < 4; c++)
            {
                FileStream inFileStream = new FileStream($"{inFilePathName}{c}.dat", FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] inBuf = new byte[inFileStream.Length];
                inFileStream.Read(inBuf, 0, (int)inFileStream.Length);
                inFileStream.Close();
                IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 通道{c}读取完成.");

                byte[][] channelFile = new byte[dataChannel[c].frmC][];
                //for (int frm = 0; frm < dataChannel[c].frmC; frm++)
                Parallel.For(0, dataChannel[c].frmC, frm =>
                {
                    channelFile[frm] = new byte[512 * 160 * 2];
                    int rowS = (int)dataChannel[c].dtChannel.Rows[frm]["row"];
                    int rowC = (int)dataChannel[c].dtChannel.Rows[frm + 1]["row"] - rowS;
                    byte[] jp2BufA = new byte[rowC * 280];
                    byte[] jp2BufB = new byte[rowC * 256 - 256 - 16];
                    Array.Copy(inBuf, rowS * 280, jp2BufA, 0, jp2BufA.Length);

                    Array.Copy(jp2BufA, 1 * 280 + 32, jp2BufB, 0, 240);
                    for (int r = 2; r < rowC; r++)
                        Array.Copy(jp2BufA, r * 280 + 16, jp2BufB, (r - 2) * 256 + 240, 256);
                    try
                    {
                        Stream s = new MemoryStream(jp2BufB);
                        FIBITMAP fibmp = FreeImage.LoadFromStream(s);
                        Marshal.Copy(FreeImage.GetBits(fibmp), channelFile[frm], 0, 512 * 160 * 2);
                        FreeImage.Unload(fibmp);
                    }
                    catch
                    {
                        errInfo.add(frm,"解压出错");
                    }

                    //if (frm % (dataChannel[c].frmC / 100) == 0 && c == 0)
                    if (frm % (dataChannel[c].frmC / 100) == 0)
                        IProg_Bar.Report((double)frm / dataChannel[c].frmC);
                });
                //if (c == 0)
                    IProg_Bar.Report(1);
                IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 通道{c}解压完成.");
                FileStream fs_out_raw = new FileStream($"{Environment.CurrentDirectory}\\channelFiles\\{md5}_{c}.raw", FileMode.Create);
                for (int frm = 0; frm < dataChannel[c].frmC; frm++)
                    fs_out_raw.Write(channelFile[frm], 0, 512 * 160 * 2);
                fs_out_raw.Close();
                IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 通道{c}写文件完成！");
            }
            return $"{Environment.CurrentDirectory}\\channelFiles\\{md5}_";
        }

        //图像拼接
        public int mergeImage(string inFilePathName, IProgress<double> IProg_Bar, IProgress<string> IProg_Cmd)
        {
            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 开始图像合并.");
            IProg_Bar.Report(0);
            Directory.CreateDirectory($"{Environment.CurrentDirectory}\\decFiles\\{md5}");
            FileStream[] inFileStream = new FileStream[4];
            for (int c = 0; c < 4; c++)
                inFileStream[c] = new FileStream($"{inFilePathName}{c}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
            int splitSum = (int)Math.Ceiling((double)dataChannel[0].frmC / 4096);
            int imageWidth = 4096;
            for (int i = 0; i < splitSum; i++)
            {
                Directory.CreateDirectory($"{Environment.CurrentDirectory}\\decFiles\\{md5}\\{i}");
                byte[][] channelBuf = new byte[4][];
                if (i == splitSum - 1)
                    imageWidth = dataChannel[0].frmC % 4096;
                for (int c = 0; c < 4; c++)
                {
                    channelBuf[c] = new byte[imageWidth * 512 * 160 * 2];
                    inFileStream[c].Read(channelBuf[c], 0, imageWidth * 512 * 160 * 2);
                }
                byte[][] imgBuf = new byte[160][];
                for (int b = 0; b < 160; b++)
                //Parallel.For(0, 160, b =>
                {
                    imgBuf[b] = new byte[imageWidth * 2048 * 2];
                    //for (int c = 0; c < 4; c++)
                    Parallel.For(0, 4, c =>
                        //for (int f = 0; f < imageWidth; f++)
                        Parallel.For(0, imageWidth, f =>
                            Array.Copy(channelBuf[c], f * 512 * 160 * 2 + b * 512 * 2, imgBuf[b], 2048 * 2 * f + c * 512 * 2, 512 * 2)));
                    FileStream outFile = new FileStream($"{Environment.CurrentDirectory}\\decFiles\\{md5}\\{i}\\{b}.raw", FileMode.Create);
                    outFile.Write(imgBuf[b], 0, 2048 * imageWidth * 2);
                    outFile.Close();
                }
                IProg_Bar.Report((double)(i + 1) / splitSum);
            }
            for (int c = 0; c < 4; c++)
            {
                inFileStream[c].Close();
            }

            Bitmap bmp;
            int cnt = (int)Math.Ceiling(dataChannel[0].frmC / (double)4096);
            for (int i = 0; i < cnt; i++)
            {
                int h = 4096;
                if (i == cnt - 1)
                    h = dataChannel[0].frmC % 4096;
                bmp = BmpOper.MakePseudoColor($"{Environment.CurrentDirectory}\\decFiles\\{md5}\\{i}\\", new ushort[] { 39, 76, 126 }, h);
                bmp.RotateFlip(System.Drawing.RotateFlipType.Rotate90FlipX);
                bmp.Save($"{Environment.CurrentDirectory}\\decFiles\\{md5}\\{i}.bmp");
            }

            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 合并完成！");
            IProg_Bar.Report(1);
            return 1;
        }

        //数据库操作
        public void sqlInsert()
        {
            SQLiteDatabase sqlExcute = new SQLiteDatabase(Global.dbPath);
            sqlExcute.ExecuteNonQuery("delete from AuxData where MD5=@MD5",
                new List<SQLiteParameter>()
                    {
                        new SQLiteParameter("MD5",md5)
                    });
            sqlExcute.BeginInsert();

            //errInfo.update(dataChannel[0].dtChannel);
            //for (int i = 0; i < errInfo.dtErrInfo.Rows.Count; i++)
            //{
            //    Insert(sqlExcute, errInfo.dtErrInfo.Rows[i]);
            //}

            //快视用数据库
            int splitSum = (int)Math.Ceiling((double)dataChannel[0].frmC/4096);
            int splitCur = 4096;
            for (int split = 0; split < splitSum; split++)
            {
                if (split == splitSum - 1)
                    splitCur = dataChannel[0].frmC % 4096;
                DateTime T0 = new DateTime(2012, 1, 1, 0, 0, 0);
                DateTime StartTime = T0.AddSeconds((double)dataChannel[0].dtChannel.Rows[split * 4096]["GST"]);
                DateTime EndTime = T0.AddSeconds((double)dataChannel[0].dtChannel.Rows[split * 4096 + splitCur - 1]["GST"]);
                Coord StartCoord = new Coord(0,0), EndCoord = new Coord(0,0);
                StartCoord.Lon = (double)dataChannel[0].dtChannel.Rows[split * 4096]["Lon"];
                StartCoord.Lat = (double)dataChannel[0].dtChannel.Rows[split * 4096]["Lat"];
                EndCoord.Lon = (double)dataChannel[0].dtChannel.Rows[split * 4096 + splitCur - 1]["Lon"];
                EndCoord.Lat = (double)dataChannel[0].dtChannel.Rows[split * 4096 + splitCur - 1]["Lat"];
                string strStartCoord, strEndCoord;
                strStartCoord = $"({StartCoord.Lat},{StartCoord.Lon})";
                strEndCoord = $"({EndCoord.Lat},{EndCoord.Lon})";
                Insert(sqlExcute,split,splitCur,StartTime,EndTime,strStartCoord,strEndCoord);
            }


            for (int f = 0; f < dataChannel[0].frmC; f++)
                Insert(sqlExcute, f, dataChannel[0].frmC, dataChannel[0].dtChannel.Rows[f]);
            sqlExcute.EndInsert();
            updateFileInfo(sqlExcute);
        }

        //插入数据库操作
        public void Insert(SQLiteDatabase sqlExcute, int InternalId, int FrameSum, DataRow dr)
        {
            var sql = "insert into AuxData values(@InternalId,@FrameSum,@FrameId,@GST,@GST_US,@Lat,@Lon,@X,@Y,@Z,@Vx,@Vy,@Vz,@Ox,@Oy,@Oz,@Q1,@Q2,@Q3,@Q4,@Freq,@Integral,@StartRow,@Gain,@MD5,@Satellite);";
            var cmdparams = new List<SQLiteParameter>()
                {
                    new SQLiteParameter("InternalId", InternalId),
                    new SQLiteParameter("FrameSum", FrameSum),
                    new SQLiteParameter("FrameId", dr["FrameId"]),
                    new SQLiteParameter("GST",dr["GST"]),
                    new SQLiteParameter("GST_US",dr["GST_US"]),
                    new SQLiteParameter("Lat",dr["Lat"]),
                    new SQLiteParameter("Lon",dr["Lon"]),
                    new SQLiteParameter("X",dr["X"]),
                    new SQLiteParameter("Y",dr["Y"]),
                    new SQLiteParameter("Z",dr["Z"]),
                    new SQLiteParameter("Vx",dr["Vx"]),
                    new SQLiteParameter("Vy",dr["Vy"]),
                    new SQLiteParameter("Vz",dr["Vz"]),
                    new SQLiteParameter("Ox",dr["Ox"]),
                    new SQLiteParameter("Oy",dr["Oy"]),
                    new SQLiteParameter("Oz",dr["Oz"]),
                    new SQLiteParameter("Q1",dr["Q1"]),
                    new SQLiteParameter("Q2",dr["Q2"]),
                    new SQLiteParameter("Q3",dr["Q3"]),
                    new SQLiteParameter("Q4",dr["Q4"]),
                    new SQLiteParameter("Freq",dr["Freq"]),
                    new SQLiteParameter("Integral",dr["Integral"]),
                    new SQLiteParameter("StartRow",dr["StartRow"]),
                    new SQLiteParameter("Gain",dr["Gain"]),
                    new SQLiteParameter("MD5",dr["MD5"]),
                    new SQLiteParameter("Satellite",dr["Satellite"])
                };

            try
            {
                sqlExcute.ExecuteNonQuery(sql, cmdparams);
            }
            catch// (Exception e)
            {
                //MessageBox.Show(e.ToString());
            }
        }

        public void Insert(SQLiteDatabase sqlExcute, int SubId, int FrameSum, DateTime StartTime,DateTime EndTime,string StartCoord,string EndCoord)
        {
            var sql = "insert into FileQuickView values(@Name,@MD5,@SubId,@FrameSum,@SavePath,@StartTime,@EndTime,@StartCoord,@EndCoord);";
            var cmdparams = new List<SQLiteParameter>()
                {
                    new SQLiteParameter("Name",srcFilePathName.Substring(srcFilePathName.LastIndexOf('\\')+1)),
                    new SQLiteParameter("MD5",md5),
                    new SQLiteParameter("SubId",SubId),
                    new SQLiteParameter("FrameSum",FrameSum),
                    new SQLiteParameter("SavePath",$"{Environment.CurrentDirectory}\\decFiles\\{md5}\\{SubId}"),
                    new SQLiteParameter("StartTime",StartTime),
                    new SQLiteParameter("EndTime",EndTime),
                    new SQLiteParameter("StartCoord",StartCoord),
                    new SQLiteParameter("EndCoord",EndCoord)
                };

            try
            {
                sqlExcute.ExecuteNonQuery(sql, cmdparams);
            }
            catch// (Exception e)
            {
                //MessageBox.Show(e.ToString());
            }
        }

        public void Insert(SQLiteDatabase sqlExcute, DataRow dr)
        {
            var sql = "insert into FileErrors values(@错误位置,@错误类型,@MD5);";
            var cmdparams = new List<SQLiteParameter>()
                {
                    new SQLiteParameter("错误位置",dr["错误位置"]),
                    new SQLiteParameter("错误类型",dr["错误类型"]),
                    new SQLiteParameter("MD5",md5)
                };

            try
            {
                sqlExcute.ExecuteNonQuery(sql, cmdparams);
            }
            catch// (Exception e)
            {
                //MessageBox.Show(e.ToString());
            }
        }

        //刷新文件信息
        public void updateFileInfo(SQLiteDatabase sqlExcute)
        {
            sqlExcute.cnn.Open();

            //更新文件信息
            FileInfo.frmSum = dataChannel[0].frmC;// (long)sqlExcute.ExecuteScalar($"SELECT COUNT(*) FROM AuxData WHERE MD5='{FileInfo.md5}'");                                    //帧总数
            DateTime T0 = new DateTime(2012, 1, 1, 0, 0, 0);
            FileInfo.startTime = T0.AddSeconds((double)dataChannel[0].dtChannel.Rows[0]["GST"]);//sqlExcute.ExecuteScalar($"SELECT GST FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId ASC"));//起始时间
            FileInfo.endTime = T0.AddSeconds((double)dataChannel[0].dtChannel.Rows[dataChannel[0].frmC-1]["GST"]);//sqlExcute.ExecuteScalar($"SELECT GST FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId DESC")); //结束时间
            double lat = (double)dataChannel[0].dtChannel.Rows[0]["Lat"];//sqlExcute.ExecuteScalar($"SELECT Lat FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId ASC");
            double lon = (double)dataChannel[0].dtChannel.Rows[0]["Lon"];//sqlExcute.ExecuteScalar($"SELECT Lon FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId ASC");
            FileInfo.startCoord.convertToCoord($"({lat},{lon})");                                                                                           //起始经纬
            lat = (double)dataChannel[0].dtChannel.Rows[dataChannel[0].frmC - 1]["Lat"];//sqlExcute.ExecuteScalar($"SELECT Lat FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId DESC");
            lon = (double)dataChannel[0].dtChannel.Rows[dataChannel[0].frmC - 1]["Lon"];//sqlExcute.ExecuteScalar($"SELECT Lon FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId DESC");
            FileInfo.endCoord.convertToCoord($"({lat},{lon})");                                                                                             //结束经纬

            SQLiteFunc.ExcuteSQL("update FileDetails_dec set 解压时间='?',解压后文件路径='?',帧数='?',起始时间='?',结束时间='?',起始经纬='?',结束经纬='?' where MD5='?'",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), $"{Environment.CurrentDirectory}\\decFiles\\{FileInfo.md5}\\", FileInfo.frmSum, FileInfo.startTime.ToString("yyyy-MM-dd HH:mm:ss"), FileInfo.endTime.ToString("yyyy-MM-dd HH:mm:ss"), FileInfo.startCoord.convertToString(), FileInfo.endCoord.convertToString(), FileInfo.md5);
            SQLiteFunc.ExcuteSQL("update FileDetails set 是否已解压='是' where MD5='?';", FileInfo.md5);
            FileInfo.decFilePathName = $"{Environment.CurrentDirectory}\\decFiles\\{FileInfo.md5}\\";
            ImageInfo.strFilesPath = FileInfo.decFilePathName;
        }

        //获得3D展示用的4张图
        public void Get3DRaw(string pathDec)
        {
            int width = 2048, height = dataChannel[0].frmC;
            byte[] buf3dUp = new byte[width * 128 * 2];
            byte[] buf3dDown = new byte[width * 128 * 2];
            byte[] buf3dLeft = new byte[height * 128 * 2];
            byte[] buf3dRight = new byte[height * 128 * 2];
            int splitSum = (int)Math.Ceiling((double)height / 4096);
            int splitHeight = 4096;
            for (int i = 0; i < splitSum; i++)
            {
                if (i == splitSum - 1)
                    splitHeight = height % 4096;
                for (int b = 27; b < 155; b++)
                {
                    FileStream inFileStream = new FileStream($"{pathDec}{i}\\{b}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
                    byte[] bufImage = new byte[width * splitHeight * 2];
                    inFileStream.Read(bufImage, 0, width * splitHeight * 2);
                    inFileStream.Close();
                    if (i == 0)
                    {
                        Array.Copy(bufImage, 0, buf3dUp, (b - 27) * 4096, 2048 * 2);
                    }
                    if (i == splitSum - 1)
                    {
                        Array.Copy(bufImage, (splitHeight - 1) * 4096, buf3dDown, (b - 27) * 4096, 2048 * 2);
                    }
                    for (int row = 0; row < splitHeight; row++)
                    {
                        Array.Copy(bufImage, row * 4096, buf3dLeft, (row + i*4096) * 128 * 2 + (b - 27) * 2, 2);
                        Array.Copy(bufImage, (row + 1) * 4096 - 2, buf3dRight, (row + i * 4096) * 128 * 2 + (b - 27) * 2, 2);
                    }
                }
            }

            FileStream outFileStream = new FileStream($"{pathDec}161.raw", FileMode.Create);
            outFileStream.Write(buf3dUp, 0, buf3dUp.Length);
            outFileStream.Close();
            outFileStream = new FileStream($"{pathDec}162.raw", FileMode.Create);
            outFileStream.Write(buf3dDown, 0, buf3dDown.Length);
            outFileStream.Close();
            outFileStream = new FileStream($"{pathDec}163.raw", FileMode.Create);
            outFileStream.Write(buf3dLeft, 0, buf3dLeft.Length);
            outFileStream.Close();
            outFileStream = new FileStream($"{pathDec}164.raw", FileMode.Create);
            outFileStream.Write(buf3dRight, 0, buf3dRight.Length);
            outFileStream.Close();
        }
    }
    
    //每个通道的信息
    public class DataChannel
    {
        public DataTable dtChannel;
        public int frmS, frmE, frmC;
        public string md5;

        public DataChannel(string m)
        {
            md5 = m;
            dtChannel = new DataTable();
            dtChannel.Columns.Add("ID", System.Type.GetType("System.Int32"));   //第几帧图像（0计数）
            dtChannel.Columns.Add("row", System.Type.GetType("System.Int32"));  //在第几行开始（0计数）
                                                                                //dtChannel.Columns.Add("InternalId", System.Type.GetType("System.Int32"));
                                                                                //dtChannel.Columns.Add("FrameSum", System.Type.GetType("System.Int32"));
            dtChannel.Columns.Add("FrameId", System.Type.GetType("System.Int32"));
            dtChannel.Columns.Add("GST", System.Type.GetType("System.Double"));
            dtChannel.Columns.Add("GST_US", System.Type.GetType("System.Int64"));
            dtChannel.Columns.Add("Lat", System.Type.GetType("System.Double"));
            dtChannel.Columns.Add("Lon", System.Type.GetType("System.Double"));
            dtChannel.Columns.Add("X", System.Type.GetType("System.Double"));
            dtChannel.Columns.Add("Y", System.Type.GetType("System.Double"));
            dtChannel.Columns.Add("Z", System.Type.GetType("System.Double"));
            dtChannel.Columns.Add("Vx", System.Type.GetType("System.Double"));
            dtChannel.Columns.Add("Vy", System.Type.GetType("System.Double"));
            dtChannel.Columns.Add("Vz", System.Type.GetType("System.Double"));
            dtChannel.Columns.Add("Ox", System.Type.GetType("System.Double"));
            dtChannel.Columns.Add("Oy", System.Type.GetType("System.Double"));
            dtChannel.Columns.Add("Oz", System.Type.GetType("System.Double"));
            dtChannel.Columns.Add("Q1", System.Type.GetType("System.Double"));
            dtChannel.Columns.Add("Q2", System.Type.GetType("System.Double"));
            dtChannel.Columns.Add("Q3", System.Type.GetType("System.Double"));
            dtChannel.Columns.Add("Q4", System.Type.GetType("System.Double"));
            dtChannel.Columns.Add("Freq", System.Type.GetType("System.Double"));
            dtChannel.Columns.Add("Integral", System.Type.GetType("System.Double"));
            dtChannel.Columns.Add("StartRow", System.Type.GetType("System.Int32"));
            dtChannel.Columns.Add("Gain", System.Type.GetType("System.Int32"));
            dtChannel.Columns.Add("MD5", System.Type.GetType("System.String"));
            dtChannel.Columns.Add("Satellite", System.Type.GetType("System.String"));
            frmS = frmE = frmC = 0;
        }

        public void add(int ID, int row, byte[] buf)
        {
            double GST, Lat, Lon, X, Y, Z, Vx, Vy, Vz, Ox, Oy, Oz, Q1, Q2, Q3, Q4, Freq, Integral;
            long GST_US;
            int StartRow, Gain;

            GST = readU32(buf, 17);
            GST_US = readU32(buf, 104);
            X = readI32(buf, 32) / (double)1000;
            Y = readI32(buf, 36) / (double)1000;
            Z = readI32(buf, 40) / (double)1000;
            double[] latlon = new double[2];
            latlon = CalEarthLonLat(new double[3] { X, Y, Z }, GST);
            Lat = double.IsNaN(latlon[1]) ? 0 : latlon[1];
            Lon = double.IsNaN(latlon[0]) ? 0 : latlon[0];

            Vx = readI32(buf, 44) / (double)1000;
            Vy = readI32(buf, 48) / (double)1000;
            Vz = readI32(buf, 52) / (double)1000;
            Ox = readI32(buf, 81) * 57.3;
            Oy = readI32(buf, 85) * 57.3;
            Oz = readI32(buf, 89) * 57.3;
            Q1 = readI32(buf, 65) / (double)10000 * 57.3;
            Q2 = readI32(buf, 69) / (double)10000 * 57.3;
            Q3 = readI32(buf, 73) / (double)10000 * 57.3;
            Q4 = readI32(buf, 77) / (double)10000 * 57.3;

            Freq = (double)100000 / (buf[102] * 256 + buf[103]);
            Integral = IntegralToLevel(buf[108] * 256 + buf[109]);
            StartRow = buf[112] * 256 + buf[113];
            Gain = buf[115];

            dtChannel.Rows.Add(new object[] { ID, row, buf[6] * 256 + buf[7], GST, GST_US, Lat, Lon, X, Y, Z, Vx, Vy, Vz, Ox, Oy, Oz, Q1, Q2, Q3, Q4, Freq, Integral, StartRow, Gain, md5, "1" });
        }

        /// <summary>
        /// 获取首帧帧号、末帧帧号及总帧数
        /// </summary>
        public void getInfo()
        {
            frmS = (int)dtChannel.Rows[0]["FrameId"];
            frmE = (int)dtChannel.Rows[dtChannel.Rows.Count - 1]["FrameId"];
            frmC = dtChannel.Rows.Count;
        }

        public void modify(int s, int e)
        {
            for (int i = 0; i < frmE - e; i++)
            {
                dtChannel.Rows.RemoveAt(dtChannel.Rows.Count - 1);
            }
            for (int i = 0; i < s - frmS; i++)
            {
                dtChannel.Rows.RemoveAt(0);
            }
            frmS = s;
            frmE = e;
            frmC = dtChannel.Rows.Count - 1;
        }

        public UInt32 readU32(byte[] buf, int addr)
        {
            byte[] conv = new byte[4] { buf[addr + 3], buf[addr + 2], buf[addr + 1], buf[addr] };
            return BitConverter.ToUInt32(conv, 0);
        }

        public float readI32(byte[] buf, int addr)
        {
            byte[] mathBuf = new byte[4];
            mathBuf[0] = buf[addr + 3];
            mathBuf[1] = buf[addr + 2];
            mathBuf[2] = buf[addr + 1];
            mathBuf[3] = buf[addr + 0];
            return BitConverter.ToSingle(mathBuf, 0);
            //byte[] conv = new byte[4] { buf[addr + 3], buf[addr + 2], buf[addr + 1], buf[addr] };
            //return BitConverter.ToInt32(conv, 0);
        }

        public static double[] CalEarthLonLat(double[] cuR, double fgst)
        {
            double[] clonlat = new double[2];
            double temp, sra, lon;
            temp = Math.Sqrt(cuR[0] * cuR[0] + cuR[1] * cuR[1]);             //|R|
            if (Math.Abs(temp) < 0.0000001F)                                //R==0
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

        public int IntegralToLevel(int integral)
        {
            switch (integral)
            {
                case 50:
                    return 0;
                case 100:
                    return 1;
                case 200:
                    return 2;
                case 300:
                    return 3;
                case 400:
                    return 4;
                case 500:
                    return 5;
                case 600:
                    return 6;
                case 1000:
                    return 7;
                default:
                    return 8;
            }
        }
    }

    //错误信息
    public class ErrorInfo
    {
        public DataTable dtErrInfo;

        public ErrorInfo()
        {
            dtErrInfo = new DataTable();
            dtErrInfo.Columns.Add("错误位置", System.Type.GetType("System.String"));
            dtErrInfo.Columns.Add("错误类型", System.Type.GetType("System.String"));
        }

        public void add(int frm,string info)
        {
            dtErrInfo.Rows.Add(new object[] {frm.ToString(),info});
        }

        public void update(DataTable dt)
        {
            int cnt = dt.Rows.Count;
            if (cnt < 1)
                return;

            int curFrm = 0,rightFrm = 0;
            for (int i = 0; i < cnt; i++)
            {
                curFrm = Convert.ToInt32(dt.Rows[i]["FrameID"]);
                if (i == 0)
                    rightFrm = curFrm;
                else
                {
                    if (rightFrm == 65535)
                        rightFrm = 0;
                    else
                        rightFrm++;
                    if(curFrm!= rightFrm)
                        dtErrInfo.Rows.Add(new object[] { rightFrm.ToString(), "丢失" });
                }
            }
        }
    }
}
