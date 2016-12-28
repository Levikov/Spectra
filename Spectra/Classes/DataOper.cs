using FreeImageAPI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Drawing.Imaging;
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
        /// 构造函数，获取MD5
        /// </summary>
        /// <param name="m"></param>
        public DataOper(string m)
        {
            md5 = m;
        }

        /// <summary>
        /// 构造函数，获取源文件路径、MD5、文件名
        /// </summary>
        /// <param name="s">源文件路径全称</param>
        /// <param name="m">源文件的MD5</param>
        /// <param name="b">是否为1星，true为1星，false为2星</param>
        public DataOper(string s, string m, string sataID)
        {
            md5 = m;
            srcFilePathName = s;
            srcFileName = s.Substring(s.LastIndexOf('\\') + 1, s.LastIndexOf('.') - s.LastIndexOf('\\') - 1);
            
            for (int c = 0; c < 4; c++)
                dataChannel[c] = new DataChannel(md5, sataID);
        }

        /// <summary>
        /// 数据解压主程序
        /// </summary>
        /// <param name="IProg_Bar">进度条</param>
        /// <param name="IProg_Cmd">控制台</param>
        /// <returns>Task</returns>
        public Task main(IProgress<double> IProg_Bar, IProgress<string> IProg_Cmd)
        {
            return Task.Run(() =>
            {
                //try
                //{
                    //string strSrc = preData(unpackData(srcFilePathName, IProg_Bar, IProg_Cmd), IProg_Bar, IProg_Cmd);
                    string strSrc = srcFilePathName;
                    if (strSrc == string.Empty)
                    {
                        IProg_Cmd.Report(DateTime.Now.ToString("HH:mm:ss") + " 文件不正确，已停止该文件操作！");
                        return;
                    }
                    strSrc = decData(splitFile(strSrc, IProg_Bar, IProg_Cmd), IProg_Bar, IProg_Cmd);
                    new Thread(() => { sqlInsert(Global.dbPath, dataChannel[0].dtChannel, true); }).Start();
                    mergeImage(strSrc, IProg_Bar, IProg_Cmd);
                    //IProg_Cmd.Report(DateTime.Now.ToString("HH:mm:ss") + " 开始清理冗余文件！");
                    //Task.Run(() => { Directory.Delete("tempFiles", true); }).Wait();
                    new Thread(() => { Get3DRaw($"{Global.pathDecFiles}{md5}\\"); }).Start();
                    IProg_Cmd.Report(DateTime.Now.ToString("HH:mm:ss") + " 操作完成！");
                //}
                //catch(Exception ex)
                //{
                //    string str = ex.ToString();
                //}
            });
        }

        /// <summary>
        /// 解包数据，将地面站数据格式解为288/280格式
        /// </summary>
        /// <param name="inFilePathName">源文件全路径名称</param>
        /// <param name="IProg_Bar">进度条</param>
        /// <param name="IProg_Cmd">控制台</param>
        /// <returns>输出文件的全路径名称</returns>
        public string unpackData(string inFilePathName, IProgress<double> IProg_Bar, IProgress<string> IProg_Cmd)
        {
            Directory.CreateDirectory("tempFiles");
            //文件不存在，退出
            if (!File.Exists(inFilePathName))
                return string.Empty;
            FileStream inFileStream = new FileStream(inFilePathName, FileMode.Open, FileAccess.Read, FileShare.Read);
            //文件打开失败，退出
            if (inFileStream == null)
                return string.Empty;
            //文件小于1MB，退出
            if (inFileStream.Length < 1024 * 1024)
                return string.Empty;
            //跳过65536头
            inFileStream.Seek(Global.longJumpLen, SeekOrigin.Begin);
            //要处理的文件长度
            long lenAll = (inFileStream.Length - Global.longJumpLen) / 1040 * 1040;
            //使用1040*1M缓存读文件
            int lenCache = 1040 * 1024 * 1024;
            Byte[] cacheBuf = new Byte[lenCache];
            double cacheSum = Math.Ceiling(lenAll / (double)lenCache);
            //使用844*1M缓存写文件
            int lenWr = 884 * 1024 * 1024;
            Byte[] outBuf = new Byte[lenWr];
            FileStream outFileStream = new FileStream($"tempFiles\\{srcFileName}_u.dat", FileMode.Create);
            //开始解包
            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 开始解包.");
            IProg_Bar.Report(0);
            //除最后一个单元，每次循环处理1040*1M文件
            int invalidCnt = 0;
            for (int cacheCnt = 0; cacheCnt < cacheSum - 1; cacheCnt++)
            {
                invalidCnt = 0;
                inFileStream.Read(cacheBuf, 0, lenCache);
                for (int i = 0; i < lenCache / 1040; i++)
                {
                    if (cacheBuf[i * 1040 + 0] != 0x1A || cacheBuf[i * 1040 + 1] != 0xCF || cacheBuf[i * 1040 + 2] != 0xFC || cacheBuf[i * 1040 + 3] != 0x1D)
                    {
                        invalidCnt++;
                        continue;
                    }
                    if (cacheBuf[i * 1040 + 13] == 0xAA && cacheBuf[i * 1040 + 14] == 0xAA && cacheBuf[i * 1040 + 15] == 0xAA && cacheBuf[i * 1040 + 16] == 0xAA)
                    {
                        invalidCnt++;
                        continue;
                    }
                    Array.Copy(cacheBuf, i * 1040 + 12, outBuf, (i - invalidCnt) * 884, 884);
                }
                outFileStream.Write(outBuf, 0, outBuf.Length - invalidCnt * 884);
                outFileStream.Flush();
                IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} {cacheCnt + 1}G解包完成.");
                IProg_Bar.Report((cacheCnt + 1) / cacheSum);
            }
            //最后一个单元处理
            invalidCnt = 0;
            lenCache = (int)(lenAll % lenCache);
            inFileStream.Read(cacheBuf, 0, lenCache);
            for (int i = 0; i < lenCache / 1040; i++)
            {
                if (cacheBuf[i * 1040 + 13] == 0xAA && cacheBuf[i * 1040 + 14] == 0xAA && cacheBuf[i * 1040 + 15] == 0xAA & cacheBuf[i * 1040 + 16] == 0xAA)
                {
                    invalidCnt++;
                    continue;
                }
                Array.Copy(cacheBuf, i * 1040 + 12, outBuf, i * 884, 884);
            }
            outFileStream.Write(outBuf, 0, lenCache / 1040 * 884 - invalidCnt * 884);
            outFileStream.Close();
            //完成
            IProg_Bar.Report(1);
            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 解包完成！");
            return $"tempFiles\\{srcFileName}_u.dat";
        }

        /// <summary>
        /// 预处理操作，将不是EB905716开头的数据全部剔除，将288长度的数据变为280长度。
        /// </summary>
        /// <param name="inFilePathName">源文件全路径名称</param>
        /// <param name="IProg_Bar">进度条</param>
        /// <param name="IProg_Cmd">控制台</param>
        /// <returns>输出文件的全路径名称</returns>
        public string preData(string inFilePathName, IProgress<double> IProg_Bar, IProgress<string> IProg_Cmd)
        {
            int updateCnt = 0;
            //不存在文件，退出
            if (!File.Exists(inFilePathName))
                return string.Empty;
            FileStream inFileStream = new FileStream(inFilePathName, FileMode.Open, FileAccess.Read, FileShare.Read);
            //文件为空，退出
            if (inFileStream == null)
                return string.Empty;
            //开始预处理
            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 开始预处理.");
            IProg_Bar.Report(0);
            //输出的数据
            Byte[] outBuf = new Byte[280];
            outBuf[0] = 0xEB;   outBuf[1] = 0x90;   outBuf[2] = 0x57;   outBuf[3] = 0x16;
            FileStream outFileStream = new FileStream($"tempFiles\\{srcFileName}_p.dat", FileMode.Create);
            //标识文件位置
            long position = 0;
            //头缓存
            Byte[] headBuf = new Byte[276];

            while (position < inFileStream.Length - 280)
            {
                inFileStream.Read(headBuf, 0, 4);
                position = position + 4;
                if (headBuf[0] == 0xEB && headBuf[1] == 0x90 && headBuf[2] == 0x57 && headBuf[3] == 0x16)
                {
                    inFileStream.Read(headBuf, 0, 276);
                    position = position + 276;
                    Array.Copy(headBuf, 0, outBuf, 4, 276);
                    outFileStream.Write(outBuf, 0, 280);
                    outFileStream.Flush();
                    if(updateCnt++%10000 == 0)
                        IProg_Bar.Report((double)position / inFileStream.Length);
                }
            }
            inFileStream.Close();
            outFileStream.Close();
            IProg_Bar.Report(1);
            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 预处理完成！");

            return $"tempFiles\\{srcFileName}_p.dat";
        }

        /// <summary>
        /// 分包并记录图像开始位置，将文件分为4个通道文件。
        /// </summary>
        /// <param name="inFilePathName">源文件全路径名称</param>
        /// <param name="IProg_Bar">进度条</param>
        /// <param name="IProg_Cmd">控制台</param>
        /// <returns>输出文件的全路径名称(不含尾号)</returns>
        public string splitFile(string inFilePathName, IProgress<double> IProg_Bar, IProgress<string> IProg_Cmd)
        {
            //文件不存在，退出
            if (!File.Exists(inFilePathName))
                return string.Empty;
            //文件为空，退出
            FileStream inFileStream = new FileStream(inFilePathName, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (inFileStream == null)
                return string.Empty;
            //输出文件
            FileStream[] outFileStream = new FileStream[4];
            for (int c = 0; c < 4; c++)
            {
                outFileStream[c] = new FileStream($"tempFiles\\S{srcFileName}_{c}.dat", FileMode.Create, FileAccess.Write, FileShare.Read);
            }
            //开始
            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 开始分包.");
            IProg_Bar.Report(0);
            //包总数
            long packSum = inFileStream.Length / 280;
            Byte[] inBuf = new Byte[280];

            int chan = 0;                   //通道号
            int[] ID = { 0, 0, 0, 0 };      //帧号
            long[] row = { 0, 0, 0, 0 };    //行号
            bool flagStart = false;
            for (long packCnt = 0; packCnt < packSum; packCnt++)
            {
                //读1包数据
                inFileStream.Read(inBuf, 0, 280);
                //得到通道号
                chan = inBuf[5] - 1;
                if (chan > 3 || chan < 0)
                    continue;
                if (inBuf[4] == 0x08)
                {
                    flagStart = true;
                    dataChannel[chan].add(ID[chan], row[chan], inBuf);
                    ID[chan]++;
                    outFileStream[chan].Write(inBuf, 0, 280);
                    outFileStream[chan].Flush();
                    row[chan]++;
                    IProg_Bar.Report((double)(packCnt + 1) / packSum);
                }
                else if(inBuf[4] == 0x0A)
                {
                    if (!flagStart)
                        continue;
                    outFileStream[chan].Write(inBuf, 0, 280);
                    outFileStream[chan].Flush();
                    row[chan]++;
                }
            }
            //关闭文件
            for (int c = 0; c < 4; c++)
            {
                outFileStream[c].Close();
            }
            //修正
            infoTemp();
            //完成
            IProg_Bar.Report(1);
            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 分包完成!");

            return $"tempFiles\\S{srcFileName}_";
        }

        /// <summary>
        /// 将图像的起始帧号确定，并将每个通道进行修正，即删除超出的帧。
        /// </summary>
        public void info()
        {
            //得到最大的帧号起始
            Int32[] frmS = { 0, 0, 0, 0 };
            Int32 max = 0;
            for (int c = 0; c < 4; c++)
            {
                frmS[c] = (int)dataChannel[c].dtChannel.Rows[0]["FrameId"];
                if (frmS[c] > max)
                    max = frmS[c];
            }
            //将多余的开始帧删除
            for (int c = 0; c < 4; c++)
            {
                for(int i=0;i< max - frmS[c]; i++)
                    dataChannel[c].dtChannel.Rows.RemoveAt(0);
            }

            //得到最大的帧总数
            Int32[] frmE = { 0, 0, 0, 0 };
            max = 0;
            for (int c = 0; c < 4; c++)
            {
                frmE[c] = dataChannel[c].dtChannel.Rows.Count;
                if (frmE[c] > max)
                    max = frmE[c];
            }
            //帧总数确定
            for (int c = 0; c < 4; c++)
                dataChannel[c].frmC = max - 1;
            //将多余的尾帧删除
            for (int c = 0; c < 4; c++)
            {
                for (int i = 0; i < max - frmE[c]; i++)
                    dataChannel[c].dtChannel.Rows.RemoveAt(dataChannel[c].dtChannel.Rows.Count - 1);
            }
        }
        public void infoTemp()
        {
            for (int i = 0; i < 8903; i++)
                dataChannel[0].dtChannel.Rows.RemoveAt(0);
            for (int i = 0; i < 430; i++)
                dataChannel[1].dtChannel.Rows.RemoveAt(0);
            for (int i = 0; i < 9173; i++)
                dataChannel[2].dtChannel.Rows.RemoveAt(0);
            for (int i = 0; i < 9229; i++)
                dataChannel[3].dtChannel.Rows.RemoveAt(0);
            for(int i=0;i<31718;i++)
                for (int c = 0; c < 4; c++)
                    dataChannel[c].dtChannel.Rows.RemoveAt(dataChannel[c].dtChannel.Rows.Count-1);
            for (int c = 0; c < 4; c++)
                dataChannel[c].frmC = 8192;
        }

        /// <summary>
        /// 通道串行按帧并行解压文件，解压后文件为4个大文件
        /// </summary>
        /// <param name="inFilePathName">源文件全路径名称</param>
        /// <param name="IProg_Bar">进度条</param>
        /// <param name="IProg_Cmd">控制台</param>
        /// <returns>输出文件的全路径名称(不含尾号)</returns>
        public string decData(string inFilePathName, IProgress<double> IProg_Bar, IProgress<string> IProg_Cmd)
        {
            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 开始解压.");
            IProg_Bar.Report(0);

            for (int c = 0; c < 4; c++)
            {
                FileStream inFileStream = new FileStream($"{inFilePathName}{c}.dat", FileMode.Open, FileAccess.Read, FileShare.Read);
                //如果文件小于2G，则并行执行
                if (inFileStream.Length < 1024)
                {
                    byte[] inBuf = new byte[inFileStream.Length];
                    inFileStream.Read(inBuf, 0, (int)inFileStream.Length);
                    inFileStream.Close();
                    IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 通道{c}读取完成.");

                    byte[][] channelFile = new byte[dataChannel[c].frmC][];
                    Parallel.For(0, dataChannel[c].frmC, frm =>
                    {
                        channelFile[frm] = new byte[512 * 160 * 2];
                        long rowS = (long)dataChannel[c].dtChannel.Rows[frm]["row"];
                        long rowC = (long)dataChannel[c].dtChannel.Rows[frm + 1]["row"] - rowS;
                        if (rowC > 1)
                        {
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
                                errInfo.add(frm, $"{c}通道解压出错");
                            }

                            if (frm % (dataChannel[c].frmC / 100) == 0)
                                IProg_Bar.Report((double)frm / dataChannel[c].frmC);
                        }
                    });
                    IProg_Bar.Report(1);
                    IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 通道{c}解压完成.");
                    FileStream fs_out_raw = new FileStream($"tempFiles\\D{srcFileName}_{c}.raw", FileMode.Create);
                    for (int frm = 0; frm < dataChannel[c].frmC; frm++)
                        fs_out_raw.Write(channelFile[frm], 0, 512 * 160 * 2);
                    fs_out_raw.Close();
                    IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 通道{c}写文件完成！");
                }
                else
                {
                    byte[] channelFile = new byte[512 * 160 * 2];   //解压后图像
                    FileStream fs_out_raw = new FileStream($"tempFiles\\D{srcFileName}_{c}.raw", FileMode.Create);
                    Stream s;
                    FIBITMAP fibmp;
                    inFileStream.Seek((long)dataChannel[c].dtChannel.Rows[0]["row"]*280, SeekOrigin.Begin);
                    for (int frm = 0;frm< dataChannel[c].frmC;frm++)
                    {
                        //行总数
                        int rowC = (int)((long)dataChannel[c].dtChannel.Rows[frm + 1]["row"] - (long)dataChannel[c].dtChannel.Rows[frm]["row"]);
                        if (rowC > 1)
                        {
                            byte[] jp2BufA = new byte[rowC * 280];
                            byte[] jp2BufB = new byte[rowC * 256 - 256 - 16];

                            inFileStream.Read(jp2BufA, 0, rowC * 280);

                            Array.Copy(jp2BufA, 1 * 280 + 32, jp2BufB, 0, 240);
                            for (int r = 2; r < rowC; r++)
                                Array.Copy(jp2BufA, r * 280 + 16, jp2BufB, (r - 2) * 256 + 240, 256);
                            try
                            {
                                s = new MemoryStream(jp2BufB);
                                fibmp = FreeImage.LoadFromStream(s);
                                if (fibmp.IsNull == true)
                                {
                                    continue;
                                }
                                try
                                {
                                    Marshal.Copy(FreeImage.GetBits(fibmp), channelFile, 0, 512 * 160 * 2);
                                    FreeImage.Unload(fibmp);
                                }
                                catch { }
                            }
                            catch
                            {
                                errInfo.add(frm, $"{c}通道解压出错");
                            }
                            fs_out_raw.Write(channelFile, 0, 512 * 160 * 2);
                            IProg_Bar.Report((double)(frm + 1) / dataChannel[c].frmC);
                        }
                    }
                    fs_out_raw.Close();
                    IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 通道{c}解压完成.");
                }
            }
            return $"tempFiles\\D{srcFileName}_";
        }

        /// <summary>
        /// 景娟娟专用数据
        /// </summary>
        public void mergeJJJ()
        {
            Directory.CreateDirectory($"{Global.pathDecFiles}{md5}\\图像处理");
            //4个通道解压前
            FileStream[] inSFile = new FileStream[4];
            for (int c = 0; c < 4; c++)
                inSFile[c] = new FileStream($"tempFiles\\S{srcFileName}_{c}.dat",FileMode.Open,FileAccess.Read,FileShare.Read);
            //4个通道解压后
            FileStream[] inDFile = new FileStream[4];
            for (int c = 0; c < 4; c++)
                inDFile[c] = new FileStream($"tempFiles\\D{srcFileName}_{c}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
            //按照2500为单元切割
            int splitSum = (int)Math.Ceiling((double)dataChannel[0].frmC / 2500);
            int len = 2500;
            long row = 0;
            Byte[] buf = new Byte[1024];
            for (int i = 0; i < splitSum; i++)
            {
                //创建文件
                FileStream outFile = new FileStream($"{Global.pathDecFiles}{md5}\\图像处理\\wn_tuxiang_{i}.raw", FileMode.Create);
                if (i == splitSum - 1)
                    len = dataChannel[0].frmC - i * 2500;
                
                for (int m = 0; m < len; m++)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        row = (long)dataChannel[c].dtChannel.Rows[m]["row"];
                        inSFile[c].Seek(row * 280, SeekOrigin.Begin);   //定位辅助数据的位置
                        inSFile[c].Read(buf, 0, 280);                   //读辅助数据
                        outFile.Write(buf, 0, 1024);                    //写辅助数据
                    }
                    for (int n = 0; n < 160; n++)
                    {
                        for (int c = 0; c < 4; c++)
                        {
                            inDFile[c].Read(buf, 0, 1024);              //读有效数据
                            outFile.Write(buf, 0, 1024);                //写有效数据
                        }
                    }
                }
                outFile.Close();
            }
            for (int c = 0; c < 4; c++)
            {
                inSFile[c].Close();
                inDFile[c].Close();
            }
        }

        /// <summary>
        /// 将图像按照4096为单元进行拼接，每个单元分别生成1张真彩图
        /// </summary>
        /// <param name="inFilePathName">文件存放路径</param>
        /// <param name="IProg_Bar">进度条</param>
        /// <param name="IProg_Cmd">控制台</param>
        public void mergeImage(string inFilePathName, IProgress<double> IProg_Bar, IProgress<string> IProg_Cmd)
        {
            //开始存图像
            Directory.CreateDirectory($"{Global.pathDecFiles}{md5}");
            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 开始图像转存.");
            IProg_Bar.Report(0);
            mergeJJJ();
            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 开始图像合并.");
            //4个通道的文件读
            FileStream[] inFileStream = new FileStream[4];
            for (int c = 0; c < 4; c++)
                inFileStream[c] = new FileStream($"{inFilePathName}{c}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
            //图像分割总数
            int splitSum = (int)Math.Ceiling((double)dataChannel[0].frmC / 4096);

            int imageWidth = 4096;
            for (int i = 0; i < splitSum; i++)
            {
                Directory.CreateDirectory($"{Global.pathDecFiles}{md5}\\{i}");
                byte[][] channelBuf = new byte[4][];
                if (i == splitSum - 1)
                    imageWidth = dataChannel[0].frmC - i * 4096;
                //读4个通道图像
                for (int c = 0; c < 4; c++)
                {
                    channelBuf[c] = new byte[imageWidth * 512 * 160 * 2];
                    inFileStream[c].Read(channelBuf[c], 0, imageWidth * 512 * 160 * 2);
                }
                //保存切割的图像
                byte[][] imgBuf = new byte[160][];
                for (int b = 0; b < 160; b++)
                {
                    imgBuf[b] = new byte[imageWidth * 2048 * 2];
                    Parallel.For(0, 4, c =>
                        Parallel.For(0, imageWidth, f =>
                            Array.Copy(channelBuf[c], f * 512 * 160 * 2 + b * 512 * 2, imgBuf[b], 2048 * 2 * f + c * 512 * 2, 512 * 2)));
                    FileStream outFile = new FileStream($"{Global.pathDecFiles}{md5}\\{i}\\{b}.raw", FileMode.Create);
                    outFile.Write(imgBuf[b], 0, 2048 * imageWidth * 2);
                    outFile.Close();
                }

                //得到要存储的辅助数据表
                DataTable dtExcel = dataChannel[0].dtChannel.Copy();
                dtExcel.Clear();
                for (int j = 0; j < imageWidth; j++)
                    dtExcel.ImportRow(dataChannel[0].dtChannel.Rows[i * 4096 + j]);
                //新建数据表存储
                SQLiteDatabase db = new SQLiteDatabase($"{Global.pathDecFiles}{md5}\\{i}\\数据库_{i}.sqlite");
                db.createTable();
                sqlInsert($"{Global.pathDecFiles}{md5}\\{i}\\数据库_{i}.sqlite", dtExcel, false);
                //移除前2列
                dtExcel.Columns.RemoveAt(0);
                dtExcel.Columns.RemoveAt(0);
                //存储为EXCEL
                ExcelHelper _excelHelper = new ExcelHelper();
                _excelHelper.SaveToText($"{Global.pathDecFiles}{md5}\\{i}\\辅助数据_{i}.xls", dtExcel);
                
                IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} {i}图像合并完成.");
                IProg_Bar.Report((double)(i + 1) / splitSum);
            }
            //关闭文件
            for (int c = 0; c < 4; c++)
            {
                inFileStream[c].Close();
            }
            //生成真彩图
            Bitmap bmp;
            for (int i = 0; i < splitSum; i++)
            {
                int h = 4096;
                if (i == splitSum - 1)
                    h = dataChannel[0].frmC % 4096;
                bmp = BmpOper.MakePseudoColor($"{Global.pathDecFiles}{md5}\\{i}\\", new ushort[] { 39, 76, 126 }, h);
                bmp.RotateFlip(System.Drawing.RotateFlipType.Rotate90FlipX);
                bmp.Save($"{Global.pathDecFiles}{md5}\\{i}\\{i}.bmp");
            }

            IProg_Cmd.Report($"{DateTime.Now.ToString("HH:mm:ss")} 合并完成！");
            IProg_Bar.Report(1);
        }

        /// <summary>
        /// 数据库操作，包括插入辅助数据、插入文件信息、插入快视信息、插入错误信息
        /// </summary>
        public void sqlInsert(string pathDB,DataTable dtDB,Boolean flag)
        {
            SQLiteDatabase sqlExcute = new SQLiteDatabase(pathDB);
            removeData("AuxData", md5, sqlExcute);
            removeData("FileQuickView", md5, sqlExcute);
            sqlExcute.BeginInsert();

            //if (flag)
            //{
            //    removeData("FileErrors", md5, sqlExcute);
            //    errInfo.update(dataChannel[0].dtChannel);
            //    for (int i = 0; i < errInfo.dtErrInfo.Rows.Count; i++)
            //    {
            //        Insert(sqlExcute, errInfo.dtErrInfo.Rows[i]);
            //    }
            //}

            //快视用数据库
            int splitSum = (int)Math.Ceiling((double)dtDB.Rows.Count/4096);
            int splitCur = 4096;
            for (int split = 0; split < splitSum; split++)
            {
                if (split == splitSum - 1)
                    splitCur = dtDB.Rows.Count - 4096 * split;
                DateTime T0 = new DateTime(2012, 1, 1, 8, 0, 0);
                DateTime StartTime = T0.AddSeconds((double)dtDB.Rows[split * 4096]["GST"]);                 //开始时间
                DateTime EndTime = T0.AddSeconds((double)dtDB.Rows[split * 4096 + splitCur - 1]["GST"]);    //结束时间
                Coord StartCoord = new Coord(0,0), EndCoord = new Coord(0,0);
                StartCoord.Lon = (double)dtDB.Rows[split * 4096]["Lon"];                                    //开始经度
                StartCoord.Lat = (double)dtDB.Rows[split * 4096]["Lat"];                                    //开始纬度
                EndCoord.Lon = (double)dtDB.Rows[split * 4096 + splitCur - 1]["Lon"];                       //结束经度
                EndCoord.Lat = (double)dtDB.Rows[split * 4096 + splitCur - 1]["Lat"];                       //结束纬度
                string strStartCoord, strEndCoord;
                strStartCoord = $"({StartCoord.Lat},{StartCoord.Lon})";
                strEndCoord = $"({EndCoord.Lat},{EndCoord.Lon})";
                Insert(sqlExcute,split,splitCur,StartTime,EndTime,strStartCoord,strEndCoord);
            }

            //插入辅助信息
            for (int f = 0; f < dtDB.Rows.Count; f++)
                Insert(sqlExcute, f, dtDB.Rows.Count, dtDB.Rows[f]);
            sqlExcute.EndInsert();
            if (flag)
            {
                //更新文件信息
                updateFileInfo(sqlExcute,dtDB);
            }
        }

        /// <summary>
        /// 从table中移除所有m相关数据
        /// </summary>
        /// <param name="t">表格</param>
        /// <param name="m">MD5</param>
        /// <param name="sqlExcute">数据库对象</param>
        public void removeData(string t, string m, SQLiteDatabase sqlExcute)
        {
            sqlExcute.ExecuteNonQuery($"delete from {t} where MD5=@MD5",
                new List<SQLiteParameter>()
                    {
                        new SQLiteParameter("MD5",m)
                    });
        }

        /// <summary>
        /// 辅助数据插入数据库操作
        /// </summary>
        /// <param name="sqlExcute">数据库对象</param>
        /// <param name="InternalId">内部编号</param>
        /// <param name="FrameSum">总帧数</param>
        /// <param name="dr">辅助数据列表</param>
        private void Insert(SQLiteDatabase sqlExcute, int InternalId, int FrameSum, DataRow dr)
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
            catch { }
        }

        /// <summary>
        /// 快视数据插入数据库
        /// </summary>
        /// <param name="sqlExcute"></param>
        /// <param name="SubId"></param>
        /// <param name="FrameSum"></param>
        /// <param name="StartTime"></param>
        /// <param name="EndTime"></param>
        /// <param name="StartCoord"></param>
        /// <param name="EndCoord"></param>
        private void Insert(SQLiteDatabase sqlExcute, int SubId, int FrameSum, DateTime StartTime,DateTime EndTime,string StartCoord,string EndCoord)
        {
            var sql = "insert into FileQuickView values(@Name,@MD5,@SubId,@FrameSum,@SavePath,@StartTime,@EndTime,@StartCoord,@EndCoord);";
            var cmdparams = new List<SQLiteParameter>()
                {
                    new SQLiteParameter("Name",srcFilePathName.Substring(srcFilePathName.LastIndexOf('\\')+1)),
                    new SQLiteParameter("MD5",md5),
                    new SQLiteParameter("SubId",SubId),
                    new SQLiteParameter("FrameSum",FrameSum),
                    new SQLiteParameter("SavePath",$"{Global.pathDecFiles}{md5}\\{SubId}\\"),
                    new SQLiteParameter("StartTime",StartTime),
                    new SQLiteParameter("EndTime",EndTime),
                    new SQLiteParameter("StartCoord",StartCoord),
                    new SQLiteParameter("EndCoord",EndCoord)
                };

            try
            {
                sqlExcute.ExecuteNonQuery(sql, cmdparams);
            }
            catch { }
        }

        /// <summary>
        /// 将错误信息插入数据库
        /// </summary>
        /// <param name="sqlExcute">数据库对象</param>
        /// <param name="dr">数据行</param>
        private void Insert(SQLiteDatabase sqlExcute, DataRow dr)
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
            catch { }
        }

        /// <summary>
        /// 文件信息插入数据库，将首帧、末帧作为文件图像的起始和结束
        /// </summary>
        /// <param name="sqlExcute">数据库对象</param>
        private void updateFileInfo(SQLiteDatabase sqlExcute, DataTable dtDB)
        {
            //更新文件信息
            FileInfo.frmSum = dtDB.Rows.Count;// (long)sqlExcute.ExecuteScalar($"SELECT COUNT(*) FROM AuxData WHERE MD5='{FileInfo.md5}'");                                    //帧总数
            DateTime T0 = new DateTime(2012, 1, 1, 8, 0, 0);
            FileInfo.startTime = T0.AddSeconds((double)dtDB.Rows[0]["GST"]);//sqlExcute.ExecuteScalar($"SELECT GST FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId ASC"));//起始时间
            FileInfo.endTime = T0.AddSeconds((double)dtDB.Rows[dtDB.Rows.Count-1]["GST"]);//sqlExcute.ExecuteScalar($"SELECT GST FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId DESC")); //结束时间
            double lat = (double)dtDB.Rows[0]["Lat"];//sqlExcute.ExecuteScalar($"SELECT Lat FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId ASC");
            double lon = (double)dtDB.Rows[0]["Lon"];//sqlExcute.ExecuteScalar($"SELECT Lon FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId ASC");
            FileInfo.startCoord.convertToCoord($"({lat},{lon})");                                                                                           //起始经纬
            lat = (double)dtDB.Rows[dtDB.Rows.Count - 1]["Lat"];//sqlExcute.ExecuteScalar($"SELECT Lat FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId DESC");
            lon = (double)dtDB.Rows[dtDB.Rows.Count - 1]["Lon"];//sqlExcute.ExecuteScalar($"SELECT Lon FROM AuxData WHERE MD5='{FileInfo.md5}' ORDER BY FrameId DESC");
            FileInfo.endCoord.convertToCoord($"({lat},{lon})");                                                                                             //结束经纬

            SQLiteFunc.ExcuteSQL("update FileDetails_dec set 解压时间='?',解压后文件路径='?',帧数='?',起始时间='?',结束时间='?',起始经纬='?',结束经纬='?' where MD5='?'",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), $"{Global.pathDecFiles}{md5}\\", FileInfo.frmSum, FileInfo.startTime.ToString("yyyy-MM-dd HH:mm:ss"), FileInfo.endTime.ToString("yyyy-MM-dd HH:mm:ss"), FileInfo.startCoord.convertToString(), FileInfo.endCoord.convertToString(), md5);
            SQLiteFunc.ExcuteSQL("update FileDetails set 是否已解压='是' where MD5='?';", md5);
            ImageInfo.strFilesPath = FileInfo.decFilePathName = $"{Global.pathDecFiles}{md5}\\";
        }

        /// <summary>
        /// 获得3D展示用的4张图
        /// </summary>
        /// <param name="pathDec">解压后文件存放路径</param>
        private void Get3DRaw(string pathDec)
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
    
    /// <summary>
    /// 每个通道的信息，包含列表、帧起始、帧结束、总帧数
    /// </summary>
    public class DataChannel
    {
        public DataTable dtChannel;
        public int frmC;
        public string md5,Satellite;

        /// <summary>
        /// 单通道对象
        /// </summary>
        /// <param name="m">MD5</param>
        /// <param name="s">1星/2星</param>
        public DataChannel(string m,string s)
        {
            md5 = m;
            Satellite = s;
            dtChannel = new DataTable();
            dtChannel.Columns.Add("ID", System.Type.GetType("System.Int32"));   //第几帧图像（0计数）
            dtChannel.Columns.Add("row", System.Type.GetType("System.Int64"));  //在第几行开始（0计数）
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
            frmC = 0;
        }

        public void add(int ID, long row, byte[] buf)
        {
            double GST, Lat, Lon, X, Y, Z, Vx, Vy, Vz, Ox, Oy, Oz, Q1, Q2, Q3, Q4, Freq, Integral;
            long GST_US;
            int StartRow, Gain;

            GST = readU32(buf, 17);
            GST_US = readU32(buf, 104);     //北京时间+8小时
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

            dtChannel.Rows.Add(new object[] { ID, row, buf[6] * 256 + buf[7], GST, GST_US, Lat, Lon, X, Y, Z, Vx, Vy, Vz, Ox, Oy, Oz, Q1, Q2, Q3, Q4, Freq, Integral, StartRow, Gain, md5, Satellite });
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

            clonlat[0] = 180 * clonlat[0] / Math.PI;            //经度
            clonlat[1] = 90 * clonlat[1] / Math.PI / 0.5;       //纬度
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
                case 10000:
                    return 7;
                default:
                    return 8;
            }
        }
    }

    /// <summary>
    /// 错误信息类
    /// </summary>
    public class ErrorInfo
    {
        /// <summary>
        /// 存放错误信息的列表
        /// </summary>
        public DataTable dtErrInfo;

        /// <summary>
        /// 构造函数，确认列表内容
        /// </summary>
        public ErrorInfo()
        {
            dtErrInfo = new DataTable();
            dtErrInfo.Columns.Add("错误位置", System.Type.GetType("System.String"));
            dtErrInfo.Columns.Add("错误类型", System.Type.GetType("System.String"));
        }

        /// <summary>
        /// 列表增加一行内容
        /// </summary>
        /// <param name="frm">帧号</param>
        /// <param name="info">错误信息</param>
        public void add(int frm,string info)
        {
            dtErrInfo.Rows.Add(new object[] {frm.ToString(),info});
        }

        /// <summary>
        /// 对文件图像的帧号进行判断是否连续，不连续则报错
        /// </summary>
        /// <param name="dt">文件图像辅助数据列表</param>
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
                    if (curFrm != rightFrm)
                    {
                        dtErrInfo.Rows.Add(new object[] { rightFrm.ToString(), "丢失" });
                        rightFrm = curFrm;
                    }
                }
            }
        }
    }

    public class DataProc
    {
        [DllImport("DLL\\DataOperation.dll", EntryPoint = "GetRGBFromBand")]
        public static extern void GetRGBFromBand(int band, out double R, out double G, out double B);

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
                if (cMode == ColorRenderMode.RealColor) chanel = 3;

                byte[] buf_full = new byte[Width * Height * 3];
                byte[] buf_band;
                buf_band = new byte[Width * Height * 2 * chanel];

                if (!File.Exists($"{path}{v}.raw") || Height < 1) return null;
                FileStream fs = new FileStream($"{path}{v}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs == null) return null;
                fs.Read(buf_band, 0, Width * Height * 2 * chanel);
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
                                GetRGBFromBand(i / 2048 + 27, out fR, out fG, out fB);
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
                                double R = (double)(readU16_PIC(buf_band, i * 6 + 4)) / 4096 * 256;
                                double G = (double)(readU16_PIC(buf_band, i * 6 + 2)) / 4096 * 256;
                                double B = (double)(readU16_PIC(buf_band, i * 6)) / 4096 * 256;

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
                                GetRGBFromBand(i % Width + 27, out fR, out fG, out fB);
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
                r[0] = await BmpOper.MakePseudoColor(path, new UInt16[] { 40, 77, 127 }, 4, ImageInfo.imgWidth);
                r[1] = await BmpOper.MakePseudoColor(path, new UInt16[] { 40, 40, 40 }, 4, ImageInfo.imgWidth);
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
        public static Task<DataTable> QueryResult(string md5, bool isChecked1, bool isChecked2, bool isChecked3, DateTime start_time, DateTime end_time, long start_FrmCnt, long end_FrmCnt, Coord coord_TL, Coord coord_DR)
        {
            return Task.Run(() =>
            {
                SQLiteConnection conn = new SQLiteConnection(Global.dbConString);
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
                    DateTime T0 = new DateTime(2012, 1, 1, 8, 0, 0);
                    TimeSpan ts_Start = selectedStartDate.Subtract(T0);
                    TimeSpan ts_End = selectedEndDate.Subtract(T0);
                    command += " AND GST>=" + (ts_Start.TotalSeconds.ToString()) + " AND GST<=" + ts_End.TotalSeconds.ToString();
                }
                if ((bool)isChecked3)
                {
                    command += " AND FrameId>=" + start_FrmCnt.ToString() + " AND FrameId<=" + end_FrmCnt.ToString();
                }

                SQLiteDatabase db = new SQLiteDatabase(Global.dbPath);
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
            byte[] conv = new byte[2] { buf_row[addr], buf_row[addr + 1] };
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
}
