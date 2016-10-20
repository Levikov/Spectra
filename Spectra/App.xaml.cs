using System.Collections.Generic;
using System.Windows;

namespace Spectra
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// 动态显示图像
        /// </summary>
        public static DynamicImagingWindow_Win32 global_Win_Dynamic;
        /// <summary>
        /// 单帧图像提取
        /// </summary>
        public static FrmImgWindow global_FrmImgWindow;
        /// <summary>
        /// 快视窗体
        /// </summary>
        public static QuickViewWindow global_QuickViewWindow;
        /// <summary>
        /// 6个展示窗体
        /// </summary>
        public static List<Window> global_Windows = new List<Window>();
        /// <summary>
        /// 4个子窗体图像的缓存
        /// </summary>
        public static ImageBuffer[] global_ImageBuffer = new ImageBuffer[4];

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            global_FrmImgWindow = new FrmImgWindow();
            global_QuickViewWindow = new QuickViewWindow();
            //固定总共6个窗体(使用时只需要刷新和显示即可,窗体内容和数据库对应)
            for (int i=0;i<6;i++)
                global_Windows.Add(new MultiFuncWindow());
        }
    }
}
