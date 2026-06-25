namespace Application.Common.DTOs;

// Validation lives in CreateOrderDtoValidator (FluentValidation) — this stays a pure data contract.
public class CreateOrderDto
{
    public string CartId { get; set; } = string.Empty;
    public int DeliveryMethodId { get; set; }
    public ShippingAddress ShippingAddress { get; set; } = null!;
    public PaymentSummary PaymentSummary { get; set; } = null!;
}
