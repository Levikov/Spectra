using System;
using System.Drawing;
using System.IO;
using System.Windows;

namespace Spectra
{
    /// <summary>
    /// MapWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MapWindow : Window
    {
        public static String pathMap = "file://127.0.0.1/E$/Map/Amap.html?";
        public System.Windows.Point start;
        public System.Windows.Point end;

        public MapWindow()
        {
            InitializeComponent();
        }

        public MapWindow(Rectangle rectangle)
        {
            InitializeComponent();
            this.Top = rectangle.Y;
            this.Left = rectangle.X;
        }

        public void DrawRectangle(System.Windows.Point Start, System.Windows.Point End)
        {
            string[] line = File.ReadAllLines(@"E:\Map\Amap.html");
            File.WriteAllText(@"E:\Map\Amap.html", File.ReadAllText(@"E:\Map\Amap.html").Replace(line[166], $"  <div id=\"map_canvas\" style=\"position: absolute; left: 0px; top: 0px; width: 100%; height: {this.webMap.ActualHeight}px; background-color: rgb(229, 227, 223); overflow: hidden;\"></div>"));
            this.start = Start;
            this.end = End;
            Uri uri = new Uri($"{pathMap}lat={0.5 * Start.X + 0.5 * End.X}&lon={0.5 * Start.Y + 0.5 * End.Y}&start_lat={Start.X}&start_lon={Start.Y}&end_lat={End.X}&end_lon={End.Y}");
            webMap.Navigate(uri);
        }

        private void windowMap_Loaded(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri(pathMap);
            webMap.Navigate(uri);
        }
        private void windowMap_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
        }

        private void webMap_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Uri uri = new Uri($"{pathMap}&lat={0.5 * start.X + 0.5 * end.X}&lon={0.5 * start.Y + 0.5 * end.Y}&start_lat={start.X}&start_lon={start.Y}&end_lat={end.X}&end_lon={end.Y}");
            string[] line = File.ReadAllLines(@"E:\Map\Amap.html");
            File.WriteAllText(@"E:\Map\Amap.html", File.ReadAllText(@"E:\Map\Amap.html").Replace(line[166], $"  <div id=\"map_canvas\" style=\"position: absolute; left: 0px; top: 0px; width: 100%; height: {this.webMap.ActualHeight}px; background-color: rgb(229, 227, 223); overflow: hidden;\"></div>"));
            webMap.Navigate(uri);
        }
    }
}
