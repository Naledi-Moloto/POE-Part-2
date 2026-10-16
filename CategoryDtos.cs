using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

public class CategoryDto
{
    public int CategoryID { get; set; }
    public int EventID { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal DistanceKM { get; set; }
    public int? MinAge { get; set; }
    public decimal EntryFee { get; set; }
}

public class SaveCategoryDto
{
    [Required, MaxLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    [Range(0.01, 999.99)]
    public decimal DistanceKM { get; set; }

    [Range(0, 120)]
    public int? MinAge { get; set; }

    [Range(0, 99999.99)]
    public decimal EntryFee { get; set; }
}
