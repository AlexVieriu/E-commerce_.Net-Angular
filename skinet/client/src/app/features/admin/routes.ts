import { Routes } from '@angular/router';
import { AdminComponent } from './admin.component';
import { AdminDashboardComponent } from './dashboard/admin-dashboard.component';
import { AdminProductsComponent } from './products/admin-products.component';
import { ProductFormComponent } from './products/product-form.component';
import { AdminOrdersComponent } from './orders/admin-orders.component';

// Admin shell (AdminComponent) + children rendered in its <router-outlet>.
// The parent /admin route is guarded in app.routes.ts; children inherit that guard.
export const adminRoutes: Routes = [
  {
    path: '',
    component: AdminComponent,
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: AdminDashboardComponent },
      { path: 'products', component: AdminProductsComponent },
      { path: 'products/new', component: ProductFormComponent },
      { path: 'products/:id/edit', component: ProductFormComponent },
      { path: 'orders', component: AdminOrdersComponent }
    ]
  }
];
