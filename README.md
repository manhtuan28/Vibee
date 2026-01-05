# 🐝 Vibee - Social Media & Digital Wallet

![.NET Core](https://img.shields.io/badge/.NET%20Core-ASP.NET%20MVC-purple)
![Database](https://img.shields.io/badge/Database-SQL%20Server-red)
![Frontend](https://img.shields.io/badge/Frontend-Bootstrap%205-blue)
![License](https://img.shields.io/badge/License-MIT-green)

**Vibee** là một nền tảng "All-in-One" kết hợp sức mạnh kết nối của Mạng xã hội (Social Media) và sự tiện lợi của Ví điện tử (Digital Wallet). Dự án được xây dựng với kiến trúc hiện đại, bảo mật và hiệu năng cao trên nền tảng **ASP.NET Core**.

## 🚀 Tính năng nổi bật (Key Features)

### 🌏 Mạng xã hội (Social Networking)
* **Newsfeed thông minh:** Cập nhật trạng thái, hình ảnh, video với giao diện 3 cột hiện đại (giống Facebook Desktop).
* **Tương tác:** Like, Comment, Share bài viết thời gian thực.
* **Hệ thống kết bạn:** Gửi lời mời, chấp nhận, hủy kết bạn hoặc chặn người dùng.
* **Profile cá nhân:** Trang cá nhân tùy biến với Avatar, Cover Photo và Bio.
* **Real-time Notification:** Thông báo tức thì khi có tương tác mới (sử dụng SignalR).

### 💳 Ví điện tử Vibee (Digital Wallet)
* **Tích hợp sâu:** Mỗi tài khoản Vibee tự động sở hữu một ví điện tử ngay khi đăng ký.
* **Chuyển tiền P2P:** Chuyển tiền cho bạn bè trong tích tắc chỉ bằng ID hoặc QR Code.
* **Bảo mật 2 lớp:** Yêu cầu mã PIN 6 số cho mọi giao dịch tài chính.
* **Lịch sử giao dịch:** Theo dõi biến động số dư chi tiết (Nạp, Rút, Chuyển khoản).
* **An toàn dữ liệu:** Sử dụng Transaction (ACID) đảm bảo tính toàn vẹn của tiền tệ.

## 🛠️ Công nghệ sử dụng (Tech Stack)

* **Backend:** ASP.NET Core MVC (.NET 10), C#.
* **Database:** SQL Server, Entity Framework Core (Code-First).
* **Identity:** ASP.NET Core Identity (Customized User & Security).
* **Frontend:** HTML5, CSS3, Bootstrap 5, JavaScript (ES6+), JQuery.
* **Real-time:** SignalR.
* **Libraries:** QRCoder (Tạo mã QR thanh toán), SweetAlert2 (Thông báo đẹp).

## ⚙️ Cài đặt & Chạy dự án (Installation)

Để chạy dự án này trên máy local, bạn cần thực hiện các bước sau:

1.  **Clone Repository:**
    ```bash
    git clone [https://github.com/manhtuan28/Vibee.git](https://github.com/manhtuan28/Vibee.git)
    cd Vibee
    ```

2.  **Cấu hình Database:**
    * Mở file `appsettings.json`.
    * Chỉnh sửa `ConnectionStrings:DefaultConnection` trỏ về SQL Server của bạn.

3.  **Chạy Migrations (Tạo Database):**
    Mở **Package Manager Console** trong Visual Studio và chạy lệnh:
    ```powershell
    Update-Database
    ```

4.  **Chạy ứng dụng:**
    Nhấn `F5` hoặc `Ctrl + F5` trong Visual Studio để khởi chạy.

## 📸 Screenshots (Một số hình ảnh)

*(Đang cập nhật)*

## 🤝 Đóng góp (Contributing)

Mọi đóng góp đều được hoan nghênh! Nếu bạn tìm thấy lỗi hoặc muốn thêm tính năng mới, hãy tạo **Pull Request**.

## 📄 License

Dự án này được phân phối dưới giấy phép **MIT License**. Xem file `LICENSE` để biết thêm chi tiết.

---
Developed with ❤️ by **Tuancute** (manhtuan28).