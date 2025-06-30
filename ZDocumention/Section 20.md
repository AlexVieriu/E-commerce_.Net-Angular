Getting the requirements:
Course Assets -> challenges -> The_Coupon challenge .pdf 

1. Business Requirements
-> the Skinet-app needs a coupon feature 
-> customers should be able to apply coupons at checkout or in their cart view, 
see discounted prices and see error messages for invalid coupons
-> the feature must ensure quick validation, security of coupon, and user-friendly interface

2. General guidance and research

a. Usage is Stripe API documentation docs will be necessary to complete this task
https://docs.stripe.com/api/coupons
https://docs.stripe.com/api/promotion_codes

b. Start from bottom and work your way up:
Core -> Infrastructure -> API -> Client

c. Demo project to see it in action: 
https://skinet-course.azurewebsites.net/

Go to Product Catalog -> Coupons -> Create new coupon.
Create 2 coupons that both have a customer facing coupon code.   
We will be supporting both percentage discount and fixed amount off in the app.  

Suggested coupons:
GIMME10 : 10% discount
GIMME5 : $5 discount

-- Core project --
1. Create a new Class for Coupon: AppCoupon(Stripes already have a coupon object in the link above)
2. Add AppCoupon as a new property in ShoppingCart
3. New interface: ICouponService (method: AppCoupon? GetCouponFromPromoCode(string code))
4. Update Order class and add Discount property that is a decimal
5. Update the GetTotal method int the Order class:
public decimal GetTotal() => (Subtotal + DeliveryMethod.Price) * (1 - Discount / 100);

-- Infrastructure project --
1. Services -> CouponService
-> use this documentation link to get all the coupons (not only one)
https://docs.stripe.com/api/promotion_codes/list?lang=dotnet

-> Get all coupon codes first:
--Stripe documentation --
StripeConfiguration.ApiKey = "sk_test_51R3BtWGMOucV11LeOaDO00p6Ffd5e02R1j5xYUhcNejDA2TJOskB7saG2S2AtI5CaOq1mC1dpSNMkTJMntEP3qVe0053pPawgf";

var options = new PromotionCodeListOptions { Limit = 3 };
var service = new PromotionCodeService();
StripeList<PromotionCode> promotionCodes = service.List(options);

-> then get the first coupon
-> the the data/properties from Stripes coupon to AppCoupon

2. Update the PaymentService.cs to accommodate the discount
-> refactor CreateOrUpdatePaymentIntent() to small methods 

New methods:
GetShippingPriceAsync(cart)
ValidateCartItemsInCartAsync(cart)
CalculateSubtotal(cart)
ApplyDiscountAsync(cart.Coupon, subtotal)
CreateUpdatePaymentIntentAsync(cart, total)

3. Implement the ApplyDiscountAsync method to update the intent with the discounted price
4. Update the OrderConfiguration to accommodate the new decimal property for the Discount
5. Create a new EF migration at this point to update the DB schema. 

dotnet ef migrations add FixDecimalPrecision2 --context SqliteStoreContext -s API -p Infrastructure -o Migrations/SQLite
dotnet watch
    -> database will update automatically when we start the program 

dotnet ef migrations add FixDecimalPrecision --context SqlServerStoreContext -s API -p Infrastructure -o Migrations/SqlServer
dotnet publish -c Release
    -> database will update automatically when we start the program


-- API project --

1. Update the Program.cs to register the new ICouponService and it's implementation
2. Update the OrderDto to include the Discount property
3. Update the OrderMappingExtension to incorporate this Discount property
4. Create a CouponsController with the endpoint:

[HttpGet("{code}")]
public async Task<ActionResult<AppCoupon>> ValidateCoupon(string code)
{
    // implement the logic for this
    // return bad request for invalid coupon
    // tip: get if from ICouponService
}

5. Run the request in Postman:
{{localhost}}/api/Coupons/gimme10
{{localhost}}/api/coupons/gimme5
{{localhost}}/api/coupons/InvalidCoupon

Tip: if is development, don't use app.UseHttpsRedirection();

6. Make sure you return a BadRequest for the second request
7. Update the CreateOrderDto to take an decimal optional Discount property
8. Update the OrdersController to use this new property
9. Certain order amounts may cause a rounding difference when comparing the Order amount to the Intent amount.
This function will will ensure that the amounts are using the same rounding method:

-- API/Controllers/PaymentsController.cs --
var orderTotalInCents = (long)Math.Round(order.GetTotal() * 100, MidpointRounding.AwayFromZero); 