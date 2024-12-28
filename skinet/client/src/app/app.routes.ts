import { Routes } from '@angular/router';
import { HomeComponent } from './features/home/home.component';
import { ShopComponent } from './features/shop/shop.component';
import { ProductDetailsComponent } from './features/shop/product-details/product-details.component';

export const routes: Routes = [
    // '' -> correspond to the route path of the app (ex: https://skinet.com/)
    { path: '', component: HomeComponent },

    // 'shop' -> https://example.com/shop
    { path: 'shop', component: ShopComponent },

    // 'shop/:id' -> https://example.com/shop/1
    { path: 'shop/:id', component: ProductDetailsComponent },

    // '**' matches any URL that hasn't been defined in the routes above
    // redirect to HomeComponent 
    { path: '**', redirectTo: '', pathMatch: 'full' },
];
