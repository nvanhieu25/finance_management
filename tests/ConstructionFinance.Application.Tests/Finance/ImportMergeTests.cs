using ConstructionFinance.Application.Import;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Finance;

public class ImportMergeTests
{
    [Fact]
    public void Classify_marks_a_brand_new_project_as_AddNew_with_no_existing_id()
    {
        var parsed = new[] { new Project { Name = "DA9" } };
        var existing = new[] { new Project { Id = 1, Name = "DA1" } };

        var result = ImportMerge.Classify(parsed, existing);

        result.Count.ShouldBe(1);
        result[0].ExistingId.ShouldBeNull();
        result[0].Resolution.ShouldBe(ImportResolution.AddNew);
    }

    [Fact]
    public void Classify_matches_an_existing_name_ignoring_case_and_padding()
    {
        var parsed = new[] { new Project { Name = "  da1 " } };
        var existing = new[] { new Project { Id = 5, Name = "DA1" } };

        var result = ImportMerge.Classify(parsed, existing);

        result[0].ExistingId.ShouldBe(5);
        result[0].Resolution.ShouldBe(ImportResolution.Overwrite); // duplicates default to overwrite
    }

    [Fact]
    public void NextFreeName_appends_2_then_keeps_counting_until_free()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DA1", "DA1 (2)", "DA1 (3)" };

        ImportMerge.NextFreeName("DA1", taken).ShouldBe("DA1 (4)");
    }

    [Fact]
    public void NextFreeName_returns_the_base_name_when_it_is_free()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DA2" };

        ImportMerge.NextFreeName("DA1", taken).ShouldBe("DA1");
    }
}
