using ConstructionFinance.Application.Import;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class ImportResolutionDialogViewModelTests
{
    private static ImportResolutionDialogViewModel CreateSut() =>
        new(new[]
        {
            new ImportCandidate(new Project { Name = "DA1" }, 5, ImportResolution.Overwrite),
            new ImportCandidate(new Project { Name = "DA9" }, null, ImportResolution.AddNew)
        });

    [Fact]
    public void Duplicates_default_to_overwrite_and_new_projects_to_add()
    {
        var sut = CreateSut();

        sut.Rows.Count.ShouldBe(2);
        sut.Rows[0].IsDuplicate.ShouldBeTrue();
        sut.Rows[0].Resolution.ShouldBe(ImportResolution.Overwrite);
        sut.Rows[1].IsDuplicate.ShouldBeFalse();
        sut.Rows[1].Resolution.ShouldBe(ImportResolution.AddNew);
    }

    [Fact]
    public void Counts_split_duplicates_from_new_projects()
    {
        var sut = CreateSut();

        sut.DuplicateCount.ShouldBe(1);
        sut.NewCount.ShouldBe(1);
    }

    [Fact]
    public void ToCandidates_carries_the_users_per_row_choice_back()
    {
        var sut = CreateSut();
        sut.Rows[0].Resolution = ImportResolution.Skip;

        var result = sut.ToCandidates();

        result[0].Resolution.ShouldBe(ImportResolution.Skip);
        result[0].ExistingId.ShouldBe(5);
        result[0].Parsed.Name.ShouldBe("DA1");
        result[1].Resolution.ShouldBe(ImportResolution.AddNew);
    }

    [Fact]
    public void A_new_project_row_cannot_be_switched_to_overwrite()
    {
        var sut = CreateSut();

        sut.Rows[1].Resolution = ImportResolution.Overwrite;

        // Nothing to overwrite → ToCandidates must keep ExistingId null so the
        // service falls through to the AddNew branch instead of updating id 0.
        sut.ToCandidates()[1].ExistingId.ShouldBeNull();
    }
}
