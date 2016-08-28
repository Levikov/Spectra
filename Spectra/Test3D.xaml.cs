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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using WFTools3D;
namespace Spectra
{
    /// <summary>
    /// Test3D.xaml 的交互逻辑
    /// </summary>
    public partial class Test3D : Window
    {
        double imheight = 60;
        public Test3D()
        {
            InitializeComponent();
            GeometryModel3D gm3d_Top =Resources["Top"] as GeometryModel3D;
            GeometryModel3D gm3d_Bottom = Resources["Bottom"] as GeometryModel3D;
            GeometryModel3D gm3d_Up = Resources["Up"] as GeometryModel3D;
            GeometryModel3D gm3d_Down = Resources["Down"] as GeometryModel3D;
            GeometryModel3D gm3d_Left = Resources["Left"] as GeometryModel3D;
            GeometryModel3D gm3d_Right = Resources["Right"] as GeometryModel3D;
            GeometryModel3D gm3d_Active = Resources["Active"] as GeometryModel3D;
            ModelVisual3D mv3d = new ModelVisual3D();
            Model3DGroup m3dg = new Model3DGroup();
            m3dg.Children.Add(gm3d_Top);
            m3dg.Children.Add(gm3d_Bottom);
            m3dg.Children.Add(gm3d_Up);
            m3dg.Children.Add(gm3d_Down);
            m3dg.Children.Add(gm3d_Left);
            m3dg.Children.Add(gm3d_Right);
            m3dg.Children.Add(gm3d_Active);
            mv3d.Content = m3dg;
            scene.Viewport.Children.Add(mv3d);






            double scalar = 0.1;
            scene.Camera.Position = new Point3D(scalar * 2048, scalar * imheight, scalar * 2048);
            scene.Camera.LookDirection = new Vector3D(-2048, -imheight, -2048);
            scene.Camera.UpDirection = Math3D.UnitY;
            scene.Camera.FieldOfView = 60;
            scene.Camera.Speed = 0;

        }
        void SetCamera(Point3D Position,Vector3D LookDirection)
        {
            scene.Camera.Position = Position;
            scene.Camera.LookDirection = LookDirection;
            scene.Camera.UpDirection = Math3D.UnitY;
            scene.Camera.FieldOfView = 60;
            scene.Camera.Speed = 0;
        }

        void Refresh(double height)
        {

        }
        private void scene_MouseDown(object sender, MouseButtonEventArgs e)
        {
            

        }

        private void scene_MouseMove(object sender, MouseEventArgs e)
        {
            

        }

        private void scene_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            int X = (int)(scene.touchPoint.X);
            int Y = (int)(scene.touchPoint.Y);
            int Z = (int)(scene.touchPoint.Z);
            if (scene.touchPoint.X != 0 || scene.touchPoint.Y != 0 || scene.touchPoint.Z != 0)
            {
                DoubleAnimation day = new DoubleAnimation { From = 0, To = 60, Duration = new Duration(TimeSpan.FromSeconds(1)) };
                TranslateTransform3D transtrans3d = new TranslateTransform3D(0, 0, scene.touchPoint.Z);
                ((GeometryModel3D)(FindName("gm3d_Active"))).Transform = transtrans3d;
                transtrans3d.BeginAnimation(TranslateTransform3D.OffsetYProperty, day);
            }

        }
    }
}
