import { Routes } from '@angular/router';
import { HomeComponent } from './features/home/home.component';
import { ShopComponent } from './features/shop/shop.component';
import { ProductDetailsComponent } from './features/shop/product-details/product-details.component';
import { NotFoundComponent } from './shared/components/not-found/not-found.component';
import { ServerErrorComponent } from './shared/components/server-error/server-error.component';
import { TestErrorComponent } from './features/test-error/test-error.component';
import { CartComponent } from './features/cart/cart.component';
import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';

export const routes: Routes = [
    { path: '', component: HomeComponent },         // Default route
    { path: 'shop', component: ShopComponent },
    { path: 'shop/:id', component: ProductDetailsComponent },
    { path: 'checkout', loadChildren: () => import('./features/checkout/routes').then(m => m.checkoutRoutes) },
    { path: 'orders', loadChildren: () => import('./features/orders/routes').then(m => m.orderRoutes) },
    { path: 'cart', component: CartComponent },
    { path: 'account', loadChildren: () => import('./features/account/routes').then(m => m.accountRoutes) },
    { path: 'test-error', component: TestErrorComponent },
    { path: 'not-found', component: NotFoundComponent },
    { path: 'server-error', component: ServerErrorComponent },
    {
        path: 'admin', loadComponent: () => import('./features/admin/admin.component')
            .then(c => c.AdminComponent), canActivate: [authGuard, adminGuard]
    },
    { path: '**', redirectTo: 'not-found', pathMatch: 'full' }, // Default redirect for unmatched routes
];