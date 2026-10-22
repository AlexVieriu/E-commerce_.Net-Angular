import { adminGuard } from './core/guards/admin.guard';
import { authGuard } from './core/guards/auth.guard';
import { NotFoundComponent } from './shared/components/not-found/not-found.component';
import { Routes } from '@angular/router';
import { ServerErrorComponent } from './shared/components/server-error/server-error.component';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/home/home.component').then((c) => c.HomeComponent)
  },
  {
    path: 'shop',
    loadComponent: () => import('./features/shop/shop.component').then((c) => c.ShopComponent)
  },
  {
    path: 'shop/:id',
    loadComponent: () =>
      import('./features/shop/product-details/product-details.component').then(
        (c) => c.ProductDetailsComponent
      )
  },
  {
    path: 'checkout',
    loadChildren: () => import('./features/checkout/routes').then((m) => m.checkoutRoutes)
  },
  {
    path: 'orders',
    loadChildren: () => import('./features/orders/routes').then((m) => m.orderRoutes)
  },
  {
    path: 'cart',
    loadComponent: () => import('./features/cart/cart.component').then((c) => c.CartComponent)
  },
  {
    path: 'account',
    loadChildren: () => import('./features/account/routes').then((m) => m.accountRoutes)
  },
  { path: 'not-found', component: NotFoundComponent },
  { path: 'server-error', component: ServerErrorComponent },
  {
    path: 'admin',
    canActivate: [authGuard, adminGuard],
    loadChildren: () => import('./features/admin/routes').then((m) => m.adminRoutes)
  },
  { path: '**', redirectTo: 'not-found', pathMatch: 'full' }
];
