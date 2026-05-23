using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Domain.Entities;

public class Attachment
{
    public int Id { get; set; }
    public AttachmentEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public required string FileName { get; set; }
    public required string FilePath { get; set; }
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public string? Description { get; set; }
    public DateTime UploadedAt { get; set; }
}
