import { Route } from "@angular/router";
import { CheckoutComponent } from "./checkout.component";
import { CheckoutSuccessComponent } from "./checkout-success/checkout-success.component";
import { authGuard } from "../../core/guards/auth.guard";
import { orderCompleteGuard } from "../../core/guards/order-complete.guard";
import { emptycartGuard } from "../../core/guards/emptycart.guard";

export const checkoutRoutes: Route[] = [
    { path: '', component: CheckoutComponent, canActivate: [authGuard, emptycartGuard] },
    { path: 'success', component: CheckoutSuccessComponent, canActivate: [authGuard, orderCompleteGuard] },
];