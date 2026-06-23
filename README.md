# SignBridgeApp - Bridge app ký số signotec cho HIS

App chạy nền (system tray) tại **từng máy có cắm thiết bị signotec Sigma**.
HIS (cài trên CÙNG máy) gọi HTTP API tới app này để mở phiên ký, nhân viên y
tế kiểm tra chữ ký trên màn hình, xác nhận xong thì API trả ảnh ngay lại cho HIS.

## Kiến trúc

```
┌─────────────────────────── 1 máy tại quầy ───────────────────────────┐
│                                                                       │
│   HIS                  SignBridgeApp          │
│   ───────────────────                ─────────────────────────       │
│   gọi HTTP                            │
│   POST localhost:4033/api/sign  ───►  HttpListener nhận request      │
│                                        │                              │
│                                        ▼                              │
│                                  Invoke() sang UI thread              │
│                                        │                              │
│                                        ▼                              │
│                                  Mở FormSign (TopMost)                │
│                                  "Vui lòng ký vào pad..."             │
│                                        │                              │
│                              [Bệnh nhân ký trên thiết bị signotec]    │
│                                        │                              │
│                                        ▼                              │
│                                  Hiện ảnh preview trên màn hình       │
│                                  Nhân viên y tế KIỂM TRA chữ ký:      │
│                                    ✓ Xác nhận hợp lệ                 │
│                                    ↺ Ký lại (nếu mờ/sai)             │
│                                    ✕ Hủy                             │
│                                        │                              │
│   nhận ảnh trong response       ◄───  Trả JSON (base64 + file path)  │
│   (request đã CHỜ tới đây)            │                              │
│                                                                       │
└───────────────────────────────────────────────────────────────────────┘
```

**Vì sao thiết kế thế này:**
- Mỗi máy có pad ký vật lý cắm vào nó → mỗi máy chạy **1 bản riêng** của
  SignBridgeApp. Bệnh viện có nhiều khu/quầy = cài app này lặp lại ở mỗi máy
  có pad, không có "1 server trung tâm" vì pad là thiết bị USB cục bộ.
- Thiết bị được **mở 1 lần lúc khởi động** và giữ mở suốt ngày (không
  mở/đóng mỗi lượt ký) - nhanh hơn và tránh lỗi USB do mở/đóng liên tục.
- Server chạy bằng `HttpListener` tự host - không cần cài IIS trên từng máy,
  chỉ cần copy thư mục + chạy file `.exe`.
- Request `/api/sign` **chặn (block)** cho tới khi nhân viên xác nhận/hủy -
  HIS không cần polling, gọi 1 lần là có kết quả ngay trong response.

## Cấu trúc project

```
SignBridgeApp/
├── SignBridgeApp.sln
└── SignBridgeApp/
    ├── SignBridgeApp.csproj      (.NET Framework 4.6.2, x86, OutputType=WinExe)
    ├── App.config                (port, thư mục lưu ảnh, device index...)
    ├── Program.cs                (entry point)
    ├── TrayAppContext.cs         (system tray icon, khởi tạo device + server)
    ├── HttpApiServer.cs          (HttpListener, routing /api/sign, /api/status)
    ├── SignDeviceController.cs   (quản lý STPadLib - mở 1 lần, dùng chung)
    ├── FormSign.cs / .Designer.cs (dialog ký + kiểm tra chữ ký - TopMost)
    ├── SignatureResult.cs
    ├── AppSettings.cs
    ├── Logger.cs                 (ghi log ra file vì app chạy ẩn, không có console)
    ├── Properties/AssemblyInfo.cs
    └── Libs/
        └── STPadLibNet.dll        (BẠN CẦN TỰ COPY VÀO ĐÂY)
```

## Bước setup & deploy (lặp lại cho MỖI máy có pad ký)

1. Copy `STPadLibNet.dll` vào `SignBridgeApp/Libs/`.
2. Mở `SignBridgeApp.sln` bằng Visual Studio, build với **Platform = x86**.
3. Kiểm tra lại `App.config` (xem mục "Cấu hình" dưới đây) - đặc biệt
   `OutputFolder` nên đặt ở ổ đĩa có quyền ghi của user đang chạy app.
4. Copy toàn bộ thư mục `bin\Release\` sang máy đích (máy có cắm pad).
5. Chạy `SignBridgeApp.exe` - sẽ thấy icon xuất hiện ở system tray (góc dưới
   phải màn hình), không có cửa sổ nào hiện ra.
6. Click phải vào icon → "Xem trạng thái thiết bị" để xác nhận
   `Thiết bị sẵn sàng: CÓ`.
7. **Để app tự chạy mỗi khi đăng nhập Windows**: tạo shortcut tới
   `SignBridgeApp.exe`, đặt vào thư mục Startup:
   `C:\Users\<tên user>\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\`

## Cấu hình (`App.config`)

| Key | Mặc định | Ý nghĩa |
|---|---|---|
| `ListenPort` | `8088` | Cổng HTTP mà HIS sẽ gọi tới |
| `BindAddress` | `localhost` | Để `localhost` nếu HIS chạy CÙNG máy (mặc định, không cần quyền Admin). Đổi thành `+` nếu sau này cần cho máy khác trong LAN gọi tới (xem mục Bảo mật) |
| `OutputFolder` | `C:\SignBridge\SignedImages` | Nơi lưu ảnh chữ ký |
| `DeviceIndex` | `0` | Index thiết bị signotec (0 = thiết bị duy nhất/đầu tiên) |
| `ApiKey` | (trống) | Nếu đặt giá trị, mọi request phải có header `X-Api-Key: <giá trị>` |

## API Contract

### `GET /api/status`

Kiểm tra thiết bị có sẵn sàng không (HIS có thể gọi trước để hiện cảnh báo
sớm nếu pad chưa cắm/lỗi).

**Response 200:**
```json
{
  "deviceReady": true,
  "busy": false,
  "lastError": null
}
```

### `POST /api/sign`

Mở phiên ký, **chặn cho tới khi nhân viên xác nhận hoặc hủy**, trả kết quả.

**Request body (tùy chọn):**
```json
{
  "fileName": "BN000123_PhieuTiepNhan"
}
```
Nếu không gửi `fileName`, file sẽ được đặt tên `signature_<timestamp>.png`.

**Response 200 - ký thành công:**
```json
{
  "success": true,
  "imageBase64": "iVBORw0KGgoAAAANSU...",
  "imagePath": "D:\\SignBridge\\SignedImages\\BN000123_PhieuTiepNhan_20260623_143501.png",
  "signedAtUtc": "2026-06-23T07:35:01.0000000Z",
  "errorMessage": null
}
```
- `imageBase64`: ảnh PNG dạng base64, dùng được ngay (decode + hiển thị/lưu).
- `imagePath`: đường dẫn file đã lưu trên máy - vì HIS và Bridge app **cùng
  máy**, HIS có thể đọc trực tiếp file này thay vì decode base64 nếu muốn.

**Response 200 - người dùng hủy/chưa ký:**
```json
{
  "success": false,
  "errorMessage": "Người dùng đã hủy."
}
```

**Response 409 - đang có phiên ký khác chạy** (ví dụ HIS gọi 2 lần liên tiếp
quá nhanh, hoặc 2 máy HIS khác nhau cùng gọi 1 Bridge app):
```json
{ "success": false, "errorMessage": "Đang có phiên ký khác đang xử lý." }
```

**Response 503 - thiết bị chưa sẵn sàng** (pad chưa cắm/mất kết nối):
```json
{ "success": false, "errorMessage": "Thiết bị ký số chưa sẵn sàng: ..." }
```

## Ví dụ gọi từ HIS (C#)

```csharp
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

public async Task<string> YeuCauKySo(string maBenhNhan)
{
    using (var client = new HttpClient())
    {
        client.Timeout = System.TimeSpan.FromMinutes(5); // chờ nhân viên ký xong
        var requestBody = new StringContent(
            $"{{\"fileName\":\"{maBenhNhan}_PhieuTiepNhan\"}}",
            Encoding.UTF8, "application/json");

        var response = await client.PostAsync("http://localhost:8088/api/sign", requestBody);
        string json = await response.Content.ReadAsStringAsync();

        // Parse json (success, imageBase64, imagePath...) bằng
        // JavaScriptSerializer hoặc Newtonsoft.Json tùy HIS đang dùng gì.
        return json;
    }
}
```

**Lưu ý quan trọng:** `HttpClient.Timeout` phải đặt đủ dài (vài phút), vì
request này CHẶN tới khi nhân viên xác nhận xong - không phải API trả lời
ngay lập tức như API thông thường.

## Bảo mật

- Mặc định `BindAddress = localhost` → **chỉ app trên CHÍNH máy đó** gọi
  được, an toàn cho mô hình HIS + Bridge app cùng máy.
- Nếu sau này cần cho máy khác trong LAN gọi tới (không đúng với yêu cầu
  hiện tại nhưng để tham khảo): đổi `BindAddress` thành `+`, sau đó chạy
  lệnh sau **với quyền Admin, 1 lần duy nhất** trên máy đó:
  ```
  netsh http add urlacl url=http://+:8088/ user=Everyone
  ```
  và mở port 8088 trên Windows Firewall. Nên đặt thêm `ApiKey` trong
  `App.config` khi mở ra LAN để tránh máy lạ gọi vào.

## Cần kiểm tra lại khi build thật (chưa xác nhận 100% qua tài liệu SDK)

Object Browser không hiện XML doc/summary cho các hàm, nên các điểm sau dùng
giá trị "mặc định hợp lý" theo pattern SDK signotec phổ biến - nếu build lỗi
hoặc ảnh xuất ra sai, đây là chỗ cần xem lại đầu tiên (trong `SignDeviceController.cs`):

| Chỗ trong code | Giả định | Cần xác nhận |
|---|---|---|
| `DeviceOpen(_deviceIndex)` | `0` = thiết bị đầu tiên/duy nhất | Nếu lỗi, thử `DeviceGetCount()` để biết số thiết bị, hoặc `DeviceGetComPort(int)` |
| `SignatureSaveAsFileEx(path, 0, 0, 0, ImageFormat.Png, 100, Color.White, SignatureImageFlag.None)` | width/height/dpi=0 (auto), quality=100 | Mở Object Browser, double-click vào `SignatureSaveAsFileEx` trong class `STPadLib` để xem chính xác tên/thứ tự tham số nếu overload không khớp |
| `SignatureImageFlag.None` | Enum có giá trị `None` | Mở Object Browser, tìm `SignatureImageFlag` trong danh sách bên trái để xem các giá trị enum thật có sẵn |
| Giá trị trả về `0` từ `DeviceOpen` = thành công | Theo pattern phổ biến | Nếu SDK dùng exception (`STPadException`) thay vì return code khi lỗi, cần bọc thêm `try/catch (STPadException)` |

Nếu build báo lỗi `CS1501` (sai overload) hoặc `CS0117` (thiếu định nghĩa),
gửi nguyên văn lỗi để sửa tiếp.
