using System;
using System.Windows.Forms;

namespace SignBridgeApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ApplicationContext (không phải Form chính) - app chạy ẩn trong
            // system tray, không có cửa sổ chính nào hiện ra khi khởi động.
            Application.Run(new TrayAppContext());
        }
    }
}
