using System;
using System.IO;
using System.Windows.Forms;

namespace SignBridgeApp
{
    public partial class FormSign : Form
    {
        private readonly SignDeviceController _device;
        private bool _hasSignatureData;

        public SignatureResult Result { get; private set; }

        public FormSign(SignDeviceController device)
        {
            InitializeComponent();
            _device = device;
            TopMost = true;
            StartPosition = FormStartPosition.CenterScreen;
            Load += FormSign_Load;
            FormClosing += FormSign_FormClosing;
        }

        private void FormSign_Load(object sender, EventArgs e)
        {
            _device.SignatureDataReceived += Device_SignatureDataReceived;
            _device.DeviceClosed += Device_DeviceClosed;

            btnConfirm.Enabled = false;
            lblStatus.Text = "Vui lòng ký vào màn hình ứng dụng EVOLIS...";

            try
            {
                _device.StartSignature();
            }
            catch (Exception ex)
            {
                Result = new SignatureResult { Success = false, ErrorMessage = "Không thể bắt đầu ký: " + ex.Message };
                MessageBox.Show(Result.ErrorMessage, "Lỗi thiết bị", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private void Device_DeviceClosed(object sender, EventArgs e)
        {
            SafeInvoke(() =>
            {
                lblStatus.Text = "Thiết bị đã ngắt kết nối. Vui lòng kiểm tra lại cáp USB.";
                btnConfirm.Enabled = false;
            });
        }

        /// <summary>
        /// SignatureDataReceived = có nét bút mới.
        /// SignDeviceController đã lấy ảnh sẵn trong LatestSignatureImage.
        /// Chỉ cần cập nhật preview và bật nút Xác nhận.
        /// KHÔNG gọi Stop/Save ở đây.
        /// </summary>
        private void Device_SignatureDataReceived(object sender, EventArgs e)
        {
            SafeInvoke(() =>
            {
                _hasSignatureData = true;
                btnConfirm.Enabled = true;
                lblStatus.Text = "Đang thực hiện ký... Bấm 'Xác nhận hợp lệ' khi hoàn thành.";

                // Hiển thị preview realtime từ LatestSignatureImage
                // (ảnh đã được lấy trong event handler của controller)
                if (_device.LatestSignatureImage != null)
                {
                    try
                    {
                        // Clone để tránh conflict khi controller cập nhật ảnh mới
                        var preview = (System.Drawing.Image)_device.LatestSignatureImage.Clone();
                        picSignaturePreview.Image?.Dispose();
                        picSignaturePreview.Image = preview;
                    }
                    catch { /* bỏ qua lỗi clone */ }
                }
            });
        }

        /// <summary>
        /// Nhân viên xác nhận ký xong.
        /// Data đã có sẵn trong _device.LatestSignatureImage và LatestRawSignData
        /// → chỉ cần StopSignature() rồi lưu từ cache, không gọi SDK read nữa.
        /// </summary>
        private void btnConfirm_Click(object sender, EventArgs e)
        {
            if (!_hasSignatureData || _device.LatestSignatureImage == null)
            {
                MessageBox.Show("Chưa có dữ liệu ký. Vui lòng ký lại vào màn hình ứng dụng EVOLIS...",
                    "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnConfirm.Enabled = false;
            btnRetry.Enabled = false;
            btnCancel.Enabled = false;
            lblStatus.Text = "Đang lưu chữ ký...";

            try
            {
                // Dừng capture (không đọc data sau bước này)
                _device.StopSignature();

                // Lưu từ cache đã lấy trong event
                Result = new SignatureResult
                {
                    Success = true,
                    SignatureImage = (System.Drawing.Image)_device.LatestSignatureImage.Clone(),
                    RawSignatureData = _device.LatestRawSignData
                };

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                Result = new SignatureResult { Success = false, ErrorMessage = "Lỗi khi lưu chữ ký: " + ex.Message };
                MessageBox.Show(Result.ErrorMessage, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnConfirm.Enabled = true;
                btnRetry.Enabled = true;
                btnCancel.Enabled = true;
                lblStatus.Text = "Lỗi. Thử xác nhận lại hoặc ký lại.";
            }
        }

        private void btnRetry_Click(object sender, EventArgs e)
        {
            try
            {
                _hasSignatureData = false;
                btnConfirm.Enabled = false;
                picSignaturePreview.Image?.Dispose();
                picSignaturePreview.Image = null;

                _device.RetrySignature();
                lblStatus.Text = "Vui lòng ký vào màn hình ứng dụng EVOLIS...";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể yêu cầu ký lại: " + ex.Message, "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            _device.CancelSignature();
            Result = new SignatureResult { Success = false, ErrorMessage = "Người dùng đã hủy." };
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void FormSign_FormClosing(object sender, FormClosingEventArgs e)
        {
            _device.SignatureDataReceived -= Device_SignatureDataReceived;
            _device.DeviceClosed -= Device_DeviceClosed;
        }

        private void SafeInvoke(Action action)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                try { Invoke(action); } catch (ObjectDisposedException) { }
            }
            else { action(); }
        }
    }
}