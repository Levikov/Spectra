﻿using Microsoft.Win32;
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
    public partial class MainWindow : Window
    {
        /*构造函数+系统初始化*/
        SelectedDatesCollection sds;
        public MainWindow()
        {
            InitializeComponent();
            //sds = new SelectedDatesCollection(calendarFile);
            //calendarFile.SelectedDates.AddRange((DateTime.Now.Date).AddDays(-100), (DateTime.Now.Date).AddDays(-1));
        }
        #region 界面控制
        /*窗体加载时显示默认值*/
        private void GroupBox_Loaded(object sender, RoutedEventArgs e)
        {
            getDefaultShow();
            getApplyModel();
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
        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Microsoft.Win32.OpenFileDialog openFile = new Microsoft.Win32.OpenFileDialog();
                openFile.Filter = "All Files(*.*)|*.*";
                if ((bool)openFile.ShowDialog())
                {
                    FileInfo.srcFilePathName = openFile.FileName;                                                                   //文件路径名称
                    FileInfo.srcFileName = FileInfo.srcFilePathName.Substring(FileInfo.srcFilePathName.LastIndexOf('\\') + 1);      //文件名称
                    tb_Console.Text = DataProc.checkFileState();                                                                    //检查文件状态
                    /*窗体控件*/
                    dataGrid_Errors.ItemsSource = SQLiteFunc.SelectDTSQL("select * from FileErrors where MD5='" + FileInfo.md5 + "'").DefaultView;  //显示错误信息
                    tb_Path.Text = FileInfo.srcFilePathName;
                    txtCurrentFile.Text = FileInfo.srcFileName;
                    prog_Import.Value = 0;
                    btnUnpFile.IsEnabled = true;
                    btnDecFile.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }
        /*点击解包*/
        private async void btnUnpFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (FileInfo.isUnpack)
                    if (System.Windows.MessageBox.Show("该文件已解包,是否要重新解包并覆盖?", "提示", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.Cancel)
                        return;

                IProgress<DataView> IProg_DataView = new Progress<DataView>((Prog_DataView) => { dataGrid_Errors.ItemsSource = Prog_DataView; });
                IProgress<double> IProgress_Prog = new Progress<double>((ProgressValue) => { prog_Import.Value = ProgressValue * this.prog_Import.Maximum; });
                IProgress<string> IProgress_List = new Progress<string>((ProgressString) => { this.tb_Console.Text = ProgressString + "\n" + this.tb_Console.Text; });
                await DataProc.unpackFile(IProg_DataView, IProgress_Prog, IProgress_List);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }
        #endregion

        #region 数据解压
        /*用于放弃操作*/
        private CancellationTokenSource cancelImport = new CancellationTokenSource();
        /*点击解压*/
        private async void btnDecFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (FileInfo.isDecomp)
                    if (System.Windows.MessageBox.Show("该文件已解压,是否要重新解压并覆盖?", "提示", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.Cancel)
                        return;

                b_Abort_Import.IsEnabled = true;
                btnOpenFile.IsEnabled = false;
                btnDecFile.IsEnabled = false;
                btnSelectFile.IsEnabled = false;
                btnDelRecord.IsEnabled = false;

                IProgress<double> IProgress_Prog = new Progress<double>((ProgressValue) => { prog_Import.Value = ProgressValue * this.prog_Import.Maximum; });
                IProgress<string> IProgress_List = new Progress<string>((ProgressString) => { this.tb_Console.Text = ProgressString + "\n" + this.tb_Console.Text; });
                //App.global_Win_Dynamic = new DynamicImagingWindow_Win32();
                //App.global_Win_Dynamic.Show();
                await DataProc.Import_5(IProgress_Prog, IProgress_List, cancelImport.Token);
                IProgress_List.Report(DateTime.Now.ToString("HH:mm:ss") + " 操作成功！");
                //App.global_Win_Dynamic.Close();

                SQLiteFunc.ExcuteSQL("update FileDetails_dec set 解压时间='?',解压后文件路径='?',帧数='?',起始时间='?',结束时间='?',起始经纬='?',结束经纬='?' where MD5='?'",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), $"{Environment.CurrentDirectory}\\decFiles\\{FileInfo.md5}\\result\\", FileInfo.frmSum, FileInfo.startTime.ToString("yyyy-MM-dd HH:mm:ss"), FileInfo.endTime.ToString("yyyy-MM-dd HH:mm:ss"), FileInfo.startCoord.convertToString(), FileInfo.endCoord.convertToString(), FileInfo.md5);
                SQLiteFunc.ExcuteSQL("update FileDetails set 是否已解压='是' where MD5='?';", FileInfo.md5);

                b_Abort_Import.IsEnabled = false;
                btnOpenFile.IsEnabled = true;
                btnDecFile.IsEnabled = true;
                btnSelectFile.IsEnabled = true;
                btnDelRecord.IsEnabled = true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message);
            }
        }

        private void b_Abort_Import_Click(object sender, RoutedEventArgs e)
        {
            cancelImport.Cancel();
        }
        #endregion

        #region 文件检索
        /*选定文件*/
        private void btnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sel = (DataRowView)dataGrid_srcFile.SelectedItem;
                if (sel != null)
                {
                    FileInfo.srcFileName = (string)sel.Row[0];
                    FileInfo.srcFilePathName = (string)sel.Row[1];
                    FileInfo.srcFileLength = Convert.ToInt64(sel.Row[2]);
                    FileInfo.isUnpack = ((string)sel.Row[3] == "是");
                    FileInfo.isDecomp = ((string)sel.Row[4] == "是");
                    FileInfo.md5 = (string)sel.Row[5];
                    txtCurrentFile.Text = FileInfo.srcFileName;
                }
                else
                {
                    System.Windows.MessageBox.Show("请先选中条目");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message);
            }
        }
        /*查找文件*/
        private void textSelectFile_TextChanged(object sender, TextChangedEventArgs e)
        {
            dataGrid_srcFile.ItemsSource = SQLiteFunc.SelectDTSQL("SELECT * from FileDetails where 文件路径 like '%" + textSelectFile.Text + "%'").DefaultView;
        }
        /*选中文件后显示解压情况*/
        private void dataGrid_srcFile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var sel = (DataRowView)dataGrid_srcFile.SelectedItem;
            if (sel != null)
                dataGrid_decFile.ItemsSource = SQLiteFunc.SelectDTSQL("SELECT * from FileDetails_dec where MD5='" + sel.Row[5] + "'").DefaultView;
        }
        /*查找所有文件*/
        private void btnGetAllRecord_Click(object sender, RoutedEventArgs e)
        {
            dataGrid_srcFile.ItemsSource = SQLiteFunc.SelectDTSQL("SELECT * from FileDetails").DefaultView;
        }
        /*删除文件记录*/
        private void btnDelRecord_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sel = (DataRowView)dataGrid_srcFile.SelectedItem;
                if (sel != null)
                {
                    if (System.Windows.MessageBox.Show("确认删除该文件的记录?", "提示", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.Cancel) return;
                    else
                    {
                        SQLiteFunc.ExcuteSQL("delete from FileDetails where MD5='" + sel.Row[5] + "'");
                        SQLiteFunc.ExcuteSQL("delete from FileDetails_dec where MD5='" + sel.Row[5] + "'");
                        SQLiteFunc.ExcuteSQL("delete from FileErrors where MD5='" + sel.Row[5] + "'");
                        dataGrid_srcFile.ItemsSource = SQLiteFunc.SelectDTSQL("SELECT * from FileDetails").DefaultView;
                    }
                    if (System.Windows.MessageBox.Show("是否同时删除缓存文件?", "提示", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                    {
                        if (Directory.Exists($"{Environment.CurrentDirectory}\\decFiles\\{sel.Row[5]}"))
                            Directory.Delete($"{Environment.CurrentDirectory}\\decFiles\\{sel.Row[5]}", true);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message);
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
                DataTable dt = await DataProc.QueryResult(FileInfo.md5,(bool)cb_byTime.IsChecked, (bool)this.cb_byCoord.IsChecked, (bool)this.cb_byFrmCnt.IsChecked, start_time, end_time, start_FrmCnt, end_FrmCnt, Coord_TL, Coord_DR);
                dataGrid_Result.ItemsSource = dt.DefaultView;
                dataGrid_SatePose.ItemsSource = dt.DefaultView;
                DataQuery.QueryResult = dt;
                ImageInfo.dtImgInfo = dt;
                ImageInfo.GetImgInfo();       /*存储图像信息*/
                SetImgInfo();
            }
            catch (Exception E)
            {
                System.Windows.MessageBox.Show(E.Message);
            }
        }
        /*点击生成图像*/
        private async void btnMakeImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IProgress<double> IProgress_Prog = new Progress<double>((ProgressValue) => { progMakeImage.Value = ProgressValue * progMakeImage.Maximum; });
                await ImageInfo.MakeImage(IProgress_Prog);
                System.Windows.MessageBox.Show("OK");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
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
            try
            {
                //DataTable dt = DataQuery.QueryResult;
                //App.global_Win_Map = new MapWindow();
                //App.global_Win_Map.Show();
                //App.global_Win_Map.DrawRectangle(new Point((double)DataQuery.QueryResult.Rows[0].ItemArray[3], (double)DataQuery.QueryResult.Rows[0].ItemArray[4]), new Point((double)DataQuery.QueryResult.Rows[DataQuery.QueryResult.Rows.Count - 1].ItemArray[3], (double)DataQuery.QueryResult.Rows[DataQuery.QueryResult.Rows.Count - 1].ItemArray[4]));
                initWindows(ImageInfo.strFilesPath, WinShowInfo.WindowsCnt,WinShowInfo.dtWinShowInfo);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
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

        #region 光谱显示
        /*显示单张图像*/
        private void btnShowSpeImg_Click(object sender, RoutedEventArgs e)
        {
            if (App.global_Win_SpecImg == null)
            {
                App.global_Win_SpecImg = new MultiFuncWindow();
                App.global_Win_SpecImg.DisplayMode = GridMode.One;
            }
            if (!App.global_Win_SpecImg.isShow)
                App.global_Win_SpecImg.ScreenShow(Screen.AllScreens, 0, "光谱图像");
            App.global_Win_SpecImg.Refresh(ImageInfo.strFilesPath, 0, WinFunc.Image);
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
            App.global_Win_3D.Refresh(ImageInfo.strFilesPath, 0, WinFunc.Cube);
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
            App.global_Win_ImgCompare.Refresh(ImageInfo.strFilesPath, 0, WinFunc.Image);
            App.global_Win_ImgCompare.Refresh(ImageInfo.strFilesPath, 1, WinFunc.Image);
            App.global_Win_ImgCompare.Refresh(ImageInfo.strFilesPath, 2, WinFunc.Image);
            App.global_Win_ImgCompare.Refresh(ImageInfo.strFilesPath, 3, WinFunc.Image);
        }
        /*设置对比图像谱段*/
        private void btnCompareSet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ckb1sub.IsChecked == true)
                    ((Ctrl_ImageView)(App.global_Win_ImgCompare.UserControls[0])).Refresh(Convert.ToUInt16(txtCompareR.Text), ColorRenderMode.Grayscale, ImageInfo.strFilesPath);
                if (ckb2sub.IsChecked == true)
                    ((Ctrl_ImageView)(App.global_Win_ImgCompare.UserControls[1])).Refresh(Convert.ToUInt16(txtCompareGray2.Text), ColorRenderMode.Grayscale, ImageInfo.strFilesPath);
                if (ckb3sub.IsChecked == true)
                    ((Ctrl_ImageView)(App.global_Win_ImgCompare.UserControls[2])).Refresh(Convert.ToUInt16(txtCompareGray3.Text), ColorRenderMode.Grayscale, ImageInfo.strFilesPath);
                if (ckb4sub.IsChecked == true)
                    ((Ctrl_ImageView)(App.global_Win_ImgCompare.UserControls[3])).Refresh(Convert.ToUInt16(txtCompareGray4.Text), ColorRenderMode.Grayscale, ImageInfo.strFilesPath);
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show("窗体未初始化！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        #endregion

        #region 异常检测
        /*获取异常信息*/
        private void btnAbnGet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ImageInfo.noise_value = Convert.ToUInt16(txtAbnNoise.Text);
            }
            catch
            {
                System.Windows.MessageBox.Show("请输入噪声灰度!", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SQLiteFunc.ExcuteSQL("delete from Detect_Abnormal");

            new Thread(() =>
            {
                for (int i = 0; i < 160; i++)
                {
                    if (ImageInfo.ImgDetectAbnormal(i, 100) < 0)
                    {
                        System.Windows.MessageBox.Show("文件不存在!");
                        return;
                    }
                    Dispatcher.Invoke(new Action(() =>
                    {
                        pgbDetectAbnormal.Value = i / (double)159;
                        dataGrid_DetectAbnormal.ItemsSource = SQLiteFunc.SelectDTSQL("select * from Detect_Abnormal").DefaultView;
                    }));
                }
                System.Windows.MessageBox.Show("完成!", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }).Start();
        }
        /*将DataGrid导出为Excel*/
        private void btnAbnExport_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.FileName = "123";
            saveFileDialog.Filter = "Excel (*.XLS)|*.xls";
            if ((bool)(saveFileDialog.ShowDialog()))
            {
                try
                {
                    ExcelHelper _excelHelper = new ExcelHelper();
                    _excelHelper.SaveToExcel(saveFileDialog.FileName, ((DataView)dataGrid_DetectAbnormal.ItemsSource).Table);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("导出失败：" + ex.Message);
                    return;
                }
                System.Windows.MessageBox.Show("导出成功");
            }
        }
        #endregion

        #region 应用样式
        /*获得已有的应用样式*/
        private void getApplyModel()
        {
            ModelShowInfo.dtModelList = SQLiteFunc.SelectDTSQL("select * from Apply_Model order by 名称");
            dataGrid_ApplyModel.ItemsSource = ModelShowInfo.dtModelList.DefaultView;
            dataGrid_ApplyModel.SelectedIndex = 0;
        }
        /*获得当前应用样式*/
        private void getCurApplyModel()
        {
            string sql = "select * from Apply_Model where 名称='" + txtModelName.Text + "'";
            dataGrid_ApplyModel.ItemsSource = SQLiteFunc.SelectDTSQL(sql).DefaultView;
            dataGrid_ApplyModel.SelectedIndex = 0;
        }
        /*选中datagrid的应用样式*/
        private void dataGrid_ApplyModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var sel = (DataRowView)dataGrid_ApplyModel.SelectedItem;
                if (sel != null)
                {
                    ModelShowInfo.WindowsCnt = Convert.ToUInt16(sel.Row[1]);
                    ModelShowInfo.dtWinShowInfo = SQLiteFunc.SelectDTSQL("SELECT 窗口数量,图像模式1,图像模式4,谷歌地图,三维立方体,图像地图模式,曲线模式 from Apply_Model where 名称='" + sel.Row[0] + "'");
                    txtModelName.Text = sel.Row[0].ToString();                                                                          /*名称*/
                    txtModelWinCnt.Text = Convert.ToString(sel.Row[1]);                                                                 /*窗口数量*/
                    txtModelStartTime.Text = (sel.Row[2].ToString() == "") ? "" : Convert.ToDateTime(sel.Row[2]).ToString();            /*起始时间*/
                    txtModelEndTime.Text = (sel.Row[3].ToString() == "") ? "" : Convert.ToDateTime(sel.Row[3]).ToString();              /*结束时间*/
                    txtModelStartLon.Text = (sel.Row[4].ToString() == "") ? "" : sel.Row[4].ToString();                                 /*起始经度*/
                    txtModelEndLon.Text = (sel.Row[5].ToString() == "") ? "" : sel.Row[5].ToString();                                   /*结束经度*/
                    txtModelStartLat.Text = (sel.Row[6].ToString() == "") ? "" : sel.Row[6].ToString();                                 /*起始纬度*/
                    txtModelEndLat.Text = (sel.Row[7].ToString() == "") ? "" : sel.Row[7].ToString();                                   /*结束纬度*/
                    cbModelImage1.IsChecked = (sel.Row[8].ToString() == "True");                                                        /*图像模式1*/
                    cbModelImage4.IsChecked = (sel.Row[9].ToString() == "True");                                                        /*图像模式4*/
                    cbModelMap.IsChecked = (sel.Row[10].ToString() == "True");                                                          /*谷歌地图*/
                    cbModel3D.IsChecked = (sel.Row[11].ToString() == "True");                                                           /*三维立方体*/
                    cbModelImageMap.IsChecked = (sel.Row[12].ToString() == "True");                                                     /*图像/地图模式*/
                    cbModelCurve.IsChecked = (sel.Row[13].ToString() != "False");                                                       /*曲线模式*/
                    rbModelCurve1.IsChecked = (sel.Row[13].ToString() != "模式4");                                                      /*曲线模式*/
                    rbModelCurve4.IsChecked = (sel.Row[13].ToString() == "模式4");                                                      /*曲线模式*/
                    txtModelRemark.Text = sel.Row[14].ToString();                                                                       /*备注*/
                    ModelShowInfo.md5 = sel.Row[15].ToString();                                                                         /*MD5*/

                    ModelShowInfo.Time_Start = Convert.ToDateTime(txtModelStartTime.Text);
                    ModelShowInfo.Time_End = Convert.ToDateTime(txtModelEndTime.Text);
                    ModelShowInfo.Coord_TL.Lon = Convert.ToDouble(txtModelStartLon.Text);
                    ModelShowInfo.Coord_TL.Lat = Convert.ToDouble(txtModelStartLat.Text);
                    ModelShowInfo.Coord_DR.Lon = Convert.ToDouble(txtModelEndLon.Text);
                    ModelShowInfo.Coord_DR.Lat = Convert.ToDouble(txtModelEndLat.Text);
                }
                else
                {
                    ModelShowInfo.WindowsCnt = 0;
                    ModelShowInfo.dtWinShowInfo = null;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }
        /*设置数据*/
        private void btnModelSetData_Click(object sender, RoutedEventArgs e)
        {
            try
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
                    SQLiteFunc.ExcuteSQL("insert into Apply_Model (名称,起始时间,结束时间,起始经度,结束经度,起始纬度,结束纬度,备注,MD5) values ('?','?','?',?,?,?,?,'?','?')", txtModelName.Text, ModelShowInfo.Time_Start.ToString("yyyy-MM-dd HH:mm:ss"), ModelShowInfo.Time_End.ToString("yyyy-MM-dd HH:mm:ss"), 0, 0, 0, 0, txtModelRemark.Text,FileInfo.md5);
                }
                else
                {
                    SQLiteFunc.ExcuteSQL("update Apply_Model set 起始时间='?',结束时间='?',起始经度=?,结束经度=?,起始纬度=?,结束纬度=?,备注='?',MD5='?' where 名称='?'", ModelShowInfo.Time_Start.ToString("yyyy-MM-dd HH:mm:ss"), ModelShowInfo.Time_End.ToString("yyyy-MM-dd HH:mm:ss"), ModelShowInfo.Coord_TL.Lon, ModelShowInfo.Coord_TL.Lat, ModelShowInfo.Coord_DR.Lon, ModelShowInfo.Coord_DR.Lat, txtModelRemark.Text,FileInfo.md5, txtModelName.Text);
                 }
                getCurApplyModel();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }
        /*设置样式*/
        private void btnModelSetStyle_Click(object sender, RoutedEventArgs e)
        {
            try
            { 
                if (txtModelName.Text == "")
                {
                    System.Windows.MessageBox.Show("请输入名称!", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                //选中项对应变量
                Int16[] isCheck = new Int16[7];string strCurve = "False";
                isCheck[0] = Convert.ToInt16(cbModelImage1.IsChecked);
                isCheck[1] = Convert.ToInt16(cbModelImage4.IsChecked);
                isCheck[2] = Convert.ToInt16(cbModelMap.IsChecked);
                isCheck[3] = Convert.ToInt16(cbModel3D.IsChecked);
                isCheck[4] = Convert.ToInt16(cbModelImageMap.IsChecked);
                isCheck[5] = Convert.ToInt16(cbModelCurve.IsChecked);
                isCheck[6] = (Int16)(isCheck[0] + isCheck[1] + isCheck[2] + isCheck[3] + isCheck[4] + isCheck[5]);
                if (cbModelCurve.IsChecked == true)
                { 
                    if(rbModelCurve1.IsChecked == true)
                        strCurve = "模式1";
                    else
                        strCurve = "模式4";
                }

                string sql = "select * from Apply_Model where 名称='" + txtModelName.Text + "' order by 名称";
                DataTable dtModel = SQLiteFunc.SelectDTSQL(sql);
                if (dtModel.Rows.Count == 0)
                    SQLiteFunc.ExcuteSQL("insert into Apply_Model (名称,窗口数量,图像模式1,图像模式4,谷歌地图,三维立方体,图像地图模式,曲线模式) values ('?',?,?,?,?,'?',?)",
                        txtModelName.Text, isCheck[6], isCheck[0], isCheck[1], isCheck[2], isCheck[3], isCheck[4], strCurve);
                else
                    SQLiteFunc.ExcuteSQL("update Apply_Model set 窗口数量=?,图像模式1=?,图像模式4=?,谷歌地图=?,三维立方体=?,图像地图模式=?,曲线模式='?' where 名称='?'",
                        isCheck[6], isCheck[0], isCheck[1], isCheck[2], isCheck[3], isCheck[4], strCurve, txtModelName.Text);
                getCurApplyModel();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }
        /*删除应用样式*/
        private void btnModelDel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (System.Windows.MessageBox.Show("确认删除该应用样式?", "提示", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.Cancel)
                    return;
                SQLiteFunc.ExcuteSQL("delete from Apply_Model where 名称='?'", txtModelName.Text);
                getApplyModel();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }
        /*刷新列表*/
        private void btnModelRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            { 
                getApplyModel();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }
        /*显示样式*/
        private async void btnModelShow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DataTable dt = await DataProc.QueryResult(ModelShowInfo.md5,ModelShowInfo.Time_Start != ModelShowInfo.Time_End, ModelShowInfo.Coord_TL.Lon != ModelShowInfo.Coord_DR.Lon && ModelShowInfo.Coord_TL.Lat != ModelShowInfo.Coord_DR.Lat, false, ModelShowInfo.Time_Start, ModelShowInfo.Time_End, 0, 0, ModelShowInfo.Coord_TL, ModelShowInfo.Coord_DR);
                dataGrid_Result.ItemsSource = dt.DefaultView;
                dataGrid_SatePose.ItemsSource = dt.DefaultView;
                DataQuery.QueryResult = dt;
                ImageInfo.dtImgInfo = dt;
                ImageInfo.GetImgInfo();       /*存储图像信息*/
                SetImgInfo();
                initWindows(ImageInfo.strFilesPath, ModelShowInfo.WindowsCnt, ModelShowInfo.dtWinShowInfo);
            }
            catch
            {
                System.Windows.MessageBox.Show("无数据!", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
        }
        #endregion

        #region 默认显示方式
        /*初始化显示窗体*/
        private void initWindows(string path, int winSum, DataTable dtShow)
        {
            for (int i = 0; i < winSum; i++)
                App.global_Windows.Add(new MultiFuncWindow());
            MultiFuncWindow[] w = new MultiFuncWindow[winSum];
            int cnt = 0;
            if (dtShow.Rows[0][1].ToString() == "True")
            {
                w[cnt] = (MultiFuncWindow)App.global_Windows[cnt];
                w[cnt].DisplayMode = GridMode.One;
                if (!w[cnt].isShow)
                    w[cnt].ScreenShow(Screen.AllScreens, 0, cnt.ToString());
                w[cnt].Refresh(path, 0, WinFunc.Image);
                cnt++;
            }
            if (dtShow.Rows[0][2].ToString() == "True")
            {
                w[cnt] = (MultiFuncWindow)App.global_Windows[cnt];
                w[cnt].DisplayMode = GridMode.Four;
                if (!w[cnt].isShow)
                    w[cnt].ScreenShow(Screen.AllScreens, 0, cnt.ToString());
                w[cnt].Refresh(path, 0, WinFunc.Image);
                w[cnt].Refresh(path, 1, WinFunc.Image);
                w[cnt].Refresh(path, 2, WinFunc.Image);
                w[cnt].Refresh(path, 3, WinFunc.Image);
                cnt++;
            }
            //谷歌地图
            if (dtShow.Rows[0][3].ToString() == "True")
            {
                w[cnt] = (MultiFuncWindow)App.global_Windows[cnt];
                w[cnt].DisplayMode = GridMode.One;
                if (!w[cnt].isShow)
                    w[cnt].ScreenShow(Screen.AllScreens, 0, cnt.ToString());
                w[cnt].Refresh(path, 0, WinFunc.Map);
                cnt++;
            }
            //三维立方体
            if (dtShow.Rows[0][4].ToString() == "True")
            {
                w[cnt] = (MultiFuncWindow)App.global_Windows[cnt];
                w[cnt].DisplayMode = GridMode.One;
                if (!w[cnt].isShow)
                    w[cnt].ScreenShow(Screen.AllScreens, 0, cnt.ToString());
                w[cnt].Refresh(path, 0, WinFunc.Cube);
                cnt++;
            }
            //图像地图
            if (dtShow.Rows[0][5].ToString() == "True")
            {
                w[cnt] = (MultiFuncWindow)App.global_Windows[cnt];
                w[cnt].DisplayMode = GridMode.Two;
                if (!w[cnt].isShow)
                    w[cnt].ScreenShow(Screen.AllScreens, 0, cnt.ToString());
                w[cnt].Refresh(path, 0, WinFunc.Image);
                w[cnt].Refresh(path, 1, WinFunc.Map);
                cnt++;
            }
            //曲线模式1
            if (dtShow.Rows[0][6].ToString() == "模式1")
            {
                w[cnt] = (MultiFuncWindow)App.global_Windows[cnt];
                w[cnt].DisplayMode = GridMode.One;
                if (!w[cnt].isShow)
                    w[cnt].ScreenShow(Screen.AllScreens, 0, cnt.ToString());
                w[cnt].Refresh(path, 0, WinFunc.Curve);
                cnt++;
            }
            //曲线模式4
            if (dtShow.Rows[0][6].ToString() == "模式4")
            {
                w[cnt] = (MultiFuncWindow)App.global_Windows[cnt];
                w[cnt].DisplayMode = GridMode.Four;
                if (!w[cnt].isShow)
                    w[cnt].ScreenShow(Screen.AllScreens, 0, cnt.ToString());
                w[cnt].Refresh(path, 0, WinFunc.Curve);
                w[cnt].Refresh(path, 1, WinFunc.Curve);
                w[cnt].Refresh(path, 2, WinFunc.Curve);
                w[cnt].Refresh(path, 3, WinFunc.Curve);
                cnt++;
            }
        }
        /*设置默认值*/
        private void btnShowSetStyle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //选中项对应变量
                Int16[] isCheck = new Int16[7]; string strCurve = "False";
                isCheck[0] = Convert.ToInt16(cbShowImage1.IsChecked);
                isCheck[1] = Convert.ToInt16(cbShowImage4.IsChecked);
                isCheck[2] = Convert.ToInt16(cbShowMap.IsChecked);
                isCheck[3] = Convert.ToInt16(cbShow3D.IsChecked);
                isCheck[4] = Convert.ToInt16(cbShowImageMap.IsChecked);
                isCheck[5] = Convert.ToInt16(cbShowCurve.IsChecked);
                isCheck[6] = (Int16)(isCheck[0] + isCheck[1] + isCheck[2] + isCheck[3] + isCheck[4] + isCheck[5]);
                if (cbShowCurve.IsChecked == true)
                {
                    if (rbShowCurve1.IsChecked == true)
                        strCurve = "模式1";
                    else
                        strCurve = "模式4";
                }
                //更新数据库
                SQLiteFunc.ExcuteSQL("update WinShowInfo set 窗口数量=?,图像模式1=?,图像模式4=?,谷歌地图=?,三维立方体=?,图像地图模式=?,曲线模式='?'",
                        isCheck[6], isCheck[0], isCheck[1], isCheck[2], isCheck[3], isCheck[4], strCurve);
                //更新界面
                getDefaultShow();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }
        /*刷新界面*/
        private void getDefaultShow()
        {
            try
            {
                WinShowInfo.dtWinShowInfo = SQLiteFunc.SelectDTSQL("select * from WinShowInfo");
                dataGrid_Screen.ItemsSource = WinShowInfo.dtWinShowInfo.DefaultView;
                if (WinShowInfo.dtWinShowInfo.Rows.Count > 0)
                {
                    WinShowInfo.WindowsCnt = Convert.ToUInt16(WinShowInfo.dtWinShowInfo.Rows[0][0]);
                    cbShowImage1.IsChecked = (WinShowInfo.dtWinShowInfo.Rows[0][1].ToString() == "True");                        /*图像模式1*/
                    cbShowImage4.IsChecked = (WinShowInfo.dtWinShowInfo.Rows[0][2].ToString() == "True");                        /*图像模式4*/
                    cbShowMap.IsChecked = (WinShowInfo.dtWinShowInfo.Rows[0][3].ToString() == "True");                          /*谷歌地图*/
                    cbShow3D.IsChecked = (WinShowInfo.dtWinShowInfo.Rows[0][4].ToString() == "True");                           /*三维立方体*/
                    cbShowImageMap.IsChecked = (WinShowInfo.dtWinShowInfo.Rows[0][5].ToString() == "True");                     /*图像/地图模式*/
                    cbShowCurve.IsChecked = (WinShowInfo.dtWinShowInfo.Rows[0][6].ToString() != "False");                       /*曲线模式*/
                    rbShowCurve1.IsChecked = (WinShowInfo.dtWinShowInfo.Rows[0][6].ToString() != "模式4");                      /*曲线模式*/
                    rbShowCurve4.IsChecked = (WinShowInfo.dtWinShowInfo.Rows[0][6].ToString() == "模式4");                      /*曲线模式*/
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }
        #endregion

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

        #region 数据存储

        private void btnSaveFilesPath_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string flodPath = fbd.SelectedPath;
            }
        }

        private void btnSaveFiles_Click(object sender, RoutedEventArgs e)
        {

        }
        #endregion
    }
}