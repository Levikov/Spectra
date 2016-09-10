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
        public static MapWindow global_Win_Map;
        public static DynamicImagingWindow_Win32 global_Win_Dynamic;
        public static List<Window> global_Windows = new List<Window>();
        public static ImageBuffer[] global_ImageBuffer = new ImageBuffer[4];

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            //固定总共6个窗体(使用时只需要刷新和显示即可,窗体内容和数据库对应)
            for(int i=0;i<6;i++)
                global_Windows.Add(new MultiFuncWindow());
            Variables.Screen_Locations = new Rectangle[4];
            #region 设定屏幕属性
            //foreach (System.Windows.Forms.Screen s in System.Windows.Forms.Screen.AllScreens)
            //{
            //    Variables.Screens.Add(new ScreenParams(s.WorkingArea, s.DeviceName, s.Primary));
            //    if (s.WorkingArea.Width == 3840) Variables.Screen_Locations[1] = s.WorkingArea;
            //}
            #endregion
        }
    }
}
