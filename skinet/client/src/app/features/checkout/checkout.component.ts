import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  signal
} from '@angular/core';
import { OrderSummaryComponent } from '../../shared/components/order-summary/order-summary.component';
import { MatStepper, MatStepperModule } from '@angular/material/stepper';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButton } from '@angular/material/button';
import { StripeService } from '../../core/services/stripe.service';
import {
  ConfirmationToken,
  StripeAddressElement,
  StripeAddressElementChangeEvent,
  StripePaymentElement,
  StripePaymentElementChangeEvent
} from '@stripe/stripe-js';
import { SnackbarService } from '../../core/services/snackbar.service';
import { MatCheckboxChange, MatCheckboxModule } from '@angular/material/checkbox';
import { StepperSelectionEvent } from '@angular/cdk/stepper';
import { Address } from '../../shared/models/address';
import { AccountService } from '../../core/services/account.service';
import { firstValueFrom } from 'rxjs';
import { CheckoutDeliveryComponent } from './checkout-delivery/checkout-delivery.component';
import { CheckoutReviewComponent } from './checkout-review/checkout-review.component';
import { CartService } from '../../core/services/cart.service';
import { CurrencyPipe } from '@angular/common';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ShippingAddress } from '../../shared/models/order/shippingAddress';
import { OrderToCreate } from '../../shared/models/order/orderToCreate';
import { Order } from '../../shared/models/order/order';
import { OrderService } from '../../core/services/order.service';

@Component({
  selector: 'app-checkout',
  imports: [
    OrderSummaryComponent,
    MatStepperModule,
    RouterLink,
    MatButton,
    MatCheckboxModule,
    CheckoutDeliveryComponent,
    CheckoutReviewComponent,
    CurrencyPipe,
    MatProgressSpinnerModule
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './checkout.component.html',
  styleUrl: './checkout.component.scss'
})
export class CheckoutComponent {
  private stripeService = inject(StripeService);
  private snackBar = inject(SnackbarService);
  private accountService = inject(AccountService);
  private router = inject(Router);
  private orderService = inject(OrderService);
  cartService = inject(CartService);

  confirmationToken = signal<ConfirmationToken | undefined>(undefined);
  addressElement?: StripeAddressElement;
  paymentElement?: StripePaymentElement;
  saveAddress = signal(false);
  loading = signal(false);

  // as the elements get completed we update our signal
  // inline type annotation - Angular signal (we use it instead of interface)
  completionStatus = signal<{ address: boolean; card: boolean; delivery: boolean }>({
    address: false,
    card: false,
    delivery: false
  });

  constructor() {
    afterNextRender(async () => {
      try {
        this.addressElement = await this.stripeService.createAddressElement();
        this.addressElement.mount('#address-element');
        this.addressElement.on('change', this.handleAddressChange);

        this.paymentElement = await this.stripeService.createPaymentElement();
        this.paymentElement?.mount('#payment-element');
        this.paymentElement.on('change', this.handlePaymentChange);
      } catch (error: unknown) {
        // A dead coupon 409s here; the interceptor already messaged and left for the cart.
        if (error instanceof HttpErrorResponse && error.status === 409) return;
        this.snackBar.error(
          error instanceof Error ? error.message : 'Failed to load payment elements'
        );
      }
    });

    inject(DestroyRef).onDestroy(() => this.stripeService.disposeElements());
  }

  handleAddressChange = (event: StripeAddressElementChangeEvent) => {
    this.completionStatus.update((state) => ({ ...state, address: event.complete }));
  };

  handlePaymentChange = (event: StripePaymentElementChangeEvent) => {
    this.completionStatus.update((state) => ({ ...state, card: event.complete }));
  };

  handleDeliveryChange(event: boolean) {
    this.completionStatus.update((state) => ({ ...state, delivery: event }));
  }

  onSaveAddressCheckboxChange(event: MatCheckboxChange) {
    this.saveAddress.set(event.checked);
  }

  async onStepChange(event: StepperSelectionEvent) {
    // index = 1 -> i moved to the next step
    if (event.selectedIndex === 1) {
      if (this.saveAddress()) {
        // update the user account Address from the API
        const address = (await this.getAddressFromStripeAddress()) as Address;
        if (address) firstValueFrom(this.accountService.updateAddress(address));
      }
    }

    if (event.selectedIndex === 2) {
      try {
        await firstValueFrom(this.stripeService.createOrUpdatePaymentIntent());
      } catch (error: unknown) {
        if (error instanceof HttpErrorResponse && error.status === 409) return;
        throw error;
      }
    }

    if (event.selectedIndex === 3) await this.getConfirmationToken();
  }

  async getConfirmationToken() {
    try {
      // check if every status in completionStatus(address, card, delivery) is true
      const everyStatusTrue = Object.values(this.completionStatus()).every(
        (status) => status == true
      );
      if (everyStatusTrue) {
        const result = await this.stripeService.createToken();
        if (result.error) throw new Error(result.error.message);
        this.confirmationToken.set(result.confirmationToken);
      }
    } catch (error: unknown) {
      this.snackBar.error(error instanceof Error ? error.message : 'Something went wrong');
    }
  }

  async confirmPayment(stepper: MatStepper) {
    this.loading.set(true);
    // if they have a problem with the credit card, go back to payment page
    try {
      if (this.confirmationToken()) {
        // Create the order FIRST (status Pending) so the Stripe webhook always finds it.
        // Otherwise payment_intent.succeeded can arrive before the order exists (a race
        // that loses on slow clients), leaving the order stuck Pending with no SignalR push.
        // Bonus: price/stock validation now runs *before* the customer is charged.
        const order = await this.createOrderModel();
        const createdOrder = (await firstValueFrom(this.orderService.createOrder(order))) as Order;
        if (!createdOrder) throw new Error('Order creation failed');
        this.orderService.createdOrder = createdOrder;

        const result = await this.stripeService.confirmPayment(this.confirmationToken()!);

        if (result.paymentIntent?.status === 'succeeded') {
          this.orderService.orderComplete = true;
          this.cartService.deleteCart();
          this.cartService.setSelectedDelivery(null);
          this.router.navigateByUrl('/checkout/success');
        } else if (result.error) throw new Error(result.error.message);
        else throw new Error('Something went wrong with the payment confirmation');
      }
    } catch (error: unknown) {
      if (error instanceof HttpErrorResponse && error.status === 409) return;
      this.snackBar.error(error instanceof Error ? error.message : 'Something went wrong');
      stepper.previous(); // go to Step 2, then can go to Step 3 to generate new token
    } finally {
      this.loading.set(false);
    }
  }

  private async getAddressFromStripeAddress(): Promise<Address | ShippingAddress | null> {
    const result = await this.addressElement?.getValue();
    const address = result?.value.address;
    if (address) {
      return {
        name: result?.value.name,
        line1: address.line1,
        line2: address.line2 || undefined,
        city: address.city,
        state: address.state,
        postalCode: address.postal_code,
        country: address.country
      };
    } else return null;
  }

  private async createOrderModel(): Promise<OrderToCreate> {
    const cart = this.cartService.cart();
    const shippingAddress = (await this.getAddressFromStripeAddress()) as ShippingAddress;
    const card = this.confirmationToken()?.payment_method_preview.card;

    if (!cart || !cart.deliveryMethodId || !shippingAddress || !card)
      throw new Error('Problem creating order');

    return {
      paymentSummary: {
        last4: +card.last4,
        brand: card.brand,
        expMonth: card.exp_month,
        expYear: card.exp_year
      },
      deliveryMethodId: cart.deliveryMethodId,
      shippingAddress
    };
  }
}
