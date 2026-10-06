using System.ComponentModel;
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.Tests.Common;

internal static class PrintFakes
{
    public static PrintSheet Sheet(string name) => new PrintTableSheet(
        name,
        PrintPageProfiles.ViTriLedger,
        new[] { new PrintColumn("Cột", 10, PrintAlign.Left) },
        Array.Empty<PrintRow>());
}

    /// Màn in được. PrintSheetName đổi được lúc chạy và bắn PropertyChanged như màn thật.
    internal class FakePrintablePage : IPrintablePage, INotifyPropertyChanged
    {
        private string? _sheetName;

        public FakePrintablePage(string? sheetName = "Vị trí 1") => _sheetName = sheetName;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string? PrintSheetName => _sheetName;

        /// Mô phỏng một màn vi phạm hợp đồng IPrintablePage: có tên mà không trả được sheet.
        public bool ReturnsNoSheet { get; init; }

        /// Sheet mà lần BuildPrintSheet gần nhất đã trả (null nếu chưa gọi lần nào).
        public PrintSheet? BuiltSheet { get; private set; }

        public int BuildCalls { get; private set; }

        /// Khác null thì BuildPrintSheet ném: mô phỏng lỗi trong code của chính màn.
        public Exception? BuildFailure { get; init; }

        public PrintSheet? BuildPrintSheet()
        {
            BuildCalls++;
            if (BuildFailure is not null) throw BuildFailure;
            if (ReturnsNoSheet || _sheetName is null) return null;
            return BuiltSheet = PrintFakes.Sheet(_sheetName);
        }

        public void SetSheetName(string? sheetName)
        {
            _sheetName = sheetName;
            Raise(nameof(PrintSheetName));
        }

        public void RaiseOther(string propertyName) => Raise(propertyName);

        /// Số handler đang nghe màn này: chứng cứ trực tiếp cho việc gắn/gỡ có đi đôi hay không.
        public int SubscriberCount => PropertyChanged?.GetInvocationList().Length ?? 0;

        protected void Raise(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    internal sealed class FakePrintableWorkbook : FakePrintablePage, IPrintableWorkbook
    {
        private bool _canPrintAll;

        public FakePrintableWorkbook(string? sheetName = "Vị trí 1", bool canPrintAll = true) : base(sheetName) =>
            _canPrintAll = canPrintAll;

        public string PrintWorkbookName { get; init; } = "DA-01";

        /// Tên dự án (KHÁC mã trong PrintWorkbookName, cố ý) cho chân trang, để test thấy lỗi dùng nhầm cái này thay cái kia.
        public string PrintFooterProjectName { get; init; } = "Cầu Thủ Thiêm";

        /// true thì BuildAllSheets() trả về một phần tử trong Skipped, để test dựng được cảnh báo
        /// "đã bỏ qua" mà không phải dựng cả một workbook thật.
        public bool SkipGdntt { get; set; }

        public bool CanPrintAll => _canPrintAll;

        public List<PrintSheet> AllSheets { get; } = new();

        /// Các trang "In toàn bộ" đã bỏ qua vì không dựng được sheet.
        public List<SkippedPrintPage> Skipped { get; } = new();

        public int BuildAllCalls { get; private set; }

        /// Khác null thì BuildAllSheets ném (vd. làm mới một tab tổng hợp hỏng).
        public Exception? BuildAllFailure { get; init; }

        public PrintAllResult BuildAllSheets()
        {
            BuildAllCalls++;
            if (BuildAllFailure is not null) throw BuildAllFailure;
            return new PrintAllResult(
                AllSheets,
                SkipGdntt ? new List<SkippedPrintPage> { new("GĐNTT", "chưa chọn gói thầu") } : Skipped);
        }

        public void SetCanPrintAll(bool value)
        {
            _canPrintAll = value;
            Raise(nameof(CanPrintAll));
        }
    }

    internal sealed class FakeWriter : IPrintWorkbookWriter
    {
        public List<PrintSheet> WrittenSheets { get; } = new();

        public string? WrittenPath { get; private set; }

        public PrintFooterInfo? WrittenFooter { get; private set; }

        public int WriteCalls { get; private set; }

        /// Gọi mỗi lần WriteAsync được gọi, để test ghi lại THỨ TỰ so với việc hiện thông báo và mở tệp.
        public Action? OnWrite { get; init; }

        /// Khác null thì WriteAsync trả một Task lỗi (như bộ ghi thật, vốn chạy trong Task.Run).
        public Exception? FailWith { get; init; }

        public void Write(IReadOnlyList<PrintSheet> sheets, Stream target, PrintFooterInfo footer) =>
            throw new NotSupportedException("Lệnh In chỉ được gọi WriteAsync.");

        public Task WriteAsync(
            IReadOnlyList<PrintSheet> sheets,
            string targetPath,
            PrintFooterInfo footer,
            CancellationToken ct = default)
        {
            WriteCalls++;
            OnWrite?.Invoke();
            if (FailWith is not null) return Task.FromException(FailWith);

            WrittenSheets.AddRange(sheets);
            WrittenPath = targetPath;
            WrittenFooter = footer;
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeLauncher : IFileLauncher
    {
        public string? Opened { get; private set; }

        public bool Result { get; init; } = true;

        /// Gọi mỗi lần TryOpen được gọi, để test ghi lại THỨ TỰ so với ghi tệp và hiện thông báo.
        public Action? OnOpen { get; init; }

        public bool TryOpen(string path)
        {
            Opened = path;
            OnOpen?.Invoke();
            return Result;
        }
    }
