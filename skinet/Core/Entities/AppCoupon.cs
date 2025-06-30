namespace Core.Entities;

public class AppCoupon
{
   public string? Name { get; set; } // "Summer Sale 25% Off"
   public decimal? AmountOff { get; set; } // 1000 (represents $10.00 in cents)
   public decimal? PercentOff { get; set; } // 25.5 (represents 25.5% discount)
   public required string PromotionCode { get; set; } // "SUMMER2024"
   public string? Id { get; set; } // "SAVE25" or "jMT0WJUD"
   public string? Currency { get; set; } // "usd", "eur", "gbp"
}