﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Spectra
{
    /// <summary>
    /// MultiFuncWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MultiFuncWindow : Window
    {
        int WinIndex;
        public bool isShow = false;
        private GridMode _DisplayMode;
        public GridMode DisplayMode
        {
            get
            {
                return _DisplayMode;
            }
            set
            {
                _DisplayMode = value;
                switch (value)
                {
                    case GridMode.One:
                        {
                            this.MainGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                            this.MainGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                            this.MainGrid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Star);
                            this.MainGrid.RowDefinitions[1].Height = new GridLength(0, GridUnitType.Star);
                        }; break;
                    case GridMode.Two:
                        {
                            this.MainGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                            this.MainGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                            this.MainGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                            this.MainGrid.RowDefinitions[1].Height = new GridLength(0, GridUnitType.Star);
                        }; break;
                    case GridMode.Four:
                        {
                            this.MainGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                            this.MainGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                            this.MainGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                            this.MainGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
                        }; break;
                    default: break;
                }
            }
        }
        public WinFunc[] WinFunc = new WinFunc[4];
        public System.Windows.Controls.UserControl[] UserControls = new System.Windows.Controls.UserControl[4];
        public Grid[] SubGrid;

        public MultiFuncWindow()
        {
            InitializeComponent();
            SubGrid = new Grid[4] { SubGrid_1, SubGrid_2, SubGrid_3, SubGrid_4 };
            DisplayMode = GridMode.Two;
        }

        public void Refresh(string path, int SubGridInedex, WinFunc setWinFunc)
        {
            SubGrid[SubGridInedex].Children.Clear();
            WinFunc[SubGridInedex] = setWinFunc;

            switch (setWinFunc)
            {
                case Spectra.WinFunc.Image:
                    UserControls[SubGridInedex] = new Ctrl_ImageView();
                    ((Ctrl_ImageView)(UserControls[SubGridInedex])).Refresh(120, ColorRenderMode.Grayscale,path);
                    break;
                case Spectra.WinFunc.Curve:
                    UserControls[SubGridInedex] = new Ctrl_SpecCurv();
                    break;
                case Spectra.WinFunc.Cube:
                    UserControls[SubGridInedex] = new Ctrl_3DView();
                    ((Ctrl_3DView)UserControls[SubGridInedex]).Refresh(path);
                    break;
                case Spectra.WinFunc.Map:
                    break;
                default:
                    break;
            }
            SubGrid[SubGridInedex].Children.Add(UserControls[SubGridInedex]);
        }

        public void ScreenShow()
        {
            this.Show();
            this.isShow = true;
        }

        /*显示器对象 显示器编号   窗口编号*/
        public void ScreenShow(Screen[] s, int id,string strT)
        {
            //this.WindowState = WindowState.Maximized;
            //this.WindowStyle = WindowStyle.None;
            //this.Left = 1920;
            //this.Top = 0;
            //this.Width = 1024;
            //this.Height = 768;
            //this.Show();

            //System.Drawing.Rectangle r2 = s[0].WorkingArea;
            //this.Top = r2.Top;
            //this.Left = r2.Left;

            this.Title = strT;
            this.Show();
            this.isShow = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
            this.isShow = false;
        }
    }
}
