import { CartItem } from "./cartItem";
import { CartType } from "./cartType";
import { nanoid } from "nanoid";

export class Cart implements CartType {
    // nanoid(10) //=> "IRFa-VaY2b"
    id: string = nanoid(); //=> "V1StGXR8_Z5jdHi6B-myT"
    items: CartItem[] = [];

    // Added for Stripe integration
    deliveryMethodId?: number;
    paymentIntentId?: string;
    clientSecret?: string;
}