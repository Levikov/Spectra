using FreeImageAPI;
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
using System.Windows.Controls;

namespace Spectra
{
    class DataOper
    {
        string srcFilePathName = "";
        string srcFileName = "";
        string md5 = "";
        public DataChannel[] dataChannel = new DataChannel[4];
        public DataOperInfo realInfo = new DataOperInfo();

        public struct DataOperInfo
        {
            public int frmS;
            public int frmE;
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
                dtChannel.Columns.Add("FrameId", System.Type.GetType("System.Int32"));
                dtChannel.Columns.Add("Satellite", System.Type.GetType("System.String"));
                dtChannel.Columns.Add("GST", System.Type.GetType("System.Double"));
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
                dtChannel.Columns.Add("ImportId", System.Type.GetType("System.Int32"));
                dtChannel.Columns.Add("Chanel", System.Type.GetType("System.Int32"));
                dtChannel.Columns.Add("MD5", System.Type.GetType("System.String"));
                dtChannel.Columns.Add("GST_US", System.Type.GetType("System.Int64"));
                dtChannel.Columns.Add("Q1", System.Type.GetType("System.Double"));
                dtChannel.Columns.Add("Q2", System.Type.GetType("System.Double"));
                dtChannel.Columns.Add("Q3", System.Type.GetType("System.Double"));
                dtChannel.Columns.Add("Q4", System.Type.GetType("System.Double"));
                frmS = frmE = frmC = 0;
            }

            public void add(int ID, int row, byte[] buf)
            {
                double GST, Lat, Lon, X, Y, Z, Vx, Vy, Vz, Ox, Oy, Oz, Q1, Q2, Q3, Q4;
                long GST_US;

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
                Ox = readI32(buf, 81) / (double)10000 * 57.3;
                Oy = readI32(buf, 85) / (double)10000 * 57.3;
                Oz = readI32(buf, 89) / (double)10000 * 57.3;
                Q1 = readI32(buf, 65) / (double)10000 * 57.3;
                Q2 = readI32(buf, 69) / (double)10000 * 57.3;
                Q3 = readI32(buf, 73) / (double)10000 * 57.3;
                Q4 = readI32(buf, 77) / (double)10000 * 57.3;

                dtChannel.Rows.Add(new object[] { ID, row, buf[6] * 256 + buf[7], "1", GST, Lat, Lon, X, Y, Z, Vx, Vy, Vz, Ox, Oy, Oz, 1, 1, md5, GST_US, Q1, Q2, Q3, Q4 });
            }

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

            public Int32 readI32(byte[] buf, int addr)
            {
                byte[] conv = new byte[4] { buf[addr + 3], buf[addr + 2], buf[addr + 1], buf[addr] };
                return BitConverter.ToInt32(conv, 0);
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
        }

        //构造函数
        public DataOper(string s, string m)
        {
            md5 = m;
            srcFilePathName = s;
            srcFileName = s.Substring(s.LastIndexOf('\\') + 1, s.LastIndexOf('.') - s.LastIndexOf('\\') - 1);
            for (int c = 0; c < 4; c++)
                dataChannel[c] = new DataChannel(md5);
        }

        //主程序
        public async void main(string inFilePathName, ProgressBar prog_Import, TextBox tb_Console)
        {
            try
            {
                IProgress<double> IProg_Bar = new Progress<double>((ProgressValue) => { prog_Import.Value = ProgressValue * prog_Import.Maximum; });
                IProgress<string> IProg_Cmd = new Progress<string>((ProgressString) => { tb_Console.Text = ProgressString + "\n" + tb_Console.Text; });
                string strSrc = await preData(await unpackData(inFilePathName, IProg_Bar, IProg_Cmd), IProg_Bar, IProg_Cmd);
                if (strSrc == string.Empty)
                {
                    IProg_Cmd.Report(DateTime.Now.ToString("HH:mm:ss") + " 文件不正确，已停止该文件操作！");
                    return;
                }
                strSrc = await decData(await splitFile(strSrc, IProg_Bar, IProg_Cmd), IProg_Bar, IProg_Cmd);
                new Thread(() => { sqlInsert(); }).Start();
                await mergeImage(strSrc, IProg_Bar, IProg_Cmd);
                IProg_Cmd.Report(DateTime.Now.ToString("HH:mm:ss") + " 操作完成！");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        //解包
        public Task<string> unpackData(string inFilePathName, IProgress<double> IProg_Bar, IProgress<string> IProg_Cmd)
        {
            return Task.Run(() =>
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
            });
        }

        //预处理
        public Task<string> preData(string inFilePathName, IProgress<double> IProg_Bar, IProgress<string> IProg_Cmd)
        {
            return Task.Run(() =>
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
            });
        }

        //分包并记录
        public Task<string> splitFile(string inFilePathName, IProgress<double> IProg_Bar, IProgress<string> IProg_Cmd)
        {
            return Task.Run(() =>
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
            });
        }

        //获取图像的起止
        public void info()
        {
            for (int c = 0; c < 4; c++)
                dataChannel[c].getInfo();

            realInfo.frmS = Math.Max(Math.Max(dataChannel[0].frmS, dataChannel[1].frmS), Math.Max(dataChannel[2].frmS, dataChannel[3].frmS));
            realInfo.frmE = Math.Min(Math.Min(dataChannel[0].frmE, dataChannel[1].frmE), Math.Min(dataChannel[2].frmE, dataChannel[3].frmE));

            for (int c = 0; c < 4; c++)
                dataChannel[c].modify(realInfo.frmS, realInfo.frmE);
        }

        //解压
        public Task<string> decData(string inFilePathName, IProgress<double> IProg_Bar, IProgress<string> IProg_Cmd)
        {
            return Task.Run(() =>
            {
                Directory.CreateDirectory($"{Environment.CurrentDirectory}\\channelFiles");
                IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 开始解压.");
                IProg_Bar.Report(0);

                Parallel.For(0, 4, c =>
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
                        catch { }

                        if (frm % (dataChannel[c].frmC / 100) == 0 && c == 0)
                            IProg_Bar.Report((double)frm / dataChannel[c].frmC);
                    });
                    if (c == 0)
                        IProg_Bar.Report(1);
                    IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 通道{c}解压完成.");
                    FileStream fs_out_raw = new FileStream($"{Environment.CurrentDirectory}\\channelFiles\\{md5}_{c}.raw", FileMode.Create);
                    for (int frm = 0; frm < dataChannel[c].frmC; frm++)
                        fs_out_raw.Write(channelFile[frm], 0, 512 * 160 * 2);
                    fs_out_raw.Close();
                    IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 通道{c}写文件完成！");
                });
                return $"{Environment.CurrentDirectory}\\channelFiles\\{md5}_";
            });
        }

        //图像拼接
        public Task<int> mergeImage(string inFilePathName, IProgress<double> IProg_Bar, IProgress<string> IProg_Cmd)
        {
            return Task.Run(() =>
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
                IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 合并完成！");
                IProg_Bar.Report(1);
                return 1;
            });
        }

        //数据库操作
        public void sqlInsert()
        {
            SQLiteDatabase sqlExcute = new SQLiteDatabase(Variables.dbPath);
            sqlExcute.ExecuteNonQuery("delete from AuxData where MD5=@MD5",
                new List<SQLiteParameter>()
                    {
                        new SQLiteParameter("MD5",md5)
                    });
            sqlExcute.BeginInsert();
            for (int f = 0; f < dataChannel[0].frmC; f++)
                Insert(sqlExcute, dataChannel[0].dtChannel.Rows[f]);
            sqlExcute.EndInsert();
            updateFileInfo(sqlExcute);
        }

        public void Insert(SQLiteDatabase sqlExcute, DataRow dr)
        {
            var sql = "insert into AuxData values(@FrameId,@SatelliteId,@GST,@Lat,@Lon,@X,@Y,@Z,@Vx,@Vy,@Vz,@Ox,@Oy,@Oz,@ImportId,@Chanel,@MD5,@GST_US,@Q1,@Q2,@Q3,@Q4);";
            var cmdparams = new List<SQLiteParameter>()
                {
                    new SQLiteParameter("FrameId", dr[2]),
                    new SQLiteParameter("SatelliteId",dr[3]),
                    new SQLiteParameter("GST",dr[4]),
                    new SQLiteParameter("Lat",dr[5]),
                    new SQLiteParameter("Lon",dr[6]),
                    new SQLiteParameter("X",dr[7]),
                    new SQLiteParameter("Y",dr[8]),
                    new SQLiteParameter("Z",dr[9]),
                    new SQLiteParameter("Vx",dr[10]),
                    new SQLiteParameter("Vy",dr[11]),
                    new SQLiteParameter("Vz",dr[12]),
                    new SQLiteParameter("Ox",dr[13]),
                    new SQLiteParameter("Oy",dr[14]),
                    new SQLiteParameter("Oz",dr[15]),
                    new SQLiteParameter("ImportId",dr[16]),
                    new SQLiteParameter("Chanel",dr[17]),
                    new SQLiteParameter("MD5",dr[18]),
                    new SQLiteParameter("GST_US",dr[19]),
                    new SQLiteParameter("Q1",dr[20]),
                    new SQLiteParameter("Q2",dr[21]),
                    new SQLiteParameter("Q3",dr[22]),
                    new SQLiteParameter("Q4",dr[23])
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

        public void updateFileInfo(SQLiteDatabase sqlExcute)
        {
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

            SQLiteFunc.ExcuteSQL("update FileDetails_dec set 解压时间='?',解压后文件路径='?',帧数='?',起始时间='?',结束时间='?',起始经纬='?',结束经纬='?' where MD5='?'",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), $"{Environment.CurrentDirectory}\\decFiles\\{FileInfo.md5}\\", FileInfo.frmSum, FileInfo.startTime.ToString("yyyy-MM-dd HH:mm:ss"), FileInfo.endTime.ToString("yyyy-MM-dd HH:mm:ss"), FileInfo.startCoord.convertToString(), FileInfo.endCoord.convertToString(), FileInfo.md5);
            SQLiteFunc.ExcuteSQL("update FileDetails set 是否已解压='是' where MD5='?';", FileInfo.md5);
            FileInfo.decFilePathName = $"{Environment.CurrentDirectory}\\decFiles\\{FileInfo.md5}\\";
            ImageInfo.strFilesPath = FileInfo.decFilePathName;
        }
    }
}
