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
        ModelVisual3D mv3d;
        public Test3D()
        {
            InitializeComponent();
            mv3d =Resources["ModelVisual3D"] as ModelVisual3D;
            scene.Viewport.Children.Add(mv3d);
           
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
            foreach (object o in mv3d.Children)
            {
                if (o is Geometry3D)
                {
                    Geometry3D g = o as Geometry3D;
                    
                }
            }

        }
    }
}
