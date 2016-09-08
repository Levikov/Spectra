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
        public Ctrl_Map()
        {
            InitializeComponent();
            MapControl.Map.Layers.Clear();
            MapControl.Map.Layers.Add(MapTilerSample.CreateLayer());
            MapControl.ZoomToFullEnvelope();
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
            return Provider.GetTile(tileInfo);
        }

        public static ITileProvider GetTileProvider()
        {
            return new FileTileProvider(new FileCache(MapInfo.RoadMapPath, "png"));
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
}
