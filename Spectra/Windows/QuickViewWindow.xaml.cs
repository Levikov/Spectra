using System;
using System.Collections.Generic;
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
    /// QuickViewWindow.xaml 的交互逻辑
    /// </summary>
    public partial class QuickViewWindow : Window
    {
        public QuickViewWindow(int subSum)
        {
            InitializeComponent();

            int subCnt = 0;
            while (subCnt++ < subSum)
            {
                Image img = new Image();
                img.Margin = new Thickness(1, 1, 0, 0);
                //img.Source = new BitmapImage(new Uri($"{Environment.CurrentDirectory}\\decFiles\\BF-22-A9-CE-42-2C-B6-1A-E7-D5-A3-3F-F4-22-9A-A6\\0.bmp"));
                img.Source = new BitmapImage(new Uri("C:\\Users\\wennyoyo\\Downloads\\1.jpg"));
                AddInfo(img,subCnt.ToString());
                DynamicAdd(panelBack, img);
            }
        }

        public void DynamicAdd(Panel container, UIElement control)
        {
            container.Children.Add(control);
        }

        public void AddInfo(Image container,string strInfo)
        {
            container.ToolTip = strInfo + "\n" + strInfo + "\n" + strInfo + "\n" + strInfo + "\n" + strInfo + "\n" + strInfo + "\n" + strInfo;
        }

        private void winQuickView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var size = e.NewSize;
            panelBack.Height = size.Height - 55;
            panelBack.ItemHeight = panelBack.Height / 4;// (panelBack.Height - 50) / 4;
            panelBack.ItemWidth = panelBack.ItemHeight * 2;
        }
    }
}
