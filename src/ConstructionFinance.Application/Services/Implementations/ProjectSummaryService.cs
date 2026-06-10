using System.Text.Json;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Services.Implementations;

public class ProjectSummaryService : IProjectSummaryService
{
    private readonly IProjectSummaryRepository _repo;
    private readonly IUnitOfWork _uow;

    public ProjectSummaryService(IProjectSummaryRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<int> SaveBatchAsync(ImportResultDto parsed, CancellationToken ct = default)
    {
        var warnings = parsed.Warnings.ToList();
        var existing = await _repo.FindByHashAsync(parsed.SourceFileHash, ct);
        if (existing.Count > 0)
        {
            var dupWarning =
                $"File này đã được import {existing.Count} lần trước (gần nhất: {existing[0].ImportedAtUtc:dd/MM/yyyy HH:mm}).";
            warnings.Add(dupWarning);
        }

        var batch = new ImportBatch
        {
            SourceFileName = parsed.SourceFileName,
            SourceFileHash = parsed.SourceFileHash,
            ImportedAtUtc = DateTime.UtcNow,
            SheetsFound = parsed.SheetsFound,
            ProjectsImported = parsed.Summaries.Count,
            WarningsJson = warnings.Count > 0 ? JsonSerializer.Serialize(warnings) : null
        };

        foreach (var dto in parsed.Summaries)
            batch.Summaries.Add(dto.ToEntity());

        await _repo.AddBatchAsync(batch, ct);
        await _uow.SaveChangesAsync(ct);
        return batch.Id;
    }

    public async Task<IReadOnlyList<ImportBatchDto>> ListBatchesAsync(CancellationToken ct = default)
    {
        var list = await _repo.ListBatchesAsync(ct);
        return list.Select(b => b.ToDto()).ToList();
    }

    public async Task<ImportBatchDto?> GetLatestBatchAsync(CancellationToken ct = default)
    {
        var batch = await _repo.GetLatestBatchAsync(ct);
        return batch?.ToDto();
    }

    public async Task<ImportBatchDto?> GetBatchByIdAsync(int batchId, CancellationToken ct = default)
    {
        var batch = await _repo.GetBatchByIdAsync(batchId, ct);
        return batch?.ToDto();
    }

    public async Task<IReadOnlyList<ProjectSummaryDto>> ListByBatchAsync(int batchId, CancellationToken ct = default)
    {
        var list = await _repo.ListByBatchAsync(batchId, ct);
        return list.Select(s => s.ToDto()).ToList();
    }

    public async Task DeleteBatchAsync(int batchId, CancellationToken ct = default)
    {
        await _repo.DeleteBatchAsync(batchId, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<TongHopAggregateDto> AggregateAsync(int batchId, CancellationToken ct = default)
    {
        var summaries = await _repo.ListByBatchAsync(batchId, ct);
        if (summaries.Count == 0)
        {
            return new TongHopAggregateDto(batchId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, Array.Empty<YearAggregateDto>());
        }

        var byYear = summaries
            .SelectMany(s => s.YearlySettlements)
            .GroupBy(y => y.Year)
            .OrderBy(g => g.Key)
            .Select(g => new YearAggregateDto(
                g.Key,
                g.Sum(x => x.KLHT ?? 0),
                g.Sum(x => x.Advance ?? 0),
                g.Sum(x => x.NSNN ?? 0),
                g.Sum(x => x.DTNH ?? 0)))
            .ToList();

        return new TongHopAggregateDto(
            batchId,
            summaries.Count,
            summaries.Sum(s => s.ContractValue),
            summaries.Sum(s => s.Advance),
            summaries.Sum(s => s.PaymentKLHT),
            summaries.Sum(s => s.RecoveredAdvance),
            summaries.Sum(s => s.PaymentToContractor),
            summaries.Sum(s => s.UnrecoveredAdvance),
            summaries.Sum(s => s.DisbursedNSNN),
            summaries.Sum(s => s.DisbursedDTNH),
            summaries.Sum(s => s.TotalDisbursed),
            byYear);
    }
}
