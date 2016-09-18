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
using Mapsui.Projection;
using Mapsui.Providers;
using Mapsui.Samples.Common;
using Mapsui.Samples.Common.Desktop;
using Mapsui.UI.Xaml;
using Mapsui.Geometries;
using BruTile;
using BruTile.Predefined;
using BruTile.FileSystem;
using BruTile.Cache;
using Mapsui.Layers;

namespace Spectra
{
    /// <summary>
    /// Ctrl_Map.xaml 的交互逻辑
    /// </summary>
    public partial class Ctrl_Map : UserControl
    {

       
        public Mapsui.Geometries.Point TopLeft = new Mapsui.Geometries.Point(0,0);
        public Mapsui.Geometries.Point BottomRight = new Mapsui.Geometries.Point(0,0);
        public System.Windows.Point TopLeftScreen = new System.Windows.Point(0, 0);
        public System.Windows.Point BottomRightScreen = new System.Windows.Point(0, 0);
        public bool selectionMode = false;
        public Ctrl_Map()
        {
            InitializeComponent();
            MapControl.Map.Layers.Clear();
            MapControl.Map.Layers.Add(MapTilerSample.CreateLayer());
            MapControl.ZoomToFullEnvelope();
            MapControl.Refresh();
        }

        public void ZoomToBox(System.Windows.Point start, System.Windows.Point End)
        {
            this.MapControl.ZoomToBox(Mapsui.Projection.SphericalMercator.FromLonLat(start.X,start.Y),Mapsui.Projection.SphericalMercator.FromLonLat(End.X,End.Y));
            this.MapControl.Refresh();
        }

        internal void Refresh()
        {
            
        }

        private void userControl_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            Mapsui.Geometries.Point p = this.MapControl.Map.Viewport.ScreenToWorld(e.GetPosition(MapControl).X, e.GetPosition(MapControl).Y);
            if (selectionMode == false)
            {
                this.TopLeftScreen = e.GetPosition(this.MapControl);
                this.MapControl.ZoomLocked = true;
                this.TopLeft = Mapsui.Projection.SphericalMercator.ToLonLat(p.X, p.Y);
                this.selectionNotice.Visibility = Visibility.Visible;
                this.selectionMode = true;
                lcLat.Visibility = Visibility.Collapsed;
                lcLon.Visibility = Visibility.Collapsed;
                cLT.Visibility = Visibility.Collapsed;
                cRB.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.BottomRightScreen = e.GetPosition(this.MapControl);
                this.SelectionArea.Margin = new Thickness(TopLeftScreen.X<=BottomRightScreen.X?TopLeftScreen.X:BottomRightScreen.X,TopLeftScreen.Y<=BottomRightScreen.Y?TopLeftScreen.Y:BottomRightScreen.Y,0,0);
                this.SelectionArea.Width = Math.Abs(BottomRightScreen.X - TopLeftScreen.X);
                this.SelectionArea.Height = Math.Abs(BottomRightScreen.Y - TopLeftScreen.Y);
                this.BottomRight = Mapsui.Projection.SphericalMercator.ToLonLat(p.X, p.Y);
                this.MapControl.ZoomLocked = false;
                this.selectionNotice.Visibility = Visibility.Collapsed;
                this.SelectionArea.Visibility = Visibility.Visible;
                this.selectionMode = false;
                MapInfo.LT_Coord.Lat = this.TopLeft.Y >= this.BottomRight.Y ? this.TopLeft.Y : this.BottomRight.Y;
                MapInfo.LT_Coord.Lon = this.TopLeft.X <= this.BottomRight.X ? this.TopLeft.X : this.BottomRight.X;
                MapInfo.RB_Coord.Lat = this.TopLeft.Y < this.BottomRight.Y ? this.TopLeft.Y : this.BottomRight.Y;
                MapInfo.RB_Coord.Lon = this.TopLeft.X > this.BottomRight.X ? this.TopLeft.X : this.BottomRight.X;
                lcLat.Visibility = Visibility.Visible;
                lcLon.Visibility = Visibility.Visible;
                cLT.Visibility = Visibility.Visible;
                cRB.Visibility = Visibility.Visible;
                cLT.Content = $"({MapInfo.LT_Coord.Lon},{MapInfo.LT_Coord.Lat})";
                cRB.Content = $"({MapInfo.RB_Coord.Lon},{MapInfo.RB_Coord.Lat})";
            }
        }

        private void MapControl_ViewChanged(object sender, ViewChangedEventArgs e)
        {
            this.SelectionArea.Visibility = Visibility.Collapsed;
            lcLat.Visibility = Visibility.Collapsed;
            lcLon.Visibility = Visibility.Collapsed;
            cLT.Visibility = Visibility.Collapsed;
            cRB.Visibility = Visibility.Collapsed;
        }

        private void userControl_MouseMove(object sender, MouseEventArgs e)
        {
            Mapsui.Geometries.Point p = this.MapControl.Map.Viewport.ScreenToWorld(e.GetPosition(MapControl).X, e.GetPosition(MapControl).Y);
            this.pLon.Content = (Mapsui.Projection.SphericalMercator.ToLonLat(p.X, p.Y).X).ToString();
            this.pLat.Content = (Mapsui.Projection.SphericalMercator.ToLonLat(p.X, p.Y).Y).ToString();
        }

        private void rdbMapModeRoad_Checked(object sender, RoutedEventArgs e)
        {
            MapInfo.MapPath = @"C:\Program Files\GMap\roadmap";
            MapInfo.MapType = "png";
            MapControl.Map.Layers.Clear();
            MapControl.Map.Layers.Add(MapTilerSample.CreateLayer());
            MapControl.Refresh();
        }

        private void rdbMapModeSat_Checked(object sender, RoutedEventArgs e)
        {
            MapInfo.MapPath = @"C:\Program Files\GMap\satellite";
            MapInfo.MapType = "jpg";
            MapControl.Map.Layers.Clear();
            MapControl.Map.Layers.Add(MapTilerSample.CreateLayer());
            MapControl.Refresh();
        }
    }
    public static class MapTilerSample
    {
        public static ILayer CreateLayer()
        {
            return new TileLayer(new MapTilerTileSource()) { Name = "True Marble in MapTiler" };
        }
    }
    public class MapTilerTileSource : ITileSource
    {
        public MapTilerTileSource()
        {
            Schema = GetTileSchema();
            Provider = GetTileProvider();
            Name = "MapTiler";
        }

        public ITileSchema Schema { get; }
        public string Name { get; }
        public ITileProvider Provider { get; }

        public byte[] GetTile(TileInfo tileInfo)
        {
            try
            {
                return Provider.GetTile(tileInfo);
            }
            catch { return null; }
        }

        public static ITileProvider GetTileProvider()
        {
            return new FileTileProvider(new FileCache(MapInfo.MapPath, MapInfo.MapType));
        }

        public static ITileSchema GetTileSchema()
        {
            var schema = new GlobalSphericalMercator(YAxis.OSM);
            schema.Resolutions.Clear();
            schema.Resolutions["0"] = new Resolution("0", 156543.033900000);
            schema.Resolutions["1"] = new Resolution("1", 78271.51695);
            schema.Resolutions["2"] = new Resolution("2", 39135.758475);
            schema.Resolutions["3"] = new Resolution("3", 19567.8792375);
            schema.Resolutions["4"] = new Resolution("4", 9783.93961875);
            schema.Resolutions["5"] = new Resolution("5", 4891.969809375);
            schema.Resolutions["6"] = new Resolution("6", 2445.9849046875);
            schema.Resolutions["7"] = new Resolution("7", 1222.99245234375);
            schema.Resolutions["8"] = new Resolution("8", 611.496226171875);
            schema.Resolutions["9"] = new Resolution("9", 305.748113085938);
            schema.Resolutions["10"] = new Resolution("10", 152.874056542969);
            schema.Resolutions["11"] = new Resolution("11", 76.4370282714844);
            schema.Resolutions["12"] = new Resolution("12", 38.2185141357422);
            schema.Resolutions["13"] = new Resolution("13", 19.1092570678711);
            schema.Resolutions["14"] = new Resolution("14", 9.55462853393555);
            schema.Resolutions["15"] = new Resolution("15", 4.77731426696777);
            schema.Resolutions["16"] = new Resolution("16", 2.38865713348389);
            schema.Resolutions["17"] = new Resolution("17", 1.19432856674194);

            return schema;
        }

        private static string GetAppDir()
        {
            return System.IO.Path.GetDirectoryName(
              System.Reflection.Assembly.GetEntryAssembly().GetModules()[0].FullyQualifiedName);
        }
    }
    public class MouseTrackerDecorator : Decorator
    {
        static readonly DependencyProperty MousePositionProperty;
        static MouseTrackerDecorator()
        {
            MousePositionProperty = DependencyProperty.Register("MousePosition", typeof(System.Windows.Point), typeof(MouseTrackerDecorator));
        }

        public override UIElement Child
        {
            get
            {
                return base.Child;
            }
            set
            {
                if (base.Child != null)
                    base.Child.MouseMove -= _controlledObject_MouseMove;
                base.Child = value;
                base.Child.MouseMove += _controlledObject_MouseMove;
            }
        }

        public System.Windows.Point MousePosition
        {
            get
            {
                return (System.Windows.Point)GetValue(MouseTrackerDecorator.MousePositionProperty);
            }
            set
            {
                SetValue(MouseTrackerDecorator.MousePositionProperty, value);
            }
        }

        void _controlledObject_MouseMove(object sender, MouseEventArgs e)
        {
            System.Windows.Point p = e.GetPosition(base.Child);

            // Here you can add some validation logic
            MousePosition = p;
        }
    }
}
