using ConstructionFinance.Application.Printing;
using ConstructionFinance.Printing.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Desktop.Common;

/// Thân của HostBuilder.AddPrinterServices — nửa Windows của bộ đăng ký dịch vụ.
///
/// KHÔNG có một dòng tiền xử lý nào trong file này, CỐ Ý. Bản trước gói hai lệnh đăng ký
/// trong `#if NET8_0_WINDOWS`, một ký hiệu .NET SDK không hề định nghĩa; `#if` luôn sai thì
/// không có cảnh báo, nên hai lệnh ấy chưa từng được biên dịch và tính năng In ra giấy chết
/// hẳn trong bản .exe đã ship — build xanh, test xanh, không dấu hiệu gì. Lỗi IM LẶNG.
///
/// Thay cho một ký hiệu khác, việc "chỉ có ở Windows" giờ do .csproj quyết định: file này
/// được khai trong ĐÚNG CÁI ItemGroup mang ProjectReference có điều kiện tới
/// ConstructionFinance.Printing.Windows (và bị `Compile Remove` ở ngoài điều kiện để glob
/// mặc định của SDK không kéo nó vào cả hai đích). ĐỪNG TÁCH HAI THỨ ĐÓ RA HAI ItemGroup:
/// chúng phải cùng xuất hiện hoặc cùng mất. Nếu ProjectReference biến mất mà file này còn
/// lại, hai tên WindowsPrinterPort/WindowsPrinterCatalog dưới đây làm build HỎNG NGAY —
/// đó mới là hành vi muốn có, thay vì lặng lẽ ship một tính năng không chạy.
public static partial class HostBuilder
{
    static partial void AddPrinterServices(IServiceCollection services)
    {
        services.AddSingleton<IPrinterPort, WindowsPrinterPort>();
        services.AddSingleton<IPrinterCatalog, WindowsPrinterCatalog>();
    }
}
