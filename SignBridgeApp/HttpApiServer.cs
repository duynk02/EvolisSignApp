using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace SignBridgeApp
{
    /// <summary>
    /// HTTP server tự host (không cần IIS) để ứng dụng HIS trên CÙNG máy gọi
    /// tới. Mọi request gọi POST /api/sign sẽ mở cửa sổ FormSign trên UI
    /// thread (cho nhân viên y tế ký + kiểm tra), CHẶN (block) tới khi cửa sổ
    /// đó đóng lại, rồi mới trả response - tức là HIS gọi API kiểu đồng bộ
    /// như mô tả: gọi -> chờ ký xong -> nhận ảnh ngay trong response.
    ///
    /// API:
    ///   GET  /api/status   - kiểm tra thiết bị có sẵn sàng không
    ///   POST /api/sign     - mở phiên ký, trả về ảnh chữ ký sau khi xác nhận
    /// </summary>
    public class HttpApiServer
    {
        private readonly HttpListener _listener = new HttpListener();
        private readonly Control _uiInvokeTarget;
        private readonly SignDeviceController _device;
        private readonly object _signLock = new object();
        private bool _isSignBusy;
        private CancellationTokenSource _cts;

        public HttpApiServer(Control uiInvokeTarget, SignDeviceController device)
        {
            _uiInvokeTarget = uiInvokeTarget;
            _device = device;

            string prefix = $"http://{AppSettings.BindAddress}:{AppSettings.ListenPort}/";
            _listener.Prefixes.Add(prefix);
        }

        public string ListenPrefix => _listener.Prefixes.Count > 0 ? string.Join(",", _listener.Prefixes) : "";

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener.Start();
            Logger.Info("HTTP server đã khởi động tại " + ListenPrefix);
            Task.Run(() => ListenLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            try { _listener.Stop(); } catch { /* bỏ qua */ }
        }

        private async void ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener.IsListening)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener.GetContextAsync();
                }
                catch
                {
                    break; // listener đã Stop()
                }

                _ = Task.Run(() => HandleRequestSafe(ctx));
            }
        }

        private void HandleRequestSafe(HttpListenerContext ctx)
        {
            try
            {
                HandleRequest(ctx);
            }
            catch (Exception ex)
            {
                Logger.Error("Lỗi xử lý request: " + ex.Message);
                try { WriteJson(ctx.Response, 500, new { success = false, errorMessage = "Lỗi server: " + ex.Message }); }
                catch { /* bỏ qua */ }
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            var req = ctx.Request;

            // Xác thực đơn giản qua header X-Api-Key (chỉ áp dụng nếu đã cấu
            // hình ApiKey trong App.config - mặc định để trống vì server chỉ
            // bind localhost).
            if (!string.IsNullOrEmpty(AppSettings.ApiKey))
            {
                string provided = req.Headers["X-Api-Key"];
                if (provided != AppSettings.ApiKey)
                {
                    WriteJson(ctx.Response, 401, new { success = false, errorMessage = "Thiếu hoặc sai X-Api-Key." });
                    return;
                }
            }

            string path = req.Url.AbsolutePath.TrimEnd('/').ToLowerInvariant();

            if (path == "/api/status" && req.HttpMethod == "GET")
            {
                HandleStatus(ctx);
            }
            else if (path == "/api/sign" && req.HttpMethod == "POST")
            {
                HandleSign(ctx);
            }
            else
            {
                WriteJson(ctx.Response, 404, new { success = false, errorMessage = "Không tìm thấy endpoint." });
            }
        }

        private void HandleStatus(HttpListenerContext ctx)
        {
            WriteJson(ctx.Response, 200, new
            {
                deviceReady = _device.IsReady,
                busy = _isSignBusy,
                lastError = _device.LastError
            });
        }

        private void HandleSign(HttpListenerContext ctx)
        {
            lock (_signLock)
            {
                if (_isSignBusy)
                {
                    WriteJson(ctx.Response, 409, new { success = false, errorMessage = "Đang có phiên ký khác đang xử lý." });
                    return;
                }
                _isSignBusy = true;
            }

            try
            {
                string body;
                using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                    body = reader.ReadToEnd();

                // Body JSON tùy chọn: { "fileName": "BN000123_PhieuTiepNhan" }
                // dùng để đặt tên file ảnh lưu lại - không bắt buộc.
                string fileNameHint = null;
                if (!string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        var serializer = new JavaScriptSerializer();
                        var dict = serializer.Deserialize<Dictionary<string, object>>(body);
                        if (dict != null && dict.ContainsKey("fileName"))
                            fileNameHint = Convert.ToString(dict["fileName"]);
                    }
                    catch
                    {
                        // Body không hợp lệ - bỏ qua, dùng tên file mặc định.
                    }
                }

                if (!_device.IsReady)
                {
                    string err = "Thiết bị ký số chưa sẵn sàng: " + (_device.LastError ?? "không xác định.");
                    Logger.Error(err);
                    WriteJson(ctx.Response, 503, new { success = false, errorMessage = err });
                    return;
                }

                SignatureResult result = null;
                Exception uiEx = null;

                // QUAN TRỌNG: FormSign (WinForms) phải tạo/chạy trên UI thread.
                // Invoke() ở đây sẽ CHẶN thread đang xử lý HTTP request này cho
                // tới khi dialog đóng lại - đây chính là cách hiện thực luồng
                // đồng bộ "gọi API -> chờ ký xong -> nhận kết quả" mà không cần
                // polling endpoint riêng.
                _uiInvokeTarget.Invoke(new Action(() =>
                {
                    try
                    {
                        using (var formSign = new FormSign(_device))
                        {
                            formSign.ShowDialog();
                            result = formSign.Result;
                        }
                    }
                    catch (Exception ex)
                    {
                        uiEx = ex;
                    }
                }));

                if (uiEx != null) throw uiEx;

                if (result == null || !result.Success)
                {
                    string reason = result?.ErrorMessage ?? "Không xác định.";
                    Logger.Info("Phiên ký không hoàn tất: " + reason);
                    WriteJson(ctx.Response, 200, new { success = false, errorMessage = reason });
                    return;
                }

                //Directory.CreateDirectory(AppSettings.OutputFolder);
                string safeName = MakeSafeFileName(fileNameHint);
                //string fileName = $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                //string fullPath = Path.Combine(AppSettings.OutputFolder, fileName);

                //result.SignatureImage.Save(fullPath, System.Drawing.Imaging.ImageFormat.Png);

                string base64;
                using (var ms = new MemoryStream())
                {
                    result.SignatureImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    base64 = Convert.ToBase64String(ms.ToArray());
                }

                //Logger.Info("Ký thành công, đã lưu: " + fullPath);

                WriteJson(ctx.Response, 200, new
                {
                    success = true,
                    imageBase64 = base64,
                    //imagePath = fullPath,
                    signedAt = DateTime.UtcNow.AddHours(7).ToString("o"),
                    errorMessage = (string)null
                });
            }
            finally
            {
                lock (_signLock) { _isSignBusy = false; }
            }
        }

        private static string MakeSafeFileName(string hint)
        {
            string baseName = string.IsNullOrWhiteSpace(hint) ? "signature" : hint;
            foreach (char c in Path.GetInvalidFileNameChars())
                baseName = baseName.Replace(c, '_');
            return baseName;
        }

        private static void WriteJson(HttpListenerResponse response, int statusCode, object data)
        {
            var serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(data);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            // CORS mở - an toàn vì server chỉ lắng nghe localhost theo mặc định.
            response.Headers.Add("Access-Control-Allow-Origin", "*");

            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
    }
}
