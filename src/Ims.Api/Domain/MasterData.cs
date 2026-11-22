namespace Ims.Api.Domain;

/// <summary>Shared columns of master-data tables. Rows in use are soft-deleted (IsDeleted) instead of removed.</summary>
public abstract class MasterDataEntity
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Product : MasterDataEntity { }

public class Component : MasterDataEntity { }

public class Point : MasterDataEntity
{
    public PointType PointType { get; set; }
}
