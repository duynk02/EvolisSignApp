using System.Drawing;

namespace SignBridgeApp
{
    /// <summary>
    /// Đóng gói kết quả ký số từ FormSign, dùng để trả dữ liệu cho
    /// HttpApiServer sau khi dialog ký đóng lại.
    /// </summary>
    public class SignatureResult
    {
        /// <summary>Ảnh chữ ký để hiển thị/lưu file.</summary>
        public Image SignatureImage { get; set; }

        /// <summary>Dữ liệu chữ ký gốc (raw bytes từ SignatureGetSignData) - dùng cho xác thực sau này nếu cần.</summary>
        public byte[] RawSignatureData { get; set; }

        /// <summary>true nếu nhân viên y tế đã xác nhận chữ ký hợp lệ (bấm "Xác nhận").</summary>
        public bool Success { get; set; }

        /// <summary>Lý do thất bại (hủy, lỗi thiết bị...) khi Success = false.</summary>
        public string ErrorMessage { get; set; }
    }
}
