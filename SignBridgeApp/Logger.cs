using System;
using System.IO;

namespace SignBridgeApp
{
    /// <summary>
    /// Ghi log đơn giản ra file. Cần thiết vì app chạy ẩn trong system tray
    /// (không có console/cửa sổ để xem log trực tiếp) - khi triển khai ở
    /// nhiều máy/khu khác nhau, log file là cách duy nhất để IT/Duy kiểm tra
    /// lỗi từ xa khi nhân viên báo "không ký được".
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new object();

        public static void Info(string message) => Write("INFO", message);
        public static void Error(string message) => Write("ERROR", message);

        private static void Write(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    string logFolder = Path.Combine(AppSettings.OutputFolder, "..", "Logs");
                    Directory.CreateDirectory(logFolder);
                    string logFile = Path.Combine(logFolder, $"bridge_{DateTime.Now:yyyyMMdd}.log");
                    File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}\r\n");
                }
            }
            catch
            {
                // Không để lỗi ghi log làm crash app.
            }
        }
    }
}
