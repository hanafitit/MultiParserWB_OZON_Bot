namespace MarketplacesGoodsTracker.Models;

public class WbProductResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal SalePrice { get; set; }
    public double Rating { get; set; }
    public int Feedbacks { get; set; }
    public int Stocks { get; set; }
}
