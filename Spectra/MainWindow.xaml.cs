using Microsoft.Win32;
using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Threading;
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
                this.dataGrid_Result.ItemsSource = dt.DefaultView;
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

        #region 图像设置
        /*更新窗体显示*/
        public void SetImgInfo()
        {
            txtImgMinFrm.Text = ImageInfo.minFrm.ToString();
            txtImgMaxFrm.Text = ImageInfo.maxFrm.ToString();
            txtImgStartTime.Text = ImageInfo.startTime.ToString();
            txtImgEndTime.Text = ImageInfo.endTime.ToString();
            txtImgWidth.Text = ImageInfo.imgWidth.ToString();
            txtMapStartLon.Text = ImageInfo.startCoord.Lon.ToString();
            txtMapStartLat.Text = ImageInfo.startCoord.Lat.ToString();
            txtMapEndLon.Text = ImageInfo.endCoord.Lon.ToString();
            txtMapEndLat.Text = ImageInfo.endCoord.Lat.ToString();
        }
        /*设置图像谱段*/
        private void button_SetImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Ctrl_ImageView c = new Ctrl_ImageView();
                c = (Ctrl_ImageView)(((MultiFuncWindow)(App.global_Windows[Convert.ToUInt16(txtImgWinID.Text)-1])).UserControls[Convert.ToUInt16(txtImgSubID.Text)-1]);
                c.Refresh(Convert.ToUInt16(txtImgSpeIdxR.Text), (ColorRenderMode)(cmbImgMode.SelectedIndex));
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show("请选择正确的窗体进行设置！","提示",MessageBoxButton.OK,MessageBoxImage.Information);
            }
        }
        /*选择图像模式*/
        private void cmbImgMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (txtImgSpeIdxB == null)
                return;
            if (cmbImgMode.SelectedIndex == 1)
            {
                lblImgSpeR.Content = "谱段R值:";
                lblImgSpeG.Visibility = Visibility.Visible;
                lblImgSpeB.Visibility = Visibility.Visible;
                txtImgSpeIdxG.Visibility = Visibility.Visible;
                txtImgSpeIdxB.Visibility = Visibility.Visible;
            }
            else
            {
                lblImgSpeR.Content = "谱段值:";
                lblImgSpeG.Visibility = Visibility.Collapsed;
                lblImgSpeB.Visibility = Visibility.Collapsed;
                txtImgSpeIdxG.Visibility = Visibility.Collapsed;
                txtImgSpeIdxB.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region 组合模式
        /*设置窗口属性*/
        private void button_MixSetWinPro_Click(object sender, RoutedEventArgs e)
        {
            MultiFuncWindow w = new MultiFuncWindow();
            w = (MultiFuncWindow)App.global_Windows[Convert.ToUInt16(cmbMixWinID1.SelectedIndex)];
            w.DisplayMode = (GridMode)(cmbMixSubWinCnt.SelectedIndex);
            if (!w.isShow)
                w.ScreenShow();
        }
        /*设置窗体类型*/
        private void button_MixSetWinType_Click(object sender, RoutedEventArgs e)
        {
            MultiFuncWindow w = new MultiFuncWindow();
            w = (MultiFuncWindow)App.global_Windows[Convert.ToUInt16(cmbMixWinID2.SelectedIndex)];
            w.Refresh(Convert.ToUInt16(cmbMixSubWinID.SelectedIndex),(WinFunc)(cmbMixSubWinType.SelectedIndex));
            if (!w.isShow)
                w.ScreenShow();
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
                    w[Convert.ToUInt16(dr[0]) - 1].ScreenShow(Screen.AllScreens,0, Convert.ToUInt16(dr[0]));
                w[Convert.ToUInt16(dr[0]) - 1].Refresh(Convert.ToUInt16(dr[4])-1,(WinFunc)Convert.ToUInt16(dr[6]));
            }
        }
        /*窗体加载时显示默认值*/
        private void GroupBox_Loaded(object sender, RoutedEventArgs e)
        {
            getDefaultShow();
            setWindowID(WinShowInfo.WindowsCnt);
            setScreenID(Screen.AllScreens.Length);
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
            for (int i = 0; i < cnt; i++)
                cmbScreenSID.Items.Add(i + 1);
            cmbScreenSID.SelectedIndex = 0;
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
    }
}