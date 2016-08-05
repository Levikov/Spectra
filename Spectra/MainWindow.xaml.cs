using Microsoft.Win32;
using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

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
            TextBox txt = sender as TextBox;
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
            OpenFileDialog openFile = new OpenFileDialog();
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
                if (MessageBox.Show("该文件已解压,是否要重新解压并覆盖?", "提示", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.Cancel) return;
            Button b = sender as Button;
            IProgress<double> IProgress_Prog = new Progress<double>((ProgressValue) => { prog_Import.Value = ProgressValue * this.prog_Import.Maximum; });
            IProgress<string> IProgress_List = new Progress<string>((ProgressString) => { this.tb_Console.Text = ProgressString + "\n"+ this.tb_Console.Text; });
            this.b_Abort_Import.IsEnabled = true;
            this.b_Start_Import.IsEnabled = false;
            this.b_Open_Import.IsEnabled = false;
            string result = await DataProc.Import_5(IProgress_Prog, IProgress_List, cancelImport.Token);
            MessageBox.Show(result);
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
                if (MessageBox.Show("确认删除该文件的记录?", "提示", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.Cancel) return;
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
            }
            catch (Exception E)
            {
                MessageBox.Show(E.Message);
            }

            MessageBox.Show("显示完成！");
        }
        /*选择时间条件*/
        private void dtp_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox dp = sender as TextBox;
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
            TextBox dp = sender as TextBox;
            switch (dp.Name)
            {
                case "LT_Lat": { double.TryParse(((TextBox)sender).Text, out tl_lat); Coord_TL.Lat = tl_lat; } break;
                case "LT_Lon": { double.TryParse(((TextBox)sender).Text, out tl_lon); Coord_TL.Lon = tl_lon; } break;
                case "RB_Lat": { double.TryParse(((TextBox)sender).Text, out rb_lat); Coord_DR.Lat = rb_lat; } break;
                case "RB_Lon": { double.TryParse(((TextBox)sender).Text, out rb_lon); Coord_DR.Lon = rb_lon; } break;
                default: break;
            }
        }
        #endregion

        #region 图像显示
        /*显示图像按钮*/
        private void button_Display_Click(object sender, RoutedEventArgs e)
        {
            DataTable dt = DataQuery.QueryResult;
            //App.global_Win_Map.DrawRectangle(new Point((double)DataQuery.QueryResult.Rows[0].ItemArray[3], (double)DataQuery.QueryResult.Rows[0].ItemArray[4]), new Point((double)DataQuery.QueryResult.Rows[DataQuery.QueryResult.Rows.Count - 1].ItemArray[3], (double)DataQuery.QueryResult.Rows[DataQuery.QueryResult.Rows.Count - 1].ItemArray[4]));
            foreach (MultiFuncWindow w in App.global_Windows)
            {
                w.Show();
                w.Refresh(0, WinFunc.Image);
                w.Refresh(1, WinFunc.Curve);
            }


        }
        /*设置图像谱段按钮*/
        private void button_SetImage_Click(object sender, RoutedEventArgs e)
        {
            long ScreenCnt;
            int SelectedScreen;
            int band;
            long.TryParse(tb_ScreenCnt.Text, out ScreenCnt);
            int.TryParse(tb_ScreenIndex.Text, out SelectedScreen);
            int.TryParse(tb_SpecIndex.Text, out band);
            SelectedScreen -= 1;
            ScreenCnt -= 1;
            App.global_Win_SpecImg.DisplayMode = (SpecImgWindow.GridMode)ScreenCnt;
            App.global_Win_Curve.DisplayMode = (SpecCurvWindow.GridMode)ScreenCnt;
            App.global_Win_SpecImg.u[SelectedScreen].Refresh(band,ColorRenderMode.Grayscale);
        }
        #endregion



        private void button_Clear_Result_Click(object sender, RoutedEventArgs e)
        {
            DataQuery.QueryResult.Clear();
            dataGrid_Result.ItemsSource = null;
        }

        private void button_SetScreen_Click(object sender, RoutedEventArgs e)
        {
            foreach (MultiFuncWindow w in App.global_Windows)
            {
                w.DisplayMode = (GridMode)(Combo_ScreenCnt.SelectedIndex);
                w.Refresh(Combo_ScreenIndex.SelectedIndex,(WinFunc)Combo_ScreenType.SelectedIndex);
            }
        }

        private void button_SetBand_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
