using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Spectra
{
    /// <summary>
    /// SpecImgWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SpecImgWindow : Window
    {
        public Ctrl_ImageView[] u = new Ctrl_ImageView[4];
        List<Bitmap> bmp_Buf = new List<Bitmap>();
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
                            this.grid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                            this.grid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                            this.grid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Star);
                            this.grid.RowDefinitions[1].Height = new GridLength(0, GridUnitType.Star);
                        }; break;
                    case GridMode.Two:
                        {
                            this.grid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                            this.grid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                            this.grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                            this.grid.RowDefinitions[1].Height = new GridLength(0, GridUnitType.Star);
                        }; break;
                    case GridMode.Four:
                        {
                            this.grid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                            this.grid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                            this.grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                            this.grid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
                        }; break;
                    default: break;
                }
            }
        }
        public SpecImgWindow()
        {
            InitializeComponent();
            u[0] = ImageView_A;
            u[1] = ImageView_B;
            u[2] = ImageView_C;
            u[3] = ImageView_D;
            u[0].ScreenIndex = 0;
            u[1].ScreenIndex = 1;
            u[2].ScreenIndex = 2;
            u[3].ScreenIndex = 3;
            DisplayMode = GridMode.One;
        }
        public enum GridMode { One, Two, Three, Four };


        private void Window_Closed(object sender, EventArgs e)
        {
            this.Hide();
        }
    }
}
