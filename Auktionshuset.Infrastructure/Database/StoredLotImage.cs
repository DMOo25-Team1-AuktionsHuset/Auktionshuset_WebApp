namespace Auktionshuset.Infrastructure.Database;

/// <summary>Image bytes shared by every API instance through PostgreSQL.</summary>
public sealed class StoredLotImage
{
    public required string FileName { get; set; }
    public required byte[] Content { get; set; }
    public required string ContentType { get; set; }
}
