using System;
using System.Drawing;
using System.Threading;
using signotec.STPadLibNet;

namespace SignBridgeApp
{
    public class SignDeviceController : IDisposable
    {
        private readonly int _deviceIndex;
        private STPadLib _stPad;
        private Timer _reconnectTimer;
        private bool _disposed;
        private bool _confirmCalled;

        public bool IsReady { get; private set; }
        public string LastError { get; private set; }

        public Image LatestSignatureImage { get; private set; }
        public byte[] LatestRawSignData { get; private set; }

        public event EventHandler SignatureDataReceived;
        public event EventHandler DeviceClosed;

        public SignDeviceController(int deviceIndex)
        {
            _deviceIndex = deviceIndex;
            TryConnect();
            _reconnectTimer = new Timer(_ =>
            {
                if (!IsReady && !_disposed) TryConnect();
            }, null, 5000, 5000);
        }

        private void TryConnect()
        {
            try
            {
                if (_stPad == null)
                {
                    _stPad = new STPadLib();
                    _stPad.SignatureDataReceived += OnSdkSignatureDataReceived;
                    _stPad.SensorHotSpotPressed += OnSdkHotSpotPressed;
                    _stPad.DeviceDisconnected += OnSdkDeviceDisconnected;
                }

                _stPad.DeviceOpen(_deviceIndex);

                int count = _stPad.DeviceGetCount();
                if (count <= 0)
                {
                    LastError = $"DeviceGetCount() = {count}. Kiểm tra cáp USB.";
                    IsReady = false;
                    Logger.Error(LastError);
                    return;
                }

                IsReady = true;
                LastError = null;
                Logger.Info($"Kết nối thiết bị OK (index={_deviceIndex}, count={count}).");
            }
            catch (Exception ex)
            {
                IsReady = false;
                LastError = ex.Message;
                Logger.Error("Lỗi kết nối thiết bị: " + ex.Message);
            }
        }

        // ── SDK Event Handlers ───────────────────────────────────────────────

        private void OnSdkHotSpotPressed(object sender, SensorHotSpotPressedEventArgs e)
        {
            Logger.Info("SensorHotSpotPressed. Gọi SignatureConfirm()...");
            CallSignatureConfirm("hotspot thiết bị");
        }

        /// <summary>Gọi từ FormSign khi nhân viên bấm nút "Lấy chữ ký" trên PC.</summary>
        public void ConfirmSignatureManually()
        {
            Logger.Info("Manual confirm từ PC. Gọi SignatureConfirm()...");
            CallSignatureConfirm("nút PC");
        }

        private void CallSignatureConfirm(string source)
        {
            lock (this)
            {
                if (_confirmCalled)
                {
                    Logger.Info($"SignatureConfirm đã được gọi rồi (source={source}), bỏ qua.");
                    return;
                }
                _confirmCalled = true;
            }
            try
            {
                // 1. Gọi lệnh xác nhận kết thúc capture chữ ký
                _stPad.SignatureConfirm();
                Logger.Info($"SignatureConfirm() OK (source={source}). Quá trình ký kết thúc. Tiến hành lấy ảnh...");

                // 2. [QUAN TRỌNG] LẤY RAW DATA VÀ ẢNH NGAY TẠI ĐÂY
                try
                {
                    byte[] raw = _stPad.SignatureGetSignData();
                    if (raw != null && raw.Length > 0)
                    {
                        LatestRawSignData = raw;
                        Logger.Info($"Raw data cuối cùng: {raw.Length} bytes.");
                    }
                }
                catch (Exception ex) { Logger.Error("SignatureGetSignData failed: " + ex.Message); }

                try
                {
                    Bitmap bmp = _stPad.SignatureSaveAsStreamEx(
                        0, 0, 0, 3, Color.Black, SignatureImageFlag.None);
                    if (bmp != null)
                    {
                        LatestSignatureImage?.Dispose();
                        LatestSignatureImage = bmp;
                        Logger.Info($"Bitmap cuối cùng OK: {bmp.Width}x{bmp.Height}px.");
                    }
                    else
                    {
                        Logger.Error("SignatureSaveAsStreamEx trả về null.");
                    }
                }
                catch (Exception ex) { Logger.Error("SignatureSaveAsStreamEx failed: " + ex.Message); }

                // 3. Bắn sự kiện để Form trên PC biết và load LatestSignatureImage lên giao diện
                SignatureDataReceived?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Logger.Error($"SignatureConfirm() failed (source={source}): " + ex.Message);
                lock (this) { _confirmCalled = false; }
            }
        }

        private void OnSdkSignatureDataReceived(object sender, SignatureDataReceivedEventArgs e)
        {
            //Logger.Info("SignatureDataReceived — lấy ảnh và raw data...");
            //try
            //{
            //    try
            //    {
            //        byte[] raw = _stPad.SignatureGetSignData();
            //        if (raw != null && raw.Length > 0)
            //        {
            //            LatestRawSignData = raw;
            //            Logger.Info($"Raw data: {raw.Length} bytes.");
            //        }
            //    }
            //    catch (Exception ex) { Logger.Error("SignatureGetSignData failed: " + ex.Message); }

            //    try
            //    {
            //        Bitmap bmp = _stPad.SignatureSaveAsStreamEx(
            //            0, 0, 0, 3, Color.Black, SignatureImageFlag.None);
            //        if (bmp != null)
            //        {
            //            LatestSignatureImage?.Dispose();
            //            LatestSignatureImage = bmp;
            //            Logger.Info($"Bitmap OK: {bmp.Width}x{bmp.Height}px.");
            //        }
            //        else
            //        {
            //            Logger.Error("SignatureSaveAsStreamEx trả về null.");
            //        }
            //    }
            //    catch (Exception ex) { Logger.Error("SignatureSaveAsStreamEx failed: " + ex.Message); }

            //    SignatureDataReceived?.Invoke(this, EventArgs.Empty);
            //}
            //catch (Exception ex)
            //{
            //    Logger.Error("OnSdkSignatureDataReceived error: " + ex.Message);
            //    SignatureDataReceived?.Invoke(this, EventArgs.Empty);
            //}
        }

        private void OnSdkDeviceDisconnected(object sender, EventArgs e)
        {
            IsReady = false;
            Logger.Error("SDK event: DeviceDisconnected.");
            DeviceClosed?.Invoke(this, EventArgs.Empty);
        }

        // ── Điều khiển chữ ký ───────────────────────────────────────────────

        public void StartSignature()
        {
            EnsureReady();

            LatestSignatureImage?.Dispose();
            LatestSignatureImage = null;
            LatestRawSignData = null;
            lock (this) { _confirmCalled = false; }

            try { _stPad.SignatureSetSecureMode(false); } catch { }
            try { _stPad.DisplayConfigPen(3, Color.Black); } catch { }
            try { _stPad.SignatureScaleToDisplay(_deviceIndex); } catch { }
            try { _stPad.DisplayErase(); } catch { }

            // Lấy kích thước màn hình thiết bị
            int sw = 640, sh = 480;
            try { sw = _stPad.DisplayWidth; sh = _stPad.DisplayHeight; } catch { }

            // Vùng nút OK chiếm 76px ở dưới cùng
            int btnH = 60;
            int margin = 8;
            int okZoneH = btnH + margin * 2; // 76px

            // SensorSetSignRect(x, y, width, height): vùng cảm nhận nét bút
            // = toàn màn hình trừ phần dưới dành cho nút OK
            try
            {
                _stPad.SensorSetSignRect(0, 0, sw, sh - okZoneH);
                Logger.Info($"SensorSetSignRect(0, 0, {sw}, {sh - okZoneH}) OK.");
            }
            catch (Exception ex)
            {
                Logger.Error("SensorSetSignRect failed: " + ex.Message);
            }

            Logger.Info("Calling SignatureStart()...");
            _stPad.SignatureStart();
            Logger.Info("SignatureStart() OK. Chờ người dùng ký và bấm OK...");


            RegisterOkHotSpot(sw, sh, btnH, margin);
        }

        private void RegisterOkHotSpot(int screenW, int screenH, int btnH, int margin)
        {
            try
            {
                // Xoá hotspot cũ
                try { _stPad.SensorClearHotSpots(); } catch { }

                int btnW = 160;
                int btnX = screenW - btnW - margin;
                int btnY = screenH - btnH - margin;

                // SensorAddHotSpot(int x, int y, int width, int height)
                _stPad.SensorAddHotSpot(btnX, btnY, btnW, btnH);
                Logger.Info($"SensorAddHotSpot OK tại ({btnX},{btnY},{btnW}x{btnH}).");

              
                // Vẽ chữ "OK" lên vùng hotspot
                try
                {
                    _stPad.DisplaySetFont(new Font("Arial", 18f, FontStyle.Bold));
                    _stPad.DisplaySetFontColor(Color.Black);
                    int textX = btnX + btnW / 2;
                    int textY = btnY + btnH / 2 - 10;
                    _stPad.DisplaySetText(textX, textY, TextAlignment.Center, "OK");
                    Logger.Info("DisplaySetText 'OK' lên thiết bị OK.");
                }
                catch (Exception ex)
                {
                    Logger.Error("Vẽ nhãn OK thất bại (hotspot vẫn hoạt động): " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RegisterOkHotSpot failed: " + ex.Message);
                // Không throw — fallback dùng nút PC
            }
        }

        public void RetrySignature()
        {
            EnsureReady();
            LatestSignatureImage?.Dispose();
            LatestSignatureImage = null;
            LatestRawSignData = null;
            lock (this) { _confirmCalled = false; }

            try { _stPad.DisplayErase(); } catch { }

            int sw = 640, sh = 480;
            try { sw = _stPad.DisplayWidth; sh = _stPad.DisplayHeight; } catch { }
            int btnH = 60, margin = 8;

            try
            {
                _stPad.SensorSetSignRect(0, 0, sw, sh - btnH - margin * 2);
                Logger.Info($"SensorSetSignRect(0, 0, {sw}, {sh - btnH - margin * 2}) OK (retry).");
            }
            catch (Exception ex) { Logger.Error("SensorSetSignRect failed (retry): " + ex.Message); }

            Logger.Info("Calling SignatureRetry()...");
            _stPad.SignatureRetry();

            RegisterOkHotSpot(sw, sh, btnH, margin);
        }

        public void StopSignature()
        {
            try { _stPad?.SignatureStop(); Logger.Info("SignatureStop() OK."); }
            catch (Exception ex) { Logger.Error("SignatureStop failed: " + ex.Message); }
        }

        public void CancelSignature()
        {
            try { _stPad?.SignatureCancel(); Logger.Info("SignatureCancel() OK."); }
            catch { }
        }

        private void EnsureReady()
        {
            if (!IsReady || _stPad == null)
                throw new InvalidOperationException(
                    "Thiết bị ký số chưa sẵn sàng: " + (LastError ?? "không xác định."));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _reconnectTimer?.Dispose();
            LatestSignatureImage?.Dispose();
            if (_stPad != null)
            {
                _stPad.SignatureDataReceived -= OnSdkSignatureDataReceived;
                _stPad.SensorHotSpotPressed -= OnSdkHotSpotPressed;
                _stPad.DeviceDisconnected -= OnSdkDeviceDisconnected;
            }
            try { _stPad?.DeviceClose(_deviceIndex); Logger.Info("DeviceClose() OK."); } catch { }
        }
    }
}