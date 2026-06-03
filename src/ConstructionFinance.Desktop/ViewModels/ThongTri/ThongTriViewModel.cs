using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.ThongTri;

public partial class ThongTriViewModel : ViewModelBase
{
    private readonly IProjectSummaryService _summaries;

    public ThongTriViewModel(IProjectSummaryService summaries) => _summaries = summaries;

    public ObservableCollection<ProjectSummaryDto> Projects { get; } = new();

    [ObservableProperty] private ProjectSummaryDto? _selectedProject;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _statusMessage = "Chọn một dự án để xem thông tin Thông tri.";

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            Projects.Clear();
            SelectedProject = null;
            var batch = await _summaries.GetLatestBatchAsync();
            if (batch is null)
            {
                StatusMessage = "Chưa có dữ liệu import. Hãy Import Excel trước.";
                return;
            }

            var list = await _summaries.ListByBatchAsync(batch.Id);
            foreach (var p in list) Projects.Add(p);
            StatusMessage = list.Count > 0
                ? $"Batch: {batch.SourceFileName} ({batch.ImportedAtUtc:dd/MM/yyyy HH:mm})"
                : "Batch không có dự án.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    partial void OnSelectedProjectChanged(ProjectSummaryDto? value)
    {
        StatusMessage = value is null
            ? "Chọn một dự án để xem chi tiết."
            : $"Đang xem: {value.ProjectName}";
    }
}
