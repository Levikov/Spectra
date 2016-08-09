using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Research.DynamicDataDisplay.PointMarkers;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;

namespace Spectra
{
    /// <summary>
    /// Ctrl_SpecCurv.xaml 的交互逻辑
    /// </summary>
    public partial class Ctrl_SpecCurv : UserControl
    {
        public int ScreenIndex;
        public Ctrl_SpecCurv()
        {
            InitializeComponent();
            initChart();
        }
        public int yChart1st = 0;
        public LineAndMarker<MarkerPointsGraph> lm = new LineAndMarker<MarkerPointsGraph>();
        ObservableDataSource<System.Windows.Point> dtsChart1st = new ObservableDataSource<System.Windows.Point>();

        public void initChart()
        {
            //chart1st.AddLineGraph(dtsChart1st, Colors.DeepSkyBlue, 2, "Sin");
            lm = chart1st.AddLineGraph(dtsChart1st,
            new System.Windows.Media.Pen(System.Windows.Media.Brushes.Yellow, 3),
            new CirclePointMarker { Size = 3.0, Fill = System.Windows.Media.Brushes.Yellow },
            new PenDescription());
        }

        internal async void Draw(System.Windows.Point p)
        {
            System.Windows.Point[] points = await SpecProc.GetSpecCurv(p);
            dtsChart1st = new ObservableDataSource<System.Windows.Point>();
            foreach (System.Windows.Point point in points)
            {
                dtsChart1st.AppendAsync(base.Dispatcher, point);
            }
            chart1st.Children.Remove(lm.LineGraph);
            chart1st.Children.Remove(lm.MarkerGraph);
            initChart();
            chart1st.Viewport.Visible = new Rect(new System.Windows.Point(400,4096), new System.Windows.Point(1020, 0));
        }
    }
}
