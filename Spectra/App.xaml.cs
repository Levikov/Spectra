using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Spectra
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {

        public static _3DWindow global_Win_3D;
        public static SpecImgWindow global_Win_SpecImg;
        public static MapWindow global_Win_Map;
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Variables.Screen_Locations = new Rectangle[4];
            #region 设定屏幕属性
            //foreach (System.Windows.Forms.Screen s in System.Windows.Forms.Screen.AllScreens)
            //{
            //    Variables.Screens.Add(new ScreenParams(s.WorkingArea, s.DeviceName, s.Primary));
            //    if (s.WorkingArea.Width == 3840) Variables.Screen_Locations[1] = s.WorkingArea;
            //}
            #endregion

            global_Win_3D = new _3DWindow();
            global_Win_SpecImg = new SpecImgWindow();
            global_Win_Map = new MapWindow();
            //global_Win_Map.Show();
            //global_Win_ImageShow.Show();
        }
    }
}
