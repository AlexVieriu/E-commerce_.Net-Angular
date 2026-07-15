import { OrderStatus } from '../order/order';

// Mirrors Application.Common.DTOs.RecentOrderDto — a lean projection, NOT the full Order.
// No nullable fields: BuyerEmail and Status are `required string` on the backend.
export type RecentOrder = {
  id: number;
  buyerEmail: string;
  orderDate: string;
  total: number;
  status: OrderStatus;
};
