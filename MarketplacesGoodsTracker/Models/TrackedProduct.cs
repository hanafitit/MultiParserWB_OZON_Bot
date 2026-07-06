namespace MarketplacesGoodsTracker.Models;

public class TrackedProduct
{
    public int Id { get; set; }
    public long ChatId { get; set; }
    public string ProductUrl { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal TargetPrice { get; set; }
    public decimal LastKnownPrice { get; set; }
}
