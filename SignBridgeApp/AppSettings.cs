using System.Configuration;

namespace SignBridgeApp
{
    /// <summary>
    /// Đọc các tham số cấu hình từ App.config (mục appSettings),
    /// có giá trị mặc định hợp lý nếu thiếu key.
    /// </summary>
    public static class AppSettings
    {
        public static int ListenPort => GetInt("ListenPort", 4033);
        public static string BindAddress => Get("BindAddress", "localhost");
        public static string OutputFolder => Get("OutputFolder", @"C:\SignBridge\SignedImages");
        public static int DeviceIndex => GetInt("DeviceIndex", 0);
        public static string ApiKey => Get("ApiKey", "");

        private static string Get(string key, string fallback)
        {
            var v = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(v) ? fallback : v;
        }

        private static int GetInt(string key, int fallback)
        {
            var v = ConfigurationManager.AppSettings[key];
            return int.TryParse(v, out var result) ? result : fallback;
        }
    }
}
