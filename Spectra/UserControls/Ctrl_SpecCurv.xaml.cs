using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Research.DynamicDataDisplay.PointMarkers;
using System;
using System.Windows.Media;
using System.IO;
using System.Windows;
using System.Windows.Controls;

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
        }

        public LineAndMarker<MarkerPointsGraph> lm = new LineAndMarker<MarkerPointsGraph>();
        ObservableDataSource<System.Windows.Point> dtsChart1st = new ObservableDataSource<System.Windows.Point>();

        public void initChart(string desp)
        {
            //chart1st.AddLineGraph(dtsChart1st, Colors.DeepSkyBlue, 2, "Sin");
            lm = chart1st.AddLineGraph(dtsChart1st,
                new System.Windows.Media.Pen(Brushes.Yellow, 2),
                new CirclePointMarker { Size = 2.0, Fill = Brushes.Yellow },
                new PenDescription(desp));
            chart1st.Viewport.Visible = new Rect(new System.Windows.Point(400, 4096), new System.Windows.Point(1020, 0));
        }

        public void initChart(Pen pen ,string desp)
        {
            lm = chart1st.AddLineGraph(dtsChart1st,
                pen,
                new CirclePointMarker { Size = 2.0, Fill = pen.Brush },
                new PenDescription(desp));
            chart1st.Viewport.Visible = new Rect(new System.Windows.Point(400, 4096), new System.Windows.Point(1020, 0));
        }

        public void Draw1(Point p, Coord coo, int index)
        {
            Point pp = new Point(p.Y, p.X);
            Point[] points = GetPoint(pp);
            dtsChart1st = new ObservableDataSource<System.Windows.Point>();
            foreach (System.Windows.Point point in points)
            {
                dtsChart1st.AppendAsync(base.Dispatcher, point);
            }
            initChart(getPen(index),$"({coo.Lon.ToString("F4")},{coo.Lat.ToString("F4")})");
        }

        public void Draw4(Point p, Coord coo)
        {
            Point pp = new Point(p.Y, p.X);
            Point[] points = GetPoint(pp);
            dtsChart1st = new ObservableDataSource<System.Windows.Point>();
            foreach (System.Windows.Point point in points)
            {
                dtsChart1st.AppendAsync(base.Dispatcher, point);
            }
            chart1st.Children.Remove(lm.LineGraph);
            chart1st.Children.Remove(lm.MarkerGraph);
            initChart($"({coo.Lon.ToString("F4")},{coo.Lat.ToString("F4")})");
        }

        public Pen getPen(int index)
        {
            Pen pen = new Pen();
            pen.Thickness = 2;
            switch (index)
            {
                case 0:
                    pen.Brush = Brushes.Yellow;
                    break;
                case 1:
                    pen.Brush = Brushes.Aqua;
                    break;
                case 2:
                    pen.Brush = Brushes.Salmon;
                    break;
                case 3:
                    pen.Brush = Brushes.Brown;
                    break;
                case 4:
                    pen.Brush = Brushes.YellowGreen;
                    break;
                case 5:
                    pen.Brush = Brushes.Red;
                    break;
                case 6:
                    pen.Brush = Brushes.Green;
                    break;
                case 7:
                    pen.Brush = Brushes.HotPink;
                    break;
                case 8:
                    pen.Brush = Brushes.BlueViolet;
                    break;
                case 9:
                    pen.Brush = Brushes.LightGreen;
                    break;
                default:
                    pen.Brush = Brushes.Orange;
                    break;
            }
            return pen;
        }

        public Point[] GetPoint(Point p)
        {

            Point[] points = new Point[149];
            FileStream[] file = new FileStream[149];
            byte[] buf = new byte[2];
            for (int i = 6; i < 155; i++)
            {
                if (File.Exists($"{ImageInfo.strFilesPath}{(int)p.Y / 4096}\\{i}.raw"))
                {
                    file[i - 6] = new FileStream($"{ImageInfo.strFilesPath}{(int)p.Y / 4096}\\{i}.raw", FileMode.Open, FileAccess.Read, FileShare.Read);
                    file[i - 6].Seek(((int)p.X + (int)p.Y % 4096 * 2048) * 2, 0);
                    file[i - 6].Read(buf,0,2);
                    file[i - 6].Close();
                    points[i - 6].X =Convert.ToDouble(ImageInfo.dtBandWave.Rows[i][1]);
                    points[i - 6].Y = buf[0] + buf[1] * 256;
                }
            }
            return points;
        }
    }
}
