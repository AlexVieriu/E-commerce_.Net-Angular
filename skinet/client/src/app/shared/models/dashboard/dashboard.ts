import { LowStockProduct } from './lowStockProduct';
import { RecentOrder } from './recentOrder';

// Mirrors Application.Common.DTOs.DashboardDto — the whole admin dashboard in one GET.
export type Dashboard = {
  totalProducts: number;
  totalOrders: number;
  pendingOrders: number;
  totalRevenue: number;
  // Every product at or below the stock threshold (5).
  lowStockCount: number;
  // Only the 10 scarcest of them, so this is shorter than lowStockCount.
  lowStockProducts: LowStockProduct[];
  // The 5 newest orders.
  recentOrders: RecentOrder[];
};
