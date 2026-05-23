using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Data.Seed;

public static class CostUnitTypeSeeder
{
    private static readonly (string Code, string Name)[] Defaults =
    {
        ("m2", "Mét vuông"),
        ("m3", "Mét khối"),
        ("kg", "Ki-lô-gam"),
        ("tan", "Tấn"),
        ("pcs", "Cái"),
        ("bo", "Bộ"),
        ("m", "Mét"),
        ("md", "Mét dài"),
        ("tg", "Trọn gói"),
        ("cong", "Công"),
        ("cm", "Ca máy"),
        ("pct", "Phần trăm")
    };

    public static void Seed(AppDbContext ctx)
    {
        var existing = ctx.CostUnitTypes.Select(u => u.Code).ToHashSet();
        foreach (var (code, name) in Defaults)
        {
            if (!existing.Contains(code))
            {
                ctx.CostUnitTypes.Add(new CostUnitType { Code = code, Name = name });
            }
        }
        ctx.SaveChanges();
    }
}
