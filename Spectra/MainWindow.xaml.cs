using Microsoft.Win32;
using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;

namespace Spectra
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>



    public partial class MainWindow : Window
    {
        /*构造函数+系统初始化*/
        SelectedDatesCollection sds;
        public MainWindow()
        {
            InitializeComponent();
            sds = new SelectedDatesCollection(calendarFile);
            calendarFile.SelectedDates.AddRange((DateTime.Now.Date).AddDays(-100), (DateTime.Now.Date).AddDays(-1));
        }

        #region 界面控制
        /*窗体加载时显示默认值*/
        private void GroupBox_Loaded(object sender, RoutedEventArgs e)
        {
            getDefaultShow();
            getApplyModel();
            setWindowID(WinShowInfo.WindowsCnt);
            setScreenID(Screen.AllScreens.Length);
        }
        /*拖动界面*/
        private void WindowMain_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DragMove();
        }
        /*程序退出*/
        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }
        /*程序最小化*/
        private void btnMin_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        /*给datagrid添加行头*/
        private void LoadingRowHeader(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = e.Row.GetIndex() + 1;
        }
        /*选择日期后日历消失*/
        private void calStart_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            Calendar cal = sender as Calendar;
            cal.Visibility = Visibility.Collapsed;
        }
        //文本框获得焦点显示日历
        private void DateGotFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox txt = sender as System.Windows.Controls.TextBox;
            switch (txt.Name)
            {
                case "dtp_Start":
                    calStart.Visibility = Visibility.Visible;
                    calEnd.Visibility = Visibility.Collapsed;
                    break;
                case "dtp_End":
                    calStart.Visibility = Visibility.Collapsed;
                    calEnd.Visibility = Visibility.Visible;
                    break;
                default:
                    break;
            }
        }
        #endregion

        #region 数据解包
        /*点击打开文件*/
        private async void b_Open_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFile = new Microsoft.Win32.OpenFileDialog();
            openFile.Filter = "All Files(*.*)|*.*";
            if ((bool)openFile.ShowDialog())
            {
                IProgress<DataView> IProg_DataView = new Progress<DataView>((Prog_DataView) => { dataGrid_Errors.ItemsSource = Prog_DataView; });
                IProgress<double> IProgress_Prog = new Progress<double>((ProgressValue) => { prog_Import.Value = ProgressValue * this.prog_Import.Maximum; });
                IProgress<string> IProgress_List = new Progress<string>((ProgressString) => { this.tb_Console.Text = ProgressString + "\n" + this.tb_Console.Text; });
                FileInfo.srcFilePathName = openFile.FileName;
                this.tb_Path.Text = openFile.FileName;
                int i=await DataProc.GetFileDetail(FileInfo.srcFilePathName,IProg_DataView,IProgress_Prog,IProgress_List);
                this.b_Start_Import.IsEnabled = true;
            }
        }
        #endregion

        #region 数据解压
        /*用于放弃操作*/
        private CancellationTokenSource cancelImport = new CancellationTokenSource();
        /*点击导入*/
        private async void b_Start_Import_Click(object sender, RoutedEventArgs e)
        {
            if (FileInfo.isDecomp)
                if (System.Windows.MessageBox.Show("该文件已解压,是否要重新解压并覆盖?", "提示", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.Cancel)
                    return;
            System.Windows.Controls.Button b = sender as System.Windows.Controls.Button;
            IProgress<double> IProgress_Prog = new Progress<double>((ProgressValue) => { prog_Import.Value = ProgressValue * this.prog_Import.Maximum; });
            IProgress<string> IProgress_List = new Progress<string>((ProgressString) => { this.tb_Console.Text = ProgressString + "\n"+ this.tb_Console.Text; });
            this.b_Abort_Import.IsEnabled = true;
            this.b_Start_Import.IsEnabled = false;
            this.b_Open_Import.IsEnabled = false;
            string result = await DataProc.Import_5(IProgress_Prog, IProgress_List, cancelImport.Token);
            System.Windows.MessageBox.Show(result);
            SQLiteFunc.ExcuteSQL("update decFileDetails set 解压时间='?',解压后文件路径='?' where 文件路径='?'",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),Variables.str_pathWork,FileInfo.srcFilePathName);
            SQLiteFunc.ExcuteSQL("update FileDetails set 是否已解压='是' where 文件路径='?';",FileInfo.srcFilePathName);
            this.b_Start_Import.IsEnabled = false;
            this.b_Abort_Import.IsEnabled = false;
            this.b_Open_Import.IsEnabled = true;
            this.prog_Import.Value = 0;
        }

        private void b_Abort_Import_Click(object sender, RoutedEventArgs e)
        {
            cancelImport.Cancel();
        }
        #endregion

        #region 文件检索
        /*查找文件*/
        private void textSelectFile_TextChanged(object sender, TextChangedEventArgs e)
        {
            dataGrid_srcFile.ItemsSource = SQLiteFunc.SelectDTSQL("SELECT * from FileDetails where 文件路径 like '%"+textSelectFile.Text+"%'").DefaultView;
        }
        /*选中文件后显示解压情况*/
        private void dataGrid_srcFile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var sel = (DataRowView)dataGrid_srcFile.SelectedItem;
            if(sel != null)
                dataGrid_decFile.ItemsSource = SQLiteFunc.SelectDTSQL("SELECT * from decFileDetails where 文件路径='" + sel.Row[1] + "'").DefaultView;
        }
        /*删除文件记录*/
        private void btnDelRecord_Click(object sender, RoutedEventArgs e)
        {
            var sel = (DataRowView)dataGrid_srcFile.SelectedItem;
            if (sel != null)
            {
                if (System.Windows.MessageBox.Show("确认删除该文件的记录?", "提示", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.Cancel) return;
                SQLiteFunc.ExcuteSQL("delete from FileDetails where 文件路径='" + sel.Row[1] + "'");
                SQLiteFunc.ExcuteSQL("delete from decFileDetails where 文件路径='" + sel.Row[1] + "'");
                SQLiteFunc.ExcuteSQL("delete from FileErrors where 文件路径='" + sel.Row[1] + "'");
                dataGrid_srcFile.ItemsSource = SQLiteFunc.SelectDTSQL("SELECT * from FileDetails").DefaultView;
            }
        }
        #endregion
        
        #region 图像检索
        public DateTime start_time;                     //起始检索时刻
        public DateTime end_time;                       //终止检索时刻
        public Coord Coord_TL = new Coord(0, 0);        //左上角坐标
        public Coord Coord_DR = new Coord(0, 0);        //右下角坐标
        public long start_FrmCnt;                       //起始帧号
        public long end_FrmCnt;                         //终止帧号
        /*点击图像检索*/
        private async void b_Query_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DataTable dt = await DataProc.QueryResult((bool)cb_byTime.IsChecked, (bool)this.cb_byCoord.IsChecked, (bool)this.cb_byFrmCnt.IsChecked, start_time, end_time, start_FrmCnt, end_FrmCnt, Coord_TL, Coord_DR);
                dataGrid_Result.ItemsSource = dt.DefaultView;
                dataGrid_SatePose.ItemsSource = dt.DefaultView;
                DataQuery.QueryResult = dt;
                ImageInfo.GetImgInfo(dt);       /*存储图像信息*/
                SetImgInfo();
            }
            catch (Exception E)
            {
                System.Windows.MessageBox.Show(E.Message);
            }
        }
        /*选择时间条件*/
        private void dtp_TextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox dp = sender as System.Windows.Controls.TextBox;
            switch (dp.Name)
            {
                case "dtp_Start": { start_time = Convert.ToDateTime(dtp_Start.Text); } break;
                case "dtp_End": { end_time = Convert.ToDateTime(dtp_End.Text); } break;
                default: break;
            }
        }
        /*帧号条件*/
        private void tb_frm_TextChanged(object sender, TextChangedEventArgs e)
        {
            long.TryParse(this.tb_start_frm.Text, out start_FrmCnt);
            long.TryParse(this.tb_end_frm.Text, out end_FrmCnt);
        }
        /*选择经纬度范围*/
        private void Coord_TextChanged(object sender, TextChangedEventArgs e)
        {
            double tl_lat = 0, tl_lon = 0, rb_lat = 0, rb_lon = 0;
            System.Windows.Controls.TextBox dp = sender as System.Windows.Controls.TextBox;
            switch (dp.Name)
            {
                case "LT_Lat": { double.TryParse(((System.Windows.Controls.TextBox)sender).Text, out tl_lat); Coord_TL.Lat = tl_lat; } break;
                case "LT_Lon": { double.TryParse(((System.Windows.Controls.TextBox)sender).Text, out tl_lon); Coord_TL.Lon = tl_lon; } break;
                case "RB_Lat": { double.TryParse(((System.Windows.Controls.TextBox)sender).Text, out rb_lat); Coord_DR.Lat = rb_lat; } break;
                case "RB_Lon": { double.TryParse(((System.Windows.Controls.TextBox)sender).Text, out rb_lon); Coord_DR.Lon = rb_lon; } break;
                default: break;
            }
        }
        #endregion

        #region 图像提取
        /*显示图像按钮*/
        private void button_Display_Click(object sender, RoutedEventArgs e)
        {
            //DataTable dt = DataQuery.QueryResult;
            App.global_Win_Map = new MapWindow();
            App.global_Win_Map.Show();
            App.global_Win_Map.DrawRectangle(new Point((double)DataQuery.QueryResult.Rows[0].ItemArray[3], (double)DataQuery.QueryResult.Rows[0].ItemArray[4]), new Point((double)DataQuery.QueryResult.Rows[DataQuery.QueryResult.Rows.Count - 1].ItemArray[3], (double)DataQuery.QueryResult.Rows[DataQuery.QueryResult.Rows.Count - 1].ItemArray[4]));
            initMultiFuncWindows();
        }
        /*清除datagrid*/
        private void button_Clear_Result_Click(object sender, RoutedEventArgs e)
        {
            DataQuery.QueryResult.Clear();
            dataGrid_Result.ItemsSource = null;
        }
        #endregion

        #region 图像信息
        /*显示图像信息*/
        public void SetImgInfo()
        {
            lblImgMinFrm.Content = ImageInfo.minFrm.ToString();
            lblImgMaxFrm.Content = ImageInfo.maxFrm.ToString();
            lblImgStartTime.Content = ImageInfo.startTime.ToString();
            lblImgEndTime.Content = ImageInfo.endTime.ToString();
            lblImgWidth.Content = ImageInfo.imgWidth.ToString();
            lblStartLon.Content = ImageInfo.startCoord.Lon.ToString("F2");
            lblStartLat.Content = ImageInfo.startCoord.Lat.ToString("F2");
            lblEndLon.Content = ImageInfo.endCoord.Lon.ToString("F2");
            lblEndLat.Content = ImageInfo.endCoord.Lat.ToString("F2");
        }
        #endregion

        #region 默认显示方式
        /*初始化显示窗体*/
        private void initMultiFuncWindows()
        {
            for (int i = 0; i < WinShowInfo.WindowsCnt; i++)
                App.global_Windows.Add(new MultiFuncWindow());
            int[] subCnt = new int[WinShowInfo.WindowsCnt];
            subCnt[0] = Convert.ToUInt16(WinShowInfo.dtWinShowInfo.Rows[0][3]);
            int p = subCnt[0];
            for (int i = 1; i < WinShowInfo.WindowsCnt; i++)
            {
                subCnt[i] = Convert.ToUInt16(WinShowInfo.dtWinShowInfo.Rows[p][3]);
                p += subCnt[i];
            }
            MultiFuncWindow[] w = new MultiFuncWindow[WinShowInfo.WindowsCnt];
            for (int i = 0; i < WinShowInfo.WindowsCnt; i++)
            {
                w[i] = (MultiFuncWindow)App.global_Windows[i];
                w[i].DisplayMode = (GridMode)(subCnt[i] - 1);
            }
            foreach (DataRow dr in WinShowInfo.dtWinShowInfo.Rows)
            {
                if(!w[Convert.ToUInt16(dr[0]) - 1].isShow)
                    w[Convert.ToUInt16(dr[0]) - 1].ScreenShow(Screen.AllScreens,0, Convert.ToUInt16(dr[0]).ToString());
                w[Convert.ToUInt16(dr[0]) - 1].Refresh(Convert.ToUInt16(dr[4])-1,(WinFunc)Convert.ToUInt16(dr[6]));
            }
        }
        /*设置窗口编号有哪些值可选*/
        private void setWindowID(int cnt)
        {
            cmbScreenWinID1.Items.Clear();
            for (int i = 0; i < cnt; i++)
                cmbScreenWinID1.Items.Add(i + 1);
            cmbScreenWinID1.SelectedIndex = 0;
            cmbScreenWinID2.Items.Clear();
            for (int i = 0; i < cnt; i++)
                cmbScreenWinID2.Items.Add(i + 1);
            cmbScreenWinID2.SelectedIndex = 0;
        }
        /*设置显示器编号有哪些值可选*/
        private void setScreenID(int cnt)
        {
            cmbScreenSID.Items.Clear();
            cmbModelSID.Items.Clear();
            for (int i = 0; i < cnt; i++)
            {
                cmbScreenSID.Items.Add(i + 1);
                cmbModelSID.Items.Add(i + 1);
            }
            cmbScreenSID.SelectedIndex = 0;
            cmbModelSID.SelectedIndex = 0;
        }
        /*获取数据库内容*/
        private void getDefaultShow()
        {
            WinShowInfo.dtWinShowInfo = SQLiteFunc.SelectDTSQL("select * from WinShowInfo order by 窗口编号,子窗体编号");
            dataGrid_Screen.ItemsSource = WinShowInfo.dtWinShowInfo.DefaultView;
            if (WinShowInfo.dtWinShowInfo.Rows.Count != 0)
                WinShowInfo.WindowsCnt = Convert.ToUInt16(WinShowInfo.dtWinShowInfo.Rows[0][1]);
            cmbScreenWindowsCnt.SelectedIndex = WinShowInfo.WindowsCnt - 1;
        }
        /*获得已有的应用样式*/
        private void getApplyModel()
        {
            ModelShowInfo.dtModelList = SQLiteFunc.SelectDTSQL("select * from Apply_Model order by 名称");
            dataGrid_ApplyModel.ItemsSource = ModelShowInfo.dtModelList.DefaultView;
            dataGrid_ApplyModel.SelectedIndex = 0;
        }
        /*设置窗口数量*/
        private void button_btnScreenWinCnt_Click(object sender, RoutedEventArgs e)
        {
            if (WinShowInfo.WindowsCnt < cmbScreenWindowsCnt.SelectedIndex + 1)
            {
                for (int i = WinShowInfo.WindowsCnt; i < cmbScreenWindowsCnt.SelectedIndex + 1; i++)
                    SQLiteFunc.ExcuteSQL("insert into WinShowInfo (窗口编号,窗口数量,显示器编号,子窗体数量,子窗体编号,窗体类型,窗体类型编号) values (?,?,?,?,?,?,?)", i + 1, cmbScreenWindowsCnt.SelectedIndex + 1, 1, 1, 1, "'图像'",0);
            }
            else if(WinShowInfo.WindowsCnt > cmbScreenWindowsCnt.SelectedIndex + 1)
            {
                for (int i = cmbScreenWindowsCnt.SelectedIndex + 1; i < WinShowInfo.WindowsCnt; i++)
                    SQLiteFunc.ExcuteSQL("delete from WinShowInfo where 窗口编号=?;", i + 1);
            }
            SQLiteFunc.ExcuteSQL("update WinShowInfo set 窗口数量=? where 窗口数量!=?;", cmbScreenWindowsCnt.SelectedIndex + 1, cmbScreenWindowsCnt.SelectedIndex + 1);
            getDefaultShow();
            setWindowID(WinShowInfo.WindowsCnt);
        }
        /*设置窗口属性*/
        private void button_btnScreenWinPro_Click(object sender, RoutedEventArgs e)
        {
            string sql = "select * from WinShowInfo where 窗口编号=" + (cmbScreenWinID1.SelectedIndex + 1);
            DataTable dtSubWin = SQLiteFunc.SelectDTSQL(sql);
            if (dtSubWin.Rows.Count < cmbSubWinCnt.SelectedIndex + 1)
            {
                for (int i = dtSubWin.Rows.Count; i < cmbSubWinCnt.SelectedIndex + 1; i++)
                    SQLiteFunc.ExcuteSQL("insert into WinShowInfo (窗口编号,窗口数量,显示器编号,子窗体数量,子窗体编号,窗体类型,窗体类型编号) values (?,?,?,?,?,?,?)", cmbScreenWinID1.SelectedIndex + 1, WinShowInfo.WindowsCnt, 1, cmbSubWinCnt.SelectedIndex + 1, i+1, "'图像'",0);
            }
            else if (dtSubWin.Rows.Count > cmbSubWinCnt.SelectedIndex + 1)
            {
                for (int i = cmbSubWinCnt.SelectedIndex + 1; i < dtSubWin.Rows.Count; i++)
                    SQLiteFunc.ExcuteSQL("delete from WinShowInfo where 窗口编号=? and 子窗体编号=?;",cmbScreenWinID1.SelectedIndex + 1, i + 1);
            }
            SQLiteFunc.ExcuteSQL("update WinShowInfo set 显示器编号=?,子窗体数量=? where 窗口编号=?;", cmbScreenSID.SelectedIndex+1, cmbSubWinCnt.SelectedIndex+1, cmbScreenWinID1.SelectedIndex + 1);
            getDefaultShow();
        }
        /*设置窗口类型*/
        private void button_btnScreenWinType_Click(object sender, RoutedEventArgs e)
        {
            switch (cmbScreenWinType.SelectedIndex)
            {
                case 0:
                    SQLiteFunc.ExcuteSQL("update WinShowInfo set 窗体类型=?,窗体类型编号=? where 窗口编号=? and 子窗体编号=?;", "'图像'", cmbScreenWinType.SelectedIndex, cmbScreenWinID2.SelectedIndex + 1, cmbScreenSubID.SelectedIndex + 1);
                    break;
                case 1:
                    SQLiteFunc.ExcuteSQL("update WinShowInfo set 窗体类型=?,窗体类型编号=? where 窗口编号=? and 子窗体编号=?;", "'曲线'", cmbScreenWinType.SelectedIndex, cmbScreenWinID2.SelectedIndex + 1, cmbScreenSubID.SelectedIndex + 1);
                    break;
                case 2:
                    SQLiteFunc.ExcuteSQL("update WinShowInfo set 窗体类型=?,窗体类型编号=? where 窗口编号=? and 子窗体编号=?;", "'立方体'", cmbScreenWinType.SelectedIndex, cmbScreenWinID2.SelectedIndex + 1, cmbScreenSubID.SelectedIndex + 1);
                    break;
                case 3:
                    SQLiteFunc.ExcuteSQL("update WinShowInfo set 窗体类型=?,窗体类型编号=? where 窗口编号=? and 子窗体编号=?;", "'地图'", cmbScreenWinType.SelectedIndex, cmbScreenWinID2.SelectedIndex + 1, cmbScreenSubID.SelectedIndex + 1);
                    break;
                default:
                    break;
            }
            getDefaultShow();
        }
        #endregion

        /*获取异常信息*/
        private void btnAbnGet_Click(object sender, RoutedEventArgs e)
        {
            for(int i=1;i<=160;i++)
                GetImgBuf(i);
        }
        
        public void GetImgBuf(int v)
        {
            byte[] buf_full = new byte[2048 * ImageInfo.imgWidth * 2];
            Parallel.For(0, ImageInfo.imgWidth, (i) =>
            {
                byte[] buf_rgb = new byte[2048 * 2];
                Parallel.For(1, 5, k =>
                {
                    FileStream fs = new FileStream($"{Variables.str_pathWork}\\{(long)(DataQuery.QueryResult.Rows[i].ItemArray[14])}_{(long)(DataQuery.QueryResult.Rows[i].ItemArray[0])}_{k}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);

                    byte[] temp = new byte[512 * 2];
                    fs.Seek(v * 512 * 2, SeekOrigin.Begin);
                    fs.Read(temp, 0, 1024);
                    Array.Copy(temp, 0, buf_rgb, 2 * 512 * (k - 1), 2 * 512);
                    fs.Close();
                });
                Array.Copy(buf_rgb, 0, buf_full, 2 * 2048 * i, 2 * 2048);
            });

            //byte[] buf_rgb = new byte[2048 * 2];
            //for (int i = 0; i < ImageInfo.imgWidth; i++)
            //{
            //    for (int k = 1; k < 5; k++)
            //    {
            //        FileStream fs = new FileStream($"{Variables.str_pathWork}\\{(long)(DataQuery.QueryResult.Rows[i].ItemArray[14])}_{(long)(DataQuery.QueryResult.Rows[i].ItemArray[0])}_{k}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);

            //        byte[] temp = new byte[512 * 2];
            //        fs.Seek(v * 512 * 2, SeekOrigin.Begin);
            //        fs.Read(temp, 0, 1024);
            //        Array.Copy(temp, 0, buf_rgb, 2 * 512 * (k - 1), 2 * 512);
            //        fs.Close();
            //    }
            //    Array.Copy(buf_rgb, 0, buf_full, 2 * 2048 * i, 2 * 2048);
            //}

            //高低位取反
            //for (int i = 0; i < 2048 * ImageInfo.imgWidth; i++)
            //{
            //    byte t = buf_full[i * 2];
            //    buf_full[i * 2] = buf_full[i * 2 + 1];
            //    buf_full[i * 2 + 1] = t;
            //}
            FileStream fTest = new FileStream("E:\\Test\\"+ v.ToString() +".raw", FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            fTest.Write(buf_full, 0, 2048 * ImageInfo.imgWidth * 2);
            fTest.Close();

            //显示8位图
            //for (int i = 0; i < 2048 * ImageInfo.imgWidth; i++)
            //{
            //    buf_full[i] = buf_full[i * 2 + 1];
            //}
            //fTest = new FileStream("E:\\1_8.raw", FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            //fTest.Write(buf_full, 0, 2048 * ImageInfo.imgWidth);
            //fTest.Close();
        }

        private void btnMapUpdate_Click(object sender, RoutedEventArgs e)
        {
            string[] line = File.ReadAllLines(@"E:\Map\Amap.html");
            switch (cmbMapType.SelectedIndex)
            {
                case 0:
                    File.WriteAllText(@"E:\Map\Amap.html", File.ReadAllText(@"E:\Map\Amap.html").Replace(line[94], $"	  var strURL = \"D:/Amap/roadmap/\" + zoom + \"/\" + tile_x+ \"/\" + tile_y + \".png\";"));
                    break;
                case 1:
                    File.WriteAllText(@"E:\Map\Amap.html", File.ReadAllText(@"E:\Map\Amap.html").Replace(line[94], $"	  var strURL = \"D:/Gmap/satellite/\" + zoom + \"/\" + tile_x+ \"/\" + tile_y + \".jpg\";"));
                    break;
                default:
                    break;
            }
            App.global_Win_Map.webMap.Refresh();
        }

        #region 光谱显示
        /*显示单张图像*/
        private void btnShowSpeImg_Click(object sender, RoutedEventArgs e)
        {
            if (App.global_Win_SpecImg == null)
            {
                App.global_Win_SpecImg = new MultiFuncWindow();
                App.global_Win_SpecImg.DisplayMode = GridMode.One;
            }
            if(!App.global_Win_SpecImg.isShow)
                App.global_Win_SpecImg.ScreenShow(Screen.AllScreens,0, "光谱图像");
            App.global_Win_SpecImg.Refresh(0,WinFunc.Image);
        }
        /*显示光谱立方体*/
        private void btnShow3D_Click(object sender, RoutedEventArgs e)
        {
            if (App.global_Win_3D == null)
            {
                App.global_Win_3D = new MultiFuncWindow();
                App.global_Win_3D.DisplayMode = GridMode.One;
            }
            if (!App.global_Win_3D.isShow)
                App.global_Win_3D.ScreenShow(Screen.AllScreens, 0, "光谱立方体");
            App.global_Win_3D.Refresh(0, WinFunc.Cube);
        }
        /*显示典型谱段对比*/
        private void btnShowImgCompare_Click(object sender, RoutedEventArgs e)
        {
            if (App.global_Win_ImgCompare == null)
            {
                App.global_Win_ImgCompare = new MultiFuncWindow();
                App.global_Win_ImgCompare.DisplayMode = GridMode.Four;
            }
            if (!App.global_Win_ImgCompare.isShow)
                App.global_Win_ImgCompare.ScreenShow(Screen.AllScreens, 0, "典型谱段图像对比");
            App.global_Win_ImgCompare.Refresh(0, WinFunc.Image);
            App.global_Win_ImgCompare.Refresh(1, WinFunc.Image);
            App.global_Win_ImgCompare.Refresh(2, WinFunc.Image);
            App.global_Win_ImgCompare.Refresh(3, WinFunc.Image);
        }
        /*设置对比图像谱段*/
        private void btnCompareSet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ckb1sub.IsChecked == true)
                    ((Ctrl_ImageView)(App.global_Win_ImgCompare.UserControls[0])).Refresh(Convert.ToUInt16(txtCompareR.Text),ColorRenderMode.Grayscale);
                if (ckb2sub.IsChecked == true)
                    ((Ctrl_ImageView)(App.global_Win_ImgCompare.UserControls[1])).Refresh(Convert.ToUInt16(txtCompareGray2.Text), ColorRenderMode.Grayscale);
                if (ckb3sub.IsChecked == true)
                    ((Ctrl_ImageView)(App.global_Win_ImgCompare.UserControls[2])).Refresh(Convert.ToUInt16(txtCompareGray3.Text), ColorRenderMode.Grayscale);
                if (ckb4sub.IsChecked == true)
                    ((Ctrl_ImageView)(App.global_Win_ImgCompare.UserControls[3])).Refresh(Convert.ToUInt16(txtCompareGray4.Text), ColorRenderMode.Grayscale);
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show("窗体未初始化！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        #endregion

        #region 应用样式
        /*选中datagrid的应用样式*/
        private void dataGrid_ApplyModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var sel = (DataRowView)dataGrid_ApplyModel.SelectedItem;
            if (sel != null)
            {
                ModelShowInfo.WindowsCnt = Convert.ToUInt16(sel.Row[1]);
                ModelShowInfo.dtWinShowInfo = SQLiteFunc.SelectDTSQL("SELECT * from Apply_ModelR where 名称='" + sel.Row[0] + "' order by 窗口编号,子窗体编号");
                txtModelName.Text = sel.Row[0].ToString();                                                                          /*名称*/
                cmbModelWinCnt.SelectedIndex = Convert.ToUInt16(sel.Row[1]) - 1;                                                    /*窗口数量*/
                txtModelStartTime.Text = (sel.Row[2].ToString() == "") ? "" : Convert.ToDateTime(sel.Row[2]).ToString();            /*起始时间*/
                txtModelEndTime.Text = (sel.Row[3].ToString() == "") ? "" : Convert.ToDateTime(sel.Row[3]).ToString();              /*结束时间*/
                txtModelStartLon.Text = (sel.Row[4].ToString() == "") ? "" : sel.Row[4].ToString();                                 /*起始经度*/
                txtModelEndLon.Text = (sel.Row[5].ToString() == "") ? "" : sel.Row[5].ToString();                                   /*结束经度*/
                txtModelStartLat.Text = (sel.Row[6].ToString() == "") ? "" : sel.Row[6].ToString();                                 /*起始纬度*/
                txtModelEndLat.Text = (sel.Row[7].ToString() == "") ? "" : sel.Row[7].ToString();                                   /*结束纬度*/
                txtModelRemark.Text = sel.Row[8].ToString();                                                                        /*备注*/

                ModelShowInfo.Time_Start = Convert.ToDateTime(txtModelStartTime.Text);
                ModelShowInfo.Time_End = Convert.ToDateTime(txtModelEndTime.Text);
                ModelShowInfo.Coord_TL.Lon = Convert.ToDouble(txtModelStartLon.Text);
                ModelShowInfo.Coord_TL.Lat = Convert.ToDouble(txtModelStartLat.Text);
                ModelShowInfo.Coord_DR.Lon = Convert.ToDouble(txtModelEndLon.Text);
                ModelShowInfo.Coord_DR.Lat = Convert.ToDouble(txtModelEndLat.Text);

                dataGrid_ApplyModelR.ItemsSource = ModelShowInfo.dtWinShowInfo.DefaultView;
                dataGrid_ApplyModelR.SelectedIndex = 0;
                cmbModelSID.SelectedIndex = Convert.ToUInt16(ModelShowInfo.dtWinShowInfo.Rows[0][2]) - 1;
                cmbModelSubCnt.SelectedIndex = Convert.ToUInt16(ModelShowInfo.dtWinShowInfo.Rows[0][3]) - 1;
            }
            else
            {
                ModelShowInfo.WindowsCnt = 0;
                ModelShowInfo.dtWinShowInfo = null;
                dataGrid_ApplyModelR.ItemsSource = null;
            }
        }
        private void dataGrid_ApplyModelR_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var sel = (DataRowView)dataGrid_ApplyModelR.SelectedItem;
            if (sel != null)
            {
                cmbModelWinID2.SelectedIndex = Convert.ToUInt16(sel.Row[1]) - 1;
                cmbModelSubID.SelectedIndex = Convert.ToUInt16(sel.Row[4]) - 1;
                cmbModelSubType.SelectedIndex = Convert.ToUInt16(sel.Row[6]);
            }
        }
        /*选择窗口数量后触发事件*/
        private void cmbModelWinCnt_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbModelWinID1 == null)
                return;
            cmbModelWinID1.Items.Clear();
            for (int i = 0; i < cmbModelWinCnt.SelectedIndex + 1; i++)
                cmbModelWinID1.Items.Add(i + 1);
            cmbModelWinID1.SelectedIndex = 0;
            cmbModelWinID2.Items.Clear();
            for (int i = 0; i < cmbModelWinCnt.SelectedIndex + 1; i++)
                cmbModelWinID2.Items.Add(i + 1);
            cmbModelWinID2.SelectedIndex = 0;
        }
        /*选择子窗体数量后触发事件*/
        private void cmbModelSubCnt_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbModelSubID == null)
                return;
            cmbModelSubID.Items.Clear();
            for (int i = 0; i < cmbModelSubCnt.SelectedIndex + 1; i++)
                cmbModelSubID.Items.Add(i + 1);
            cmbModelSubID.SelectedIndex = 0;
        }
        /*1设置名称、窗口数量、备注*/
        private void btnModelSet1_Click(object sender, RoutedEventArgs e)
        {
            ModelShowInfo.Time_Start = Convert.ToDateTime(txtModelStartTime.Text);
            ModelShowInfo.Time_End = Convert.ToDateTime(txtModelEndTime.Text);
            ModelShowInfo.Coord_TL.Lon = Convert.ToDouble(txtModelStartLon.Text);
            ModelShowInfo.Coord_TL.Lat = Convert.ToDouble(txtModelStartLat.Text);
            ModelShowInfo.Coord_DR.Lon = Convert.ToDouble(txtModelEndLon.Text);
            ModelShowInfo.Coord_DR.Lat = Convert.ToDouble(txtModelEndLat.Text);
            string sql = "select * from Apply_Model where 名称='" + txtModelName.Text + "'";
            DataTable dtModel = SQLiteFunc.SelectDTSQL(sql);
            if (dtModel.Rows.Count == 0)
            {
                SQLiteFunc.ExcuteSQL("insert into Apply_Model (名称,窗口数量,起始时间,结束时间,起始经度,结束经度,起始纬度,结束纬度,备注) values ('?',?,'?','?',?,?,?,?,'?')", txtModelName.Text, cmbModelWinCnt.SelectedIndex + 1, ModelShowInfo.Time_Start.ToString("yyyy-MM-dd HH:mm:ss"), ModelShowInfo.Time_End.ToString("yyyy-MM-dd HH:mm:ss"), 0, 0, 0, 0, txtModelRemark.Text);
                for (int i = 0; i < cmbModelWinCnt.SelectedIndex + 1; i++)
                    SQLiteFunc.ExcuteSQL("insert into Apply_ModelR (名称,窗口编号,显示器编号,子窗体数量,子窗体编号,窗体类型,窗体类型编号) values ('?',?,?,?,?,'?',?)", txtModelName.Text, i + 1, 1, 1, 1, "图像", 0);
                getApplyModel();
                dataGrid_ApplyModel.SelectedIndex = dataGrid_ApplyModel.Items.Count - 1;
            }
            else
            {
                SQLiteFunc.ExcuteSQL("update Apply_Model set 窗口数量=?,起始时间='?',结束时间='?',起始经度=?,结束经度=?,起始纬度=?,结束纬度=?,备注='?' where 名称='?'", cmbModelWinCnt.SelectedIndex + 1, ModelShowInfo.Time_Start.ToString("yyyy-MM-dd HH:mm:ss"), ModelShowInfo.Time_End.ToString("yyyy-MM-dd HH:mm:ss"), 0, 0, 0, 0, txtModelRemark.Text, txtModelName.Text);
                if(cmbModelWinCnt.SelectedIndex + 1 > Convert.ToUInt16(dtModel.Rows[0][1]))
                    for (int i = Convert.ToUInt16(dtModel.Rows[0][1]); i < cmbModelWinCnt.SelectedIndex + 1; i++)
                        SQLiteFunc.ExcuteSQL("insert into Apply_ModelR (名称,窗口编号,显示器编号,子窗体数量,子窗体编号,窗体类型,窗体类型编号) values ('?',?,?,?,?,'?',?)", txtModelName.Text, i + 1, 1, 1, 1, "图像", 0);
                else if (cmbModelWinCnt.SelectedIndex + 1 < Convert.ToUInt16(dtModel.Rows[0][1]))
                    for (int i = cmbModelWinCnt.SelectedIndex+1; i < Convert.ToUInt16(dtModel.Rows[0][1]); i++)
                        SQLiteFunc.ExcuteSQL("delete from Apply_ModelR where 名称='?' and 窗口编号=?;", txtModelName.Text, i+1);
                sql = "select * from Apply_ModelR where 名称='" + txtModelName.Text + "'";
                dataGrid_ApplyModelR.ItemsSource = SQLiteFunc.SelectDTSQL(sql).DefaultView;
            }
        }
        /*2设置子窗体数*/
        private void btnModelSet2_Click(object sender, RoutedEventArgs e)
        {
            string sql = "select * from Apply_ModelR where 名称='" + txtModelName.Text + "' order by 窗口编号,子窗体编号";
            DataTable dtModel = SQLiteFunc.SelectDTSQL(sql);
            if (dtModel.Rows.Count == 0)
            {
                System.Windows.MessageBox.Show("不存在该样式!", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (Convert.ToUInt16(dtModel.Rows[0][3]) < cmbModelSubCnt.SelectedIndex+1)
            { 
                for (int i = Convert.ToUInt16(dtModel.Rows[0][3]); i < cmbModelSubCnt.SelectedIndex + 1; i++)
                    SQLiteFunc.ExcuteSQL("insert into Apply_ModelR (名称,窗口编号,显示器编号,子窗体数量,子窗体编号,窗体类型,窗体类型编号) values ('?',?,?,?,?,'?',?)", txtModelName.Text, cmbModelWinID1.SelectedIndex+1, 1, 1, i + 1, "图像", 0);
            }
            else if (Convert.ToUInt16(dtModel.Rows[0][3]) > cmbModelSubCnt.SelectedIndex + 1)
            {
                for (int i = cmbModelSubCnt.SelectedIndex+1; i <= Convert.ToUInt16(dtModel.Rows[0][3]) ; i++)
                    SQLiteFunc.ExcuteSQL("delete from Apply_ModelR where 名称='?' and 子窗体编号=?;", txtModelName.Text, i + 1);
            }
            SQLiteFunc.ExcuteSQL("update Apply_ModelR set 子窗体数量=?,显示器编号=? where 名称='?' and 窗口编号=?", cmbModelSubCnt.SelectedIndex + 1,cmbModelSID.SelectedIndex+1, txtModelName.Text,cmbModelWinID1.SelectedIndex+1);
            sql = "select * from Apply_ModelR where 名称='" + txtModelName.Text + "' order by 窗口编号,子窗体编号";
            dataGrid_ApplyModelR.ItemsSource = SQLiteFunc.SelectDTSQL(sql).DefaultView;
        }
        /*3设置窗体类型*/
        private void btnModelSet3_Click(object sender, RoutedEventArgs e)
        {
            SQLiteFunc.ExcuteSQL("update Apply_ModelR set 窗体类型='?',窗体类型编号=? where 窗口编号=? and 子窗体编号=?", cmbModelSubType.Text, cmbModelSubType.SelectedIndex,cmbModelWinID2.SelectedIndex+1, cmbModelSubID.SelectedIndex+1);
            string sql = "select * from Apply_ModelR where 名称='" + txtModelName.Text + "' order by 窗口编号,子窗体编号";
            dataGrid_ApplyModelR.ItemsSource = SQLiteFunc.SelectDTSQL(sql).DefaultView;
        }
        /*删除应用样式*/
        private void btnModelDel_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.MessageBox.Show("确认删除该应用样式?", "提示", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.Cancel)
                return;
            SQLiteFunc.ExcuteSQL("delete from Apply_Model where 名称='?'", txtModelName.Text);
            SQLiteFunc.ExcuteSQL("delete from Apply_ModelR where 名称='?'", txtModelName.Text);
            getApplyModel();
        }
        /*显示样式*/
        private async void btnModelShow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DataTable dt = await DataProc.QueryResult(ModelShowInfo.Time_Start != ModelShowInfo.Time_End, ModelShowInfo.Coord_TL.Lon != ModelShowInfo.Coord_DR.Lon && ModelShowInfo.Coord_TL.Lat != ModelShowInfo.Coord_DR.Lat, false, ModelShowInfo.Time_Start, ModelShowInfo.Time_End, 0, 0, ModelShowInfo.Coord_TL, ModelShowInfo.Coord_DR);
                dataGrid_Result.ItemsSource = dt.DefaultView;
                dataGrid_SatePose.ItemsSource = dt.DefaultView;
                DataQuery.QueryResult = dt;
                ImageInfo.GetImgInfo(dt);       /*存储图像信息*/
                SetImgInfo();
            }
            catch (Exception E)
            {
                System.Windows.MessageBox.Show("无数据!","提示",MessageBoxButton.OK,MessageBoxImage.Information);
                return;
            }

            App.global_ApplyModel.Clear();
            for (int i = 0; i < ModelShowInfo.WindowsCnt; i++)
                App.global_ApplyModel.Add(new MultiFuncWindow());
            int[] subCnt = new int[ModelShowInfo.WindowsCnt];
            subCnt[0] = Convert.ToUInt16(ModelShowInfo.dtWinShowInfo.Rows[0][3]);
            int p = subCnt[0];
            for (int i = 1; i < ModelShowInfo.WindowsCnt; i++)
            {
                subCnt[i] = Convert.ToUInt16(ModelShowInfo.dtWinShowInfo.Rows[p][3]);
                p += subCnt[i];
            }
            MultiFuncWindow[] w = new MultiFuncWindow[ModelShowInfo.WindowsCnt];
            for (int i = 0; i < ModelShowInfo.WindowsCnt; i++)
            {
                w[i] = (MultiFuncWindow)App.global_ApplyModel[i];
                w[i].DisplayMode = (GridMode)(subCnt[i] - 1);
            }
            foreach (DataRow dr in ModelShowInfo.dtWinShowInfo.Rows)
            {
                if (!w[Convert.ToUInt16(dr[1]) - 1].isShow)
                    w[Convert.ToUInt16(dr[1]) - 1].ScreenShow(Screen.AllScreens, 0, Convert.ToUInt16(dr[1]).ToString());
                w[Convert.ToUInt16(dr[1]) - 1].Refresh(Convert.ToUInt16(dr[4]) - 1, (WinFunc)Convert.ToUInt16(dr[6]));
            }
        }
        #endregion
    }
}