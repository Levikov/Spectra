using FreeImageAPI;
using System;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Text;

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
            getBandWave();
            dataGrid_srcFile.ItemsSource = SQLiteFunc.SelectDTSQL("SELECT * from FileDetails").DefaultView;
            Global.pathDecFiles = txtSetDecPath.Text = SQLiteFunc.SelectDTSQL("SELECT * from Global where ID=1").Rows[0][1].ToString();
        }
        /*拖动界面*/
        private void WindowMain_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DragMove();
        }
        /*程序退出*/
        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Environment.Exit(0);
            }
            catch { }
        }

        private void btnMax_Click(object sender, RoutedEventArgs e)
        {
            if(WindowState == WindowState.Normal)
                WindowState = WindowState.Maximized;
            else
                WindowState = WindowState.Normal;
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
        #endregion

        #region 数据解压
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
                    tb_Console.Text = FileInfo.checkFileState();                                                                    //检查文件状态
                    /*窗体控件*/
                    dataGrid_Errors.ItemsSource = SQLiteFunc.SelectDTSQL("select * from FileErrors where MD5='" + FileInfo.md5 + "'").DefaultView;  //显示错误信息
                    tb_Path.Text = FileInfo.srcFilePathName;
                    txtCurrentFile.Text = FileInfo.srcFileName;
                    prog_Import.Value = 0;
                    btnDecFile.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }
        /*批处理文件*/
        private async void btnOpenFiles_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFile = new Microsoft.Win32.OpenFileDialog();
            openFile.Filter = "All Files(*.*)|*.*";
            openFile.Multiselect = true;
            if ((bool)openFile.ShowDialog())
            {
                btnOpenFile.IsEnabled = false;
                btnOpenFiles.IsEnabled = false;
                btnSelectFile.IsEnabled = false;
                btnDelRecord.IsEnabled = false;
                IProgress<double> IProg_Bar = new Progress<double>((ProgressValue) => { prog_Import.Value = ProgressValue * prog_Import.Maximum; });
                IProgress<string> IProg_Cmd = new Progress<string>((ProgressString) => { tb_Console.Text = ProgressString + "\n" + tb_Console.Text; });
                for (int fi = 0; fi < openFile.SafeFileNames.Length; fi++)
                {
                    try
                    {
                        FileInfo.srcFilePathName = openFile.FileNames[fi];                                                                   //文件路径名称
                        FileInfo.srcFileName = FileInfo.srcFilePathName.Substring(FileInfo.srcFilePathName.LastIndexOf('\\') + 1);      //文件名称
                        tb_Console.Text = FileInfo.checkFileState();                                                                    //检查文件状态

                        DataOper dataOper = new DataOper(FileInfo.srcFilePathName, FileInfo.md5);
                        await dataOper.main(IProg_Bar, IProg_Cmd);
                        dataGrid_Errors.ItemsSource = SQLiteFunc.SelectDTSQL("select * from FileErrors where MD5='" + FileInfo.md5 + "'").DefaultView;  //显示错误信息
                    }
                    catch
                    {
                        //System.Windows.MessageBox.Show(ex.ToString());
                    }
                }
                btnOpenFile.IsEnabled = true;
                btnOpenFiles.IsEnabled = true;
                btnSelectFile.IsEnabled = true;
                btnDelRecord.IsEnabled = true;
            }
        }

        /*点击解压*/
        private async void btnDecFile_Click(object sender, RoutedEventArgs e)
        {
            btnDecFile.IsEnabled = false;

            DataOper dataOper = new DataOper(FileInfo.srcFilePathName,FileInfo.md5);
            IProgress<double> IProg_Bar = new Progress<double>((ProgressValue) => { prog_Import.Value = ProgressValue * prog_Import.Maximum; });
            IProgress<string> IProg_Cmd = new Progress<string>((ProgressString) => { tb_Console.Text = ProgressString + "\n" + tb_Console.Text; });
            await dataOper.main(IProg_Bar, IProg_Cmd);
            dataGrid_Errors.ItemsSource = SQLiteFunc.SelectDTSQL("select * from FileErrors where MD5='" + FileInfo.md5 + "'").DefaultView;  //显示错误信息

            btnOpenFile.IsEnabled = true;
            btnTopB.IsChecked = true;
            btnLeftB1.IsChecked = true;
            searchList(FileInfo.md5, false, false, false);
        }

        /*用于放弃操作*/
        public CancellationTokenSource cancelImport = new CancellationTokenSource();
        private void b_Abort_Import_Click(object sender, RoutedEventArgs e)
        {
            cancelImport.Cancel(true);
        }
        #endregion

        #region 数据存储
        /*显示列表*/
        private void btnLeftA2_Click(object sender, RoutedEventArgs e)
        {
            IList<TestTreeView.Model.TreeModel> treeList = new List<TestTreeView.Model.TreeModel>();

            DataTable dtFirst = SQLiteFunc.SelectDTSQL("SELECT * from FileDetails");
            int cntFirst = dtFirst.Rows.Count;
            for (int i = 0; i < cntFirst; i++)
            {
                TestTreeView.Model.TreeModel tree = new TestTreeView.Model.TreeModel();
                tree.Id = dtFirst.Rows[i]["MD5"].ToString();
                tree.Name = dtFirst.Rows[i]["文件名"].ToString();
                tree.IsExpanded = true;

                DataTable dtSecond = SQLiteFunc.SelectDTSQL($"SELECT * from FileQuickView where MD5='{dtFirst.Rows[i]["MD5"].ToString()}' order by SubId");
                int cntSecond = dtSecond.Rows.Count;
                TreeViewItem[] tviSecond = new TreeViewItem[cntSecond];
                for (int j = 0; j < cntSecond; j++)
                {
                    TestTreeView.Model.TreeModel child = new TestTreeView.Model.TreeModel();
                    child.Id = tree.Id + "_" + j;
                    child.Name = j.ToString();
                    child.Parent = tree;
                    tree.Children.Add(child);
                }
                treeList.Add(tree);
            }

            treeQuickView.ItemsSourceData = treeList;
        }
        /*开始截取*/
        private void btnSectionBegin_Click(object sender, RoutedEventArgs e)
        {
            IList<TestTreeView.Model.TreeModel> treeList = treeQuickView.CheckedItemsIgnoreRelation();
            DataTable dtTreeView = new DataTable();
            dtTreeView.Columns.Add("MD5",Type.GetType("System.String"));
            dtTreeView.Columns.Add("ID");
            foreach (TestTreeView.Model.TreeModel tree in treeList)
            {
                if (tree.Id.Contains("_"))
                    dtTreeView.Rows.Add(new object[] { tree.Id.Substring(0, tree.Id.IndexOf('_')), tree.Id.Substring(tree.Id.IndexOf('_') + 1, 1) });
            }

            if (App.global_QuickViewWindow == null)
                App.global_QuickViewWindow = new QuickViewWindow();
            App.global_QuickViewWindow.ShowView(dtTreeView);
            return;
        }
        /*存储路径*/
        private void btnSaveFilesPath_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtSaveFilesPath.Text = fbd.SelectedPath;
                btnSaveFiles.IsEnabled = true;
            }
        }
        /*开始存储*/
        private async void btnSaveFiles_Click(object sender, RoutedEventArgs e)
        {
            progSaveAs.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(Convert.ToByte(255), Convert.ToByte(6), Convert.ToByte(176), Convert.ToByte(37)));
            var iprog_Prog = new Progress<double>((curPosi) => { progSaveAs.Value = curPosi; });
            await App.global_QuickViewWindow.SaveFiles(App.global_QuickViewWindow.getSelects(), txtSaveFilesPath.Text, iprog_Prog);
            progSaveAs.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(Convert.ToByte(255), Convert.ToByte(253), Convert.ToByte(94), Convert.ToByte(188)));
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
                    DataTable dt = SQLiteFunc.SelectDTSQL("SELECT * from FileDetails_dec where MD5='" + FileInfo.md5 + "'");
                    FileInfo.upkFilePathName = dt.Rows[0][7].ToString();
                    FileInfo.decFilePathName = dt.Rows[0][9].ToString();
                    ImageInfo.strFilesPath = FileInfo.decFilePathName;
                    btnTopB.IsChecked = true;
                    btnLeftB1.IsChecked = true;
                    searchList(FileInfo.md5, false, false, false);
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
        /*清除选定*/
        private void btnNoSelectFile_Click(object sender, RoutedEventArgs e)
        {
            FileInfo.md5 = null;
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
        /*打开解压文件夹*/
        private void decFileOpen_Click(object sender, RoutedEventArgs e)
        {
            if (dataGrid_decFile.Items.Count > 0)
            {
                string path = ((DataRowView)(dataGrid_decFile.Items[0])).Row[9].ToString();
                System.Diagnostics.Process.Start("explorer.exe", path);
            }
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
                        SQLiteFunc.ExcuteSQL("delete from AuxData where MD5='" + sel.Row[5] + "'");
                        SQLiteFunc.ExcuteSQL("delete from FileQuickView where MD5='" + sel.Row[5] + "'");
                        dataGrid_srcFile.ItemsSource = SQLiteFunc.SelectDTSQL("SELECT * from FileDetails").DefaultView;
                        dataGrid_decFile.ItemsSource = null;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message);
            }
        }
        #endregion

        #region 数据检索
        public DateTime start_time;                     //起始检索时刻
        public DateTime end_time;                       //终止检索时刻
        public Coord Coord_TL = new Coord(0, 0);        //左上角坐标
        public Coord Coord_DR = new Coord(0, 0);        //右下角坐标
        public long start_FrmCnt;                       //起始帧号
        public long end_FrmCnt;                         //终止帧号
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
        /*点击图像检索*/
        private async void searchList(string path,bool bTime,bool bCoord,bool bFrm)
        {
            ImageInfo.dtImgInfo = await DataProc.QueryResult(path, bTime, bCoord, bFrm,start_time, end_time, start_FrmCnt, end_FrmCnt, Coord_TL, Coord_DR);
            dataGrid_SatePose.ItemsSource = dataGrid_Result.ItemsSource = ImageInfo.dtImgInfo.DefaultView;
            ImageInfo.GetImgInfo();
            SetImgInfo();
            btnMakeImage.IsEnabled = true;
            dtp_Start.Text = ImageInfo.startTime.ToString("yyyy-MM-dd HH:mm:ss");
            dtp_End.Text = ImageInfo.endTime.ToString("yyyy-MM-dd HH:mm:ss");
            LT_Lat.Text = ImageInfo.endCoord.Lat.ToString();
            LT_Lon.Text = ImageInfo.startCoord.Lon.ToString();
            RB_Lat.Text = ImageInfo.startCoord.Lat.ToString();
            RB_Lon.Text = ImageInfo.endCoord.Lon.ToString();
            tb_start_frm.Text = ImageInfo.minFrm.ToString();
            tb_end_frm.Text = ImageInfo.maxFrm.ToString();
        }
        private void b_Query_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                searchList(FileInfo.md5, (bool)cb_byTime.IsChecked, (bool)this.cb_byCoord.IsChecked, (bool)this.cb_byFrmCnt.IsChecked);
            }
            catch
            {
                System.Windows.MessageBox.Show("无数据!","警告",MessageBoxButton.OK,MessageBoxImage.Warning);
            }
        }
        /*清空结果*/
        private void button_Clear_Result_Click(object sender, RoutedEventArgs e)
        {
            ImageInfo.dtImgInfo.Clear();
            dataGrid_Result.ItemsSource = null;
            dataGrid_SatePose.ItemsSource = null;
            btnMakeImage.IsEnabled = false;
        }
        /*点击生成图像*/
        private async void btnMakeImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IProgress<double> IProgress_Prog = new Progress<double>((ProgressValue) => { progMakeImage.Value = ProgressValue * progMakeImage.Maximum; });
                await ImageInfo.MakeImage(IProgress_Prog);
                ImageInfo.strFilesPath = $"{Environment.CurrentDirectory}\\showFiles\\";
                System.Windows.MessageBox.Show("OK");
                btnMakeImage.IsEnabled = false;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }
        /*显示图像按钮*/
        private void btnDisplay_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //App.global_Win_Map = new MapWindow();
                //App.global_Win_Map.Show();
                //App.global_Win_Map.DrawRectangle(new Point((double)ImageInfo.dtImgInfo.Rows[0].ItemArray[3], (double)ImageInfo.dtImgInfo.Rows[0].ItemArray[4]), new Point((double)ImageInfo.dtImgInfo.Rows[ImageInfo.dtImgInfo.Rows.Count - 1].ItemArray[3], (double)ImageInfo.dtImgInfo.Rows[ImageInfo.dtImgInfo.Rows.Count - 1].ItemArray[4]));
                initWindows(ImageInfo.strFilesPath, WinShowInfo.WindowsCnt, WinShowInfo.dtWinShowInfo);
                btnLeftB2.IsChecked = true;
            }
            catch
            {
                System.Windows.MessageBox.Show("无数据!", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
        //取一行谱段与波长的对应关系存储至Band_Wave表(不用该函数)
        private void setBandWave()
        {
            SQLiteFunc.ExcuteSQL("delete from Band_Wave");
            DataTable dt1 = SQLiteFunc.SelectDTSQL("select * from SpectrumMap where SpaN=1");
            SQLiteFunc.SelectDTSQL("delete from Band_Wave");
            for (int i = 159; i >= 0; i--)
            {
                if (Convert.ToDouble(dt1.Rows[0][i + 1]) == 0)
                    SQLiteFunc.ExcuteSQL("insert into Band_Wave (谱段,波长,光谱色) values (?,0,'-')", 160 - i);
                else if (Convert.ToDouble(dt1.Rows[0][i + 1]) < 400)
                    SQLiteFunc.ExcuteSQL("insert into Band_Wave (谱段,波长,光谱色) values (?,?,'紫外')", 160 - i, Convert.ToDouble(dt1.Rows[0][i + 1]));
                else if (Convert.ToDouble(dt1.Rows[0][i + 1]) < 450)
                    SQLiteFunc.ExcuteSQL("insert into Band_Wave (谱段,波长,光谱色) values (?,?,'紫')", 160 - i, Convert.ToDouble(dt1.Rows[0][i + 1]));
                else if (Convert.ToDouble(dt1.Rows[0][i + 1]) < 480)
                    SQLiteFunc.ExcuteSQL("insert into Band_Wave (谱段,波长,光谱色) values (?,?,'蓝')", 160 - i, Convert.ToDouble(dt1.Rows[0][i + 1]));
                else if (Convert.ToDouble(dt1.Rows[0][i + 1]) < 490)
                    SQLiteFunc.ExcuteSQL("insert into Band_Wave (谱段,波长,光谱色) values (?,?,'绿蓝')", 160 - i, Convert.ToDouble(dt1.Rows[0][i + 1]));
                else if (Convert.ToDouble(dt1.Rows[0][i + 1]) < 500)
                    SQLiteFunc.ExcuteSQL("insert into Band_Wave (谱段,波长,光谱色) values (?,?,'蓝绿')", 160 - i, Convert.ToDouble(dt1.Rows[0][i + 1]));
                else if (Convert.ToDouble(dt1.Rows[0][i + 1]) < 560)
                    SQLiteFunc.ExcuteSQL("insert into Band_Wave (谱段,波长,光谱色) values (?,?,'绿')", 160 - i, Convert.ToDouble(dt1.Rows[0][i + 1]));
                else if (Convert.ToDouble(dt1.Rows[0][i + 1]) < 580)
                    SQLiteFunc.ExcuteSQL("insert into Band_Wave (谱段,波长,光谱色) values (?,?,'黄绿')", 160 - i, Convert.ToDouble(dt1.Rows[0][i + 1]));
                else if (Convert.ToDouble(dt1.Rows[0][i + 1]) < 610)
                    SQLiteFunc.ExcuteSQL("insert into Band_Wave (谱段,波长,光谱色) values (?,?,'黄')", 160 - i, Convert.ToDouble(dt1.Rows[0][i + 1]));
                else if (Convert.ToDouble(dt1.Rows[0][i + 1]) < 650)
                    SQLiteFunc.ExcuteSQL("insert into Band_Wave (谱段,波长,光谱色) values (?,?,'橙')", 160 - i, Convert.ToDouble(dt1.Rows[0][i + 1]));
                else if (Convert.ToDouble(dt1.Rows[0][i + 1]) < 760)
                    SQLiteFunc.ExcuteSQL("insert into Band_Wave (谱段,波长,光谱色) values (?,?,'红')", 160 - i, Convert.ToDouble(dt1.Rows[0][i + 1]));
                else if (Convert.ToDouble(dt1.Rows[0][i + 1]) < 1500)
                    SQLiteFunc.ExcuteSQL("insert into Band_Wave (谱段,波长,光谱色) values (?,?,'近红外')", 160 - i, Convert.ToDouble(dt1.Rows[0][i + 1]));
            }
        }
        /*获得谱段-波长对应关系刷新DataGrid*/
        private void getBandWave()
        {
            try
            { 
                ImageInfo.dtBandWave = SQLiteFunc.SelectDTSQL("select * from Band_Wave order by 谱段");
                dataGrid_BandWave.ItemsSource = ImageInfo.dtBandWave.DefaultView;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("数据出错!", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        /*显示单谱段图像*/
        private void btnShowSpeImg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MultiFuncWindow w = new MultiFuncWindow();
                w = (MultiFuncWindow)App.global_Windows[0];
                w.DisplayMode = GridMode.One;
                if (!w.isShow)
                    w.ScreenShow(Screen.AllScreens, 0, "单谱段图像");
                UInt16[] colorBand = { 40, 40, 40 };
                w.RefreshImage(ImageInfo.strFilesPath, 0, WinFunc.Image, colorBand, ColorRenderMode.Grayscale);
            }
            catch
            {
                System.Windows.MessageBox.Show("无数据!", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        /*显示典型谱段图像对比*/
        private void btnShowImgCompare_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MultiFuncWindow w = new MultiFuncWindow();
                w = (MultiFuncWindow)App.global_Windows[1];
                w.DisplayMode = GridMode.Four;
                if (!w.isShow)
                    w.ScreenShow(Screen.AllScreens, 0, "典型谱段图像对比");
                w.RefreshImage(ImageInfo.strFilesPath, 0, WinFunc.Image, new UInt16[] { 40, 77, 127 }, ColorRenderMode.ArtColor);
                w.RefreshImage(ImageInfo.strFilesPath, 1, WinFunc.Image, new UInt16[] { 40, 40, 40 }, ColorRenderMode.Grayscale);
                w.RefreshImage(ImageInfo.strFilesPath, 2, WinFunc.Image, new UInt16[] { 77, 77, 77 }, ColorRenderMode.Grayscale);
                w.RefreshImage(ImageInfo.strFilesPath, 3, WinFunc.Image, new UInt16[] { 127, 127, 127 }, ColorRenderMode.Grayscale);
            }
            catch
            {
                System.Windows.MessageBox.Show("无数据!", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        /*显示光谱三维立方体*/
        private void btnShow3D_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MultiFuncWindow w = new MultiFuncWindow();
                w = (MultiFuncWindow)App.global_Windows[3];
                w.DisplayMode = GridMode.One;
                if (!w.isShow)
                    w.ScreenShow(Screen.AllScreens, 1, "光谱三维立方体");
                w.Refresh(ImageInfo.strFilesPath, 0, WinFunc.Cube);
            }
            catch
            {
                System.Windows.MessageBox.Show("数据出错!", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        /*还原默认值*/
        private void btnDefaultValue_Click(object sender, RoutedEventArgs e)
        {
            txtSingleR.Text = "40";
            txtSingleG.Text = "77";
            txtSingleB.Text = "127";
            txtSingleGray.Text = "40";
            txtCompareR.Text = "40";
            txtCompareG.Text = "77";
            txtCompareB.Text = "127";
            txtCompareGray2.Text = "40";
            txtCompareGray3.Text = "77";
            txtCompareGray4.Text = "127";
        }
        /*设置单谱段图像谱段*/
        private void btnSingleSet_Click(object sender, RoutedEventArgs e)
        {
            UInt16[] band = new UInt16[3];
            try
            { 
                if (rb1Single.IsChecked == true)
                {
                    if (Convert.ToDouble(txtSingleR.Text) > 160)
                        band[0] = ImageInfo.getBand(Convert.ToDouble(txtSingleR.Text));
                    else
                        band[0] = Convert.ToUInt16(txtSingleR.Text);
                    if (Convert.ToDouble(txtSingleG.Text) > 160)
                        band[1] = ImageInfo.getBand(Convert.ToDouble(txtSingleG.Text));
                    else
                        band[1] = Convert.ToUInt16(txtSingleG.Text);
                    if (Convert.ToDouble(txtSingleB.Text) > 160)
                        band[2] = ImageInfo.getBand(Convert.ToDouble(txtSingleB.Text));
                    else
                        band[2] = Convert.ToUInt16(txtSingleB.Text);
                    ((Ctrl_ImageView)((MultiFuncWindow)App.global_Windows[0]).UserControls[0]).RefreshPseudoColor(0,ImageInfo.strFilesPath, 4, band, ColorRenderMode.ArtColor);
                }
                else
                {
                    if (Convert.ToDouble(txtSingleGray.Text) > 160)
                        band[0] = band[1] = band[2] = ImageInfo.getBand(Convert.ToDouble(txtSingleGray.Text));
                    else
                        band[0] = band[1] = band[2] = Convert.ToUInt16(txtSingleGray.Text);
                    ((Ctrl_ImageView)((MultiFuncWindow)App.global_Windows[0]).UserControls[0]).RefreshPseudoColor(0,ImageInfo.strFilesPath, 4, band, ColorRenderMode.Grayscale);
                }
            }
            catch
            {
                System.Windows.MessageBox.Show("窗体未初始化！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        /*设置对比图像谱段*/
        private void btnCompareSet_Click(object sender, RoutedEventArgs e)
        {
            UInt16[] band = new UInt16[3];
            if (Convert.ToDouble(txtCompareR.Text) > 160)
                band[0] = ImageInfo.getBand(Convert.ToDouble(txtCompareR.Text));
            else
                band[0] = Convert.ToUInt16(txtCompareR.Text);
            if (Convert.ToDouble(txtCompareG.Text) > 160)
                band[1] = ImageInfo.getBand(Convert.ToDouble(txtCompareG.Text));
            else
                band[1] = Convert.ToUInt16(txtCompareG.Text);
            if (Convert.ToDouble(txtCompareB.Text) > 160)
                band[2] = ImageInfo.getBand(Convert.ToDouble(txtCompareB.Text));
            else
                band[2] = Convert.ToUInt16(txtCompareB.Text);
            try
            {
                if (ckb1sub.IsChecked == true)
                    ((Ctrl_ImageView)((MultiFuncWindow)App.global_Windows[1]).UserControls[0]).RefreshPseudoColor(0,ImageInfo.strFilesPath, 4, new UInt16[] { band[0], band[1], band[2] }, ColorRenderMode.ArtColor);
                if (ckb2sub.IsChecked == true)
                {
                    if (Convert.ToDouble(txtCompareGray2.Text) > 160)
                        band[0] = band[1] = band[2] = ImageInfo.getBand(Convert.ToDouble(txtCompareGray2.Text));
                    else
                        band[0] = band[1] = band[2] = Convert.ToUInt16(txtCompareGray2.Text);
                    ((Ctrl_ImageView)((MultiFuncWindow)App.global_Windows[1]).UserControls[1]).RefreshPseudoColor(1,ImageInfo.strFilesPath, 4, new UInt16[] { band[0], band[1] ,band[2] }, ColorRenderMode.Grayscale);
                }
                if (ckb3sub.IsChecked == true)
                {
                    if (Convert.ToDouble(txtCompareGray3.Text) > 160)
                        band[0] = band[1] = band[2] = ImageInfo.getBand(Convert.ToDouble(txtCompareGray3.Text));
                    else
                        band[0] = band[1] = band[2] = Convert.ToUInt16(txtCompareGray3.Text);
                    ((Ctrl_ImageView)((MultiFuncWindow)App.global_Windows[1]).UserControls[2]).RefreshPseudoColor(2,ImageInfo.strFilesPath, 4, new UInt16[] { band[0], band[1], band[2] }, ColorRenderMode.Grayscale);
                }
                if (ckb4sub.IsChecked == true)
                {
                    if (Convert.ToDouble(txtCompareGray4.Text) > 160)
                        band[0] = band[1] = band[2] = ImageInfo.getBand(Convert.ToDouble(txtCompareGray4.Text));
                    else
                        band[0] = band[1] = band[2] = Convert.ToUInt16(txtCompareGray4.Text);
                    ((Ctrl_ImageView)((MultiFuncWindow)App.global_Windows[1]).UserControls[3]).RefreshPseudoColor(3,ImageInfo.strFilesPath, 4, new UInt16[] { band[0], band[1], band[2] }, ColorRenderMode.Grayscale);
                }
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show("窗体未初始化！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        /*设置曲线模式*/
        private void rbChartMode_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Controls.RadioButton rb = sender as System.Windows.Controls.RadioButton;

                if (rb.Name == "rbChartMode1")
                {
                    ImageInfo.chartMode = true;
                    ImageInfo.chartShowCnt = 0;
                }
                else if (rb.Name == "rbChartMode4")
                {
                    ImageInfo.chartMode = false;
                    ImageInfo.chartShowCnt = 0;
                }

                if (((MultiFuncWindow)App.global_Windows[5]).UserControls[0] != null)
                {
                    if (ImageInfo.chartMode)
                    {
                        ((MultiFuncWindow)App.global_Windows[5]).DisplayMode = GridMode.One;
                        ((MultiFuncWindow)App.global_Windows[5]).Refresh(null, 0, WinFunc.Curve);
                    }
                    else
                    {
                        ((MultiFuncWindow)App.global_Windows[5]).DisplayMode = GridMode.Four;
                        ((MultiFuncWindow)App.global_Windows[5]).Refresh(null, 0, WinFunc.Curve);
                        ((MultiFuncWindow)App.global_Windows[5]).Refresh(null, 1, WinFunc.Curve);
                        ((MultiFuncWindow)App.global_Windows[5]).Refresh(null, 2, WinFunc.Curve);
                        ((MultiFuncWindow)App.global_Windows[5]).Refresh(null, 3, WinFunc.Curve);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("数据出错!", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        /*3D设置*/
        private void btn3Dset_Click(object sender, RoutedEventArgs e)
        {
            if (cb3Dfrm.IsChecked != true && cb3Dband.IsChecked != true)
                return;
            if(App.global_Windows[3]!=null)
            {
                var w = (MultiFuncWindow)(App.global_Windows[3]);
                if (w.UserControls[0] != null)
                {
                    var u = (Ctrl_3DView)(w.UserControls[0]);
                    if (cb3Dfrm.IsChecked == true && txt3Dfrm.Text != "")
                        u.showSingleFrm(Convert.ToUInt16(txt3Dfrm.Text));
                    if (cb3Dband.IsChecked == true && txt3Dband.Text != "")
                        u.pickSingleImage(Convert.ToUInt16(txt3Dband.Text));
                }
            }
        }
        #endregion

        #region 异常检测
        /*获取异常信息*/
        Thread _threadAbn;
        private void btnAbnGet_Click(object sender, RoutedEventArgs e)
        {
            if (btnAbnGet.Content.ToString() == "异常检测")
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

                _threadAbn = new Thread(() =>
                {
                    for (int i = 0; i < 160; i++)
                    {
                        if (ImageInfo.ImgDetectAbnormal(i, 128) < 0)
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
                    Dispatcher.Invoke(new Action(() =>
                    {
                        btnAbnGet.Content = "异常检测";
                    }));
                    System.Windows.MessageBox.Show("完成!", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                });
                _threadAbn.Start();
                btnAbnGet.Content = "停止检测";
            }
            else
            {
                _threadAbn.Abort();
                pgbDetectAbnormal.Value = 0;
                btnAbnGet.Content = "异常检测";
            }
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
                    txtModelStartTime.Text = (sel.Row[3].ToString() == "") ? "" : Convert.ToDateTime(sel.Row[3]).ToString("yyyy-MM-dd HH:mm:ss");            /*起始时间*/
                    txtModelEndTime.Text = (sel.Row[4].ToString() == "") ? "" : Convert.ToDateTime(sel.Row[4]).ToString("yyyy-MM-dd HH:mm:ss");              /*结束时间*/
                    txtModelStartLon.Text = (sel.Row[5].ToString() == "") ? "" : sel.Row[5].ToString();                                 /*起始经度*/
                    txtModelEndLon.Text = (sel.Row[6].ToString() == "") ? "" : sel.Row[6].ToString();                                   /*结束经度*/
                    txtModelStartLat.Text = (sel.Row[7].ToString() == "") ? "" : sel.Row[7].ToString();                                 /*起始纬度*/
                    txtModelEndLat.Text = (sel.Row[8].ToString() == "") ? "" : sel.Row[8].ToString();                                   /*结束纬度*/
                    cbModelImage1.IsChecked = (sel.Row[9].ToString() == "True");                                                        /*图像模式1*/
                    cbModelImage4.IsChecked = (sel.Row[10].ToString() == "True");                                                        /*图像模式4*/
                    cbModelMap.IsChecked = (sel.Row[11].ToString() == "True");                                                          /*谷歌地图*/
                    cbModel3D.IsChecked = (sel.Row[12].ToString() == "True");                                                           /*三维立方体*/
                    cbModelImageMap.IsChecked = (sel.Row[13].ToString() == "True");                                                     /*图像/地图模式*/
                    cbModelCurve.IsChecked = (sel.Row[14].ToString() != "False");                                                       /*曲线模式*/
                    rbModelCurve1.IsChecked = (sel.Row[14].ToString() != "模式4");                                                      /*曲线模式*/
                    rbModelCurve4.IsChecked = (sel.Row[14].ToString() == "模式4");                                                      /*曲线模式*/
                    txtModelRemark.Text = sel.Row[15].ToString();                                                                       /*备注*/

                    ModelShowInfo.Time_Start = Convert.ToDateTime(txtModelStartTime.Text);
                    ModelShowInfo.Time_End = Convert.ToDateTime(txtModelEndTime.Text);
                    ModelShowInfo.Coord_TL.Lon = Convert.ToDouble(txtModelStartLon.Text);
                    ModelShowInfo.Coord_TL.Lat = Convert.ToDouble(txtModelStartLat.Text);
                    ModelShowInfo.Coord_DR.Lon = Convert.ToDouble(txtModelEndLon.Text);
                    ModelShowInfo.Coord_DR.Lat = Convert.ToDouble(txtModelEndLat.Text);
                    ModelShowInfo.imgWidth = Convert.ToInt32(sel.Row[2]);
                    ModelShowInfo.isMakeImage = Convert.ToBoolean(sel.Row[17]);
                    ModelShowInfo.strFilesPath = sel.Row[18].ToString();
                    ModelShowInfo.MD5 = sel.Row[16].ToString();
                    btnModelMakeImage.IsEnabled = true;
                }
                else
                {
                    ModelShowInfo.WindowsCnt = 0;
                    ModelShowInfo.dtWinShowInfo = null;
                    btnModelMakeImage.IsEnabled = false;
                }
            }
            catch
            {
                System.Windows.MessageBox.Show("数据出错!", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        /*取当前图像信息*/
        private void btnModelGetCurImg_Click(object sender, RoutedEventArgs e)
        {
            txtModelStartTime.Text = ImageInfo.startTime.ToString("yyyy-MM-dd HH:mm:ss");
            txtModelEndTime.Text = ImageInfo.endTime.ToString("yyyy-MM-dd HH:mm:ss");
            txtModelStartLat.Text = ImageInfo.startCoord.Lat.ToString();
            txtModelStartLon.Text = ImageInfo.startCoord.Lon.ToString();
            txtModelEndLat.Text = ImageInfo.endCoord.Lat.ToString();
            txtModelEndLon.Text = ImageInfo.endCoord.Lon.ToString();
        }
        /*设置数据*/
        private async void btnModelSetData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string sql = "select * from Apply_Model where 名称='" + txtModelName.Text + "'";
                DataTable dtModel = SQLiteFunc.SelectDTSQL(sql);
                if(dtModel.Rows.Count > 0)
                    if (System.Windows.MessageBox.Show("确认对该应用示范进行数据修改?(不可退回)", "提示", MessageBoxButton.YesNoCancel, MessageBoxImage.Information) != MessageBoxResult.Yes)
                        return;
                ModelShowInfo.Time_Start = Convert.ToDateTime(txtModelStartTime.Text);
                ModelShowInfo.Time_End = Convert.ToDateTime(txtModelEndTime.Text);
                ModelShowInfo.Coord_TL.Lon = Convert.ToDouble(txtModelStartLon.Text);
                ModelShowInfo.Coord_TL.Lat = Convert.ToDouble(txtModelStartLat.Text);
                ModelShowInfo.Coord_DR.Lon = Convert.ToDouble(txtModelEndLon.Text);
                ModelShowInfo.Coord_DR.Lat = Convert.ToDouble(txtModelEndLat.Text);
                DataTable dt = await DataProc.QueryResult(FileInfo.md5, ModelShowInfo.Time_Start != ModelShowInfo.Time_End, ModelShowInfo.Coord_TL.Lon != ModelShowInfo.Coord_DR.Lon && ModelShowInfo.Coord_TL.Lat != ModelShowInfo.Coord_DR.Lat, false, ModelShowInfo.Time_Start, ModelShowInfo.Time_End, 0, 0, ModelShowInfo.Coord_TL, ModelShowInfo.Coord_DR);
                ModelShowInfo.imgWidth = dt.Rows.Count;
                ModelShowInfo.isMakeImage = false;
                if (dtModel.Rows.Count == 0)
                {
                    SQLiteFunc.ExcuteSQL("insert into Apply_Model (图像帧数,名称,起始时间,结束时间,起始经度,结束经度,起始纬度,结束纬度,备注,MD5) values (?,'?','?','?',?,?,?,?,'?','?')",ModelShowInfo.imgWidth, txtModelName.Text, ModelShowInfo.Time_Start.ToString("yyyy-MM-dd HH:mm:ss"), ModelShowInfo.Time_End.ToString("yyyy-MM-dd HH:mm:ss"), 0, 0, 0, 0, txtModelRemark.Text,FileInfo.md5);
                }
                else
                {
                    SQLiteFunc.ExcuteSQL("update Apply_Model set 图像帧数=?,起始时间='?',结束时间='?',起始经度=?,结束经度=?,起始纬度=?,结束纬度=?,备注='?',MD5='?',是否已生成图像=? where 名称='?'", ModelShowInfo.imgWidth, ModelShowInfo.Time_Start.ToString("yyyy-MM-dd HH:mm:ss"), ModelShowInfo.Time_End.ToString("yyyy-MM-dd HH:mm:ss"), ModelShowInfo.Coord_TL.Lon, ModelShowInfo.Coord_DR.Lon, ModelShowInfo.Coord_TL.Lat, ModelShowInfo.Coord_DR.Lat, txtModelRemark.Text, FileInfo.md5, Convert.ToUInt16(ModelShowInfo.isMakeImage), txtModelName.Text);
                 }
                getCurApplyModel();
            }
            catch
            {
                System.Windows.MessageBox.Show("数据出错!", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                System.Windows.MessageBox.Show("数据出错!", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                System.Windows.MessageBox.Show("数据出错!", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                System.Windows.MessageBox.Show("数据出错!", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        /*准备应用图像*/
        private async void btnModelMakeImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(((DataRowView)dataGrid_ApplyModel.SelectedItem).Row[17].ToString() == "True")
                    if (System.Windows.MessageBox.Show("文件已生成过,确认要重新生成并覆盖？", "提示", MessageBoxButton.YesNoCancel, MessageBoxImage.Information) != MessageBoxResult.Yes)
                        return;
                ModelShowInfo.dtImgInfo = await DataProc.QueryResult(ModelShowInfo.MD5, ModelShowInfo.Time_Start != ModelShowInfo.Time_End, ModelShowInfo.Coord_TL.Lon != ModelShowInfo.Coord_DR.Lon && ModelShowInfo.Coord_TL.Lat != ModelShowInfo.Coord_DR.Lat, false, ModelShowInfo.Time_Start, ModelShowInfo.Time_End, 0, 0, ModelShowInfo.Coord_TL, ModelShowInfo.Coord_DR);
                if (ModelShowInfo.dtImgInfo.Rows.Count == 0)
                {
                    System.Windows.MessageBox.Show("无数据!", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                FolderBrowserDialog fbd = new FolderBrowserDialog();
                if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    ModelShowInfo.strFilesPath = fbd.SelectedPath + "\\" + txtModelName.Text + "\\";
                    if (!File.Exists(ModelShowInfo.strFilesPath))
                        Directory.CreateDirectory(ModelShowInfo.strFilesPath);
                    
                    if (ModelShowInfo.imgWidth != ModelShowInfo.dtImgInfo.Rows.Count)
                    {
                        ModelShowInfo.imgWidth = ModelShowInfo.dtImgInfo.Rows.Count;
                        SQLiteFunc.ExcuteSQL("update Apply_Model set 图像帧数=? where 名称='?'", ModelShowInfo.imgWidth, txtModelName.Text);
                    }
                    ModelShowInfo.MakeImage();
                    ModelShowInfo.isMakeImage = true;
                    SQLiteFunc.ExcuteSQL("update Apply_Model set 是否已生成图像=?,文件路径='?' where 名称='?'", Convert.ToUInt16(ModelShowInfo.isMakeImage), ModelShowInfo.strFilesPath, txtModelName.Text);
                    System.Windows.MessageBox.Show("OK");
                    getCurApplyModel();
                }
            }
            catch
            {
                System.Windows.MessageBox.Show("数据出错!", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        /*显示样式*/
        private async void btnModelShow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ModelShowInfo.isMakeImage)
                {
                    System.Windows.MessageBox.Show("请先准备图像!", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                DataTable dt = await DataProc.QueryResult(ModelShowInfo.MD5,ModelShowInfo.Time_Start != ModelShowInfo.Time_End, ModelShowInfo.Coord_TL.Lon != ModelShowInfo.Coord_DR.Lon && ModelShowInfo.Coord_TL.Lat != ModelShowInfo.Coord_DR.Lat, false, ModelShowInfo.Time_Start, ModelShowInfo.Time_End, 0, 0, ModelShowInfo.Coord_TL, ModelShowInfo.Coord_DR);
                dataGrid_Result.ItemsSource = dt.DefaultView;
                dataGrid_SatePose.ItemsSource = dt.DefaultView;
                ImageInfo.dtImgInfo = dt;
                ImageInfo.GetImgInfo();       /*存储图像信息*/
                SetImgInfo();
                ImageInfo.strFilesPath = ModelShowInfo.strFilesPath;
                initWindows(ImageInfo.strFilesPath, ModelShowInfo.WindowsCnt, ModelShowInfo.dtWinShowInfo);
            }
            catch
            {
                System.Windows.MessageBox.Show("数据出错!", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        #endregion

        #region 默认显示方式
        /*初始化显示窗体*/
        private void initWindows(string path, int winSum, DataTable dtShow)
        {
            MultiFuncWindow[] w = new MultiFuncWindow[6];
            for (int i = 0; i < 6; i++)
                w[i] = (MultiFuncWindow)App.global_Windows[i];

            if (dtShow.Rows[0][1].ToString() == "True")
            {
                w[0].DisplayMode = GridMode.One;
                if (!w[0].isShow)
                    w[0].ScreenShow(Screen.AllScreens, 0, "单谱段图像");
                w[0].RefreshImage(path, 0, WinFunc.Image, new UInt16[] { 40, 40, 40 }, ColorRenderMode.Grayscale);
            }
            if (dtShow.Rows[0][2].ToString() == "True")
            {
                w[1].DisplayMode = GridMode.Four;
                if (!w[1].isShow)
                    w[1].ScreenShow(Screen.AllScreens, 0, "典型谱段图像对比");
                w[1].RefreshImage(path, 0, WinFunc.Image, new UInt16[] { 40, 77, 127 }, ColorRenderMode.ArtColor);
                w[1].RefreshImage(path, 1, WinFunc.Image, new UInt16[] { 40, 40, 40 }, ColorRenderMode.Grayscale);
                w[1].RefreshImage(path, 2, WinFunc.Image, new UInt16[] { 77, 77, 77 }, ColorRenderMode.Grayscale);
                w[1].RefreshImage(path, 3, WinFunc.Image, new UInt16[] { 127, 127, 127 }, ColorRenderMode.Grayscale);
            }
            //谷歌地图
            if (dtShow.Rows[0][3].ToString() == "True")
            {
                w[2].DisplayMode = GridMode.One;
                if (!w[2].isShow)
                    w[2].ScreenShow(Screen.AllScreens, 2, "谷歌地图");
                w[2].Refresh(path, 0, WinFunc.Map);
            }
            //三维立方体
            if (dtShow.Rows[0][4].ToString() == "True")
            {
                w[3].DisplayMode = GridMode.One;
                if (!w[3].isShow)
                    w[3].ScreenShow(Screen.AllScreens, 1, "光谱三维立方体");
                w[3].Refresh(path, 0, WinFunc.Cube);
            }
            //图像地图
            if (dtShow.Rows[0][5].ToString() == "True")
            {
                w[4].DisplayMode = GridMode.Two;
                if (!w[4].isShow)
                    w[4].ScreenShow(Screen.AllScreens, 2, "图像/地图");
                UInt16[] colorBand = { 40, 77, 127 };
                w[4].RefreshImage(path, 0, WinFunc.Image, colorBand, ColorRenderMode.ArtColor);
                w[4].Refresh(path, 1, WinFunc.Map);
            }
            //曲线模式1
            if (dtShow.Rows[0][6].ToString() == "模式1")
            {
                w[5].DisplayMode = GridMode.One;
                if (!w[5].isShow)
                    w[5].ScreenShow(Screen.AllScreens, 1, "曲线");
                w[5].Refresh(path, 0, WinFunc.Curve);
                rbChartMode1.IsChecked = true;
            }
            //曲线模式4
            if (dtShow.Rows[0][6].ToString() == "模式4")
            {
                w[5].DisplayMode = GridMode.Four;
                if (!w[5].isShow)
                    w[5].ScreenShow(Screen.AllScreens, 1, "曲线");
                w[5].Refresh(path, 0, WinFunc.Curve);
                w[5].Refresh(path, 1, WinFunc.Curve);
                w[5].Refresh(path, 2, WinFunc.Curve);
                w[5].Refresh(path, 3, WinFunc.Curve);
                rbChartMode4.IsChecked = true;
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
                btnTopB.IsChecked = true;
                btnLeftB1.IsChecked = true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("数据出错!", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                System.Windows.MessageBox.Show("数据出错!", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        #endregion

        #region 默认数据设置
        //设置解压后默认存放路径
        private void btnSetDecPath_Click(object sender, RoutedEventArgs e)
        {
            if (txtSetDecPath.Text.Substring(txtSetDecPath.Text.Length - 1) != "\\")
                txtSetDecPath.Text = txtSetDecPath.Text + "\\";
            SQLiteFunc.ExcuteSQL("update Global set Variable='?' where ID=1", txtSetDecPath.Text);
        }
        #endregion
    }
}