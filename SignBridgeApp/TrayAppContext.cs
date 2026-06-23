using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SignBridgeApp
{
    /// <summary>
    /// Context chạy nền của Bridge app - không có form chính hiển thị, chỉ
    /// có icon trong system tray. Đây vừa là "UI thread" giữ message loop
    /// cho WinForms (bắt buộc để STPadLib + FormSign hoạt động), vừa cho
    /// nhân viên/IT xem trạng thái hoặc thoát app khi cần qua menu chuột phải.
    /// </summary>
    public class TrayAppContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        // Form ẩn hoàn toàn - chỉ tồn tại để có handle cho Control.Invoke() từ
        // HTTP thread gọi sang UI thread. Không hiển thị gì với người dùng.
        private readonly Form _invokeTarget;
        private readonly SignDeviceController _device;
        private readonly HttpApiServer _server;

        public TrayAppContext()
        {
            _invokeTarget = new Form
            {
                ShowInTaskbar = false,
                FormBorderStyle = FormBorderStyle.None,
                Size = new Size(0, 0),
                Opacity = 0,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-2000, -2000)
            };
            // Show()/Hide() ngay để buộc tạo window handle thật (cần cho
            // Invoke) mà không hiện gì lên màn hình.
            _invokeTarget.Show();
            _invokeTarget.Hide();

            _device = new SignDeviceController(AppSettings.DeviceIndex);
            _server = new HttpApiServer(_invokeTarget, _device);

            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "SignBridgeApp - đang khởi động..."
            };
            _trayIcon.ContextMenuStrip = BuildMenu();
            _trayIcon.DoubleClick += (s, e) => ShowStatus();

            try
            {
                _server.Start();
                _trayIcon.Text = Truncate($"SignBridgeApp - đang chạy ({_server.ListenPrefix})");
            }
            catch (Exception ex)
            {
                Logger.Error("Không thể khởi động HTTP server: " + ex.Message);
                _trayIcon.Text = "SignBridgeApp - LỖI khởi động server";
                MessageBox.Show(
                    "Không thể khởi động HTTP server: " + ex.Message +
                    "\n\nKiểm tra xem port đã được dùng bởi app khác chưa, hoặc xem README " +
                    "phần quyền Admin / lệnh netsh nếu BindAddress khác 'localhost'.",
                    "Lỗi SignBridgeApp", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();

            var statusItem = new ToolStripMenuItem("Xem trạng thái thiết bị");
            statusItem.Click += (s, e) => ShowStatus();
            menu.Items.Add(statusItem);

            //var openFolderItem = new ToolStripMenuItem("Mở thư mục lưu ảnh");
            //openFolderItem.Click += (s, e) =>
            //{
            //    try
            //    {
            //        Directory.CreateDirectory(AppSettings.OutputFolder);
            //        Process.Start("explorer.exe", AppSettings.OutputFolder);
            //    }
            //    catch (Exception ex)
            //    {
            //        MessageBox.Show("Không thể mở thư mục: " + ex.Message);
            //    }
            //};
            //menu.Items.Add(openFolderItem);

            menu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("Thoát");
            exitItem.Click += (s, e) => ExitThread();
            menu.Items.Add(exitItem);

            return menu;
        }

        private void ShowStatus()
        {
            MessageBox.Show(
                $"Thiết bị sẵn sàng: {(_device.IsReady ? "CÓ" : "KHÔNG")}\n" +
                $"Lỗi gần nhất: {_device.LastError ?? "(không có)"}\n" +
                $"Địa chỉ API: {_server.ListenPrefix}\n",
                //$"Thư mục lưu ảnh: {AppSettings.OutputFolder}",
                "Trạng thái SignBridgeApp", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string Truncate(string s) => s.Length > 63 ? s.Substring(0, 60) + "..." : s;

        protected override void ExitThreadCore()
        {
            _server.Stop();
            _device.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _invokeTarget.Dispose();
            base.ExitThreadCore();
        }
    }
}
