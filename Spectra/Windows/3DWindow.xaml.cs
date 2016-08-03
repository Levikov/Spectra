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
    /// _3DWindow.xaml 的交互逻辑
    /// </summary>
    public partial class _3DWindow : Window
    {
        public _3DWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ImageInfo _3DimgInfo = new ImageInfo();
            labelWidth.DataContext = _3DimgInfo;
        }
    }
}
