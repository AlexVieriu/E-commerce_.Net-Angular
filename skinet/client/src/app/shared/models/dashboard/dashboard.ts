import { LowStockProduct } from './lowStockProduct';
import { RecentOrder } from './recentOrder';

// Mirrors Application.Common.DTOs.DashboardDto — the whole admin dashboard in one GET.
// lowStockCount counts EVERY product at or below the threshold; lowStockProducts is capped
// server-side (LowStockListSize = 10), so the two can differ. recentOrders is capped at 5.
export type Dashboard = {
  totalProducts: number;
  totalOrders: number;
  pendingOrders: number;
  totalRevenue: number;
  lowStockCount: number;
  lowStockProducts: LowStockProduct[];
  recentOrders: RecentOrder[];
};
