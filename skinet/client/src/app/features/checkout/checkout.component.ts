import { Component, inject, OnInit } from '@angular/core';
import { OrderSummaryComponent } from "../../shared/components/order-summary/order-summary.component";
import { MatStepperModule } from '@angular/material/stepper';
import { RouterLink } from '@angular/router';
import { MatButton } from '@angular/material/button';
import { StripeService } from '../../core/services/stripe.service';
import { StripeAddressElement } from '@stripe/stripe-js';
import { SnackbarService } from '../../core/services/snackbar.service';

@Component({
  selector: 'app-checkout',
  imports: [OrderSummaryComponent, MatStepperModule, RouterLink, MatButton],
  templateUrl: './checkout.component.html',
  styleUrl: './checkout.component.scss'
})
export class CheckoutComponent implements OnInit {

  private stripeService = inject(StripeService);
  private snackBar = inject(SnackbarService);
  addressElement?: StripeAddressElement

  async ngOnInit() {
    try {
      this.addressElement = await this.stripeService.createAddressElement();
      this.addressElement.mount('#address-element'); // use # because is an id property
    }
    catch (error: any) {
      this.snackBar.error(error.message);
    }
  }
}
