241. Introduction
-> API performance(caching) 
-> Angular Lazy loading


242. Setting up caching on the API

-- Core.Interfaces -> IResponseCacheService.cs --
Task CacheResponseAsync(string cacheKey, object response, TimeSpan timeToLive);
Task<string?> GetCachedResponseAsync(string cacheKey);
Task RemoveCacheByPattern(string pattern);


-- Infrastructure.Services -> ResponseCacheService.cs --
CacheResponseAsync
    -> add json option for CamelCase
    -> serialize the response
    -> await to set the cache

GetCachedResponseAsync
    -> await to get the cache
    -> return the cached response

RemoveCacheByPattern


-- Program.cs --
builder.Services.AddSingleton<IResponseCacheService, ResponseCacheService>();


243. Creating an attribute on the API

-- API.RequestHelpers -> CacheAttribute.cs --
-> derived from Attribute, IAsyncActionFilter
-> override OnActionExecutionAsync()
    -> called asynchronously before the action, after model biding is complete
    -> get cache service from DI container
    -> generate the key
    -> if we have a cached response with a key, return it
    -> otherwise, call the next filter in the pipeline (goes to the controller where the cache attribute is applied)
    -> if there is OkObjectResult, cache the response(cacheKey, object(ex: products), timeToLive)


Dictionary:

-- CacheAttribute.cs --

AttributeTargets.All
Pros: 
-> very flexible - you can put your attributes anywhere
-> helpful when experimenting or not sure yet where it will be needed

Cons:
-> to permissive - attributes might be applied on places 
they don't make sense(caching property or method) 
-> can cause confusion to other developers (cache to class and property)
-> harder to enforce correct usage - you might introduce 
bugs if the attribute does nothing in some contexts


IAsyncActionFilter:
-> a filter that asynchronously surrounds execution of the action, 
after the model biding is complete


244. Testing the caching

-- ProductController.cs --
-> put cache on GetProducts(), GetProductById(), GetBrands(), GetTypes(): [Cache(10)]

Test it
https://localhost:4200/
-> add products, sort products, search products
-> all this need to be in the second redis DB


245. Invalidating the cache

-> solution for when we add a new product but we can't see the new product in the redis DB

-- ResponseCacheService.cs --
-> implementing RemoveCacheByPattern()

-- API.RequestHelpers -> InvalidateCache.cs --
-> inherit from Attribute, IAsyncActionFilter
-> override OnActionExecutionAsync()
    -> await next();
    -> get cache service from DI container
    -> call RemoveCacheByPattern()

-- ProductController.cs --
-> put InvalidateCache on CreateProduct(), UpdateProduct(), DeleteProduct():
[InvalidateCache("api/products|")]

Test in Postman:
-> add a folder with 4 requests
{{localhost}}/api/products?pageSize=3&pageIndex=1
{{localhost}}/api/products?brands=Angular,React&types=Boots,Gloves
{{localhost}}/api/products?brands=Angular,React
{{localhost}}/api/products?search=red

-> go to folder press run (delete all in the redis DB to be clean)
-> run it again (should see a much smaller request time)

-> add a new product to see if they are deleted from redis DB
    -> login as admin 
    -> add a new product: {{localhost}}/api/products
    -> check the redis DB: we only need to see the types and the brands 


246. Angular Lazy loading

-- app.routes.ts --
-> remove the routes for checkout, orders, account and admin and replace it with
{ path: 'checkout', loadChildren: () => import('./features/checkout/routes').then(m => m.checkoutRoutes) },
{ path: 'orders', loadChildren: () => import('./features/orders/routes').then(m => m.orderRoutes) },
{ path: 'account', loadChildren: () => import('./features/account/routes').then(m => m.accountRoutes) },
{
    path: 'admin', loadComponent: () => import('./features/admin/admin.component')
        .then(c => c.AdminComponent), canActivate: [authGuard, adminGuard]
},


-- src>app>features>checkout> -- 
-- routes.ts --
export const checkoutRoutes: Route[] = [
    { path: '', component: CheckoutComponent, canActivate: [authGuard, emptycartGuard] },
    { path: 'success', component: CheckoutSuccessComponent, canActivate: [authGuard, orderCompleteGuard] },
];

-- src>app>features>orders> -- 
export const orderRoutes: Route[] = [
    { path: '', component: OrderComponent, canActivate: [authGuard] },
    { path: 'id', component: OrderDetailedComponent, canActivate: [authGuard] },
]

-- src>app>features>account> --
export const accountRoutes: Route[] = [
    {path: 'login', component: LoginComponent},
    {path: 'register', component: RegisterComponent},
]


Dictionary:
https://www.geeksforgeeks.org/angular-js/implementing-lazy-loading-in-angular/
https://paul-chesa.medium.com/mastering-lazy-loading-in-angular-a-step-by-step-guide-6a65385b4f17 


What is lazy loading?
-> loading modules only when they're needed, rather than at application startup

What are modules? 
-> are organized units that group related components, services, directives and other code together
-> they are like containers that help structure your app

Pros of Lazy loading?
-> reduce initial bundle size
-> improves performance
-> route-base loading: modules load when navigates to specific routes
-> automatic code splitting: Angular CLI automatically creates separate bundles

Cons of Lazy loading?
-> initial setup complexity
-> runtime loading delay: users feels a bit of delay when loading for the first time the module
-> debugging complexity
-> memory management issues: poorly implemented lazy loading can lead to memory leaks 
if modules aren't properly cleaned up when no longer needed
-> testing complications: unit and integration testing become more complex when dealing with 
dynamically loaded modules, requiring additional setup and mocking strategies for proper test coverage

When you use Lazy loading?
-> Images and media: Load images only when they're about to enter the viewport, reducing initial page load time
-> JavaScript modules: Import code modules only when needed rather than loading everything upfront
-> Content sections: Load additional content as users scroll (infinite scroll)
-> Route-based components: Load page components only when navigating to specific routes

When to use loadChildren vs loadComponent in app.routes.ts?
loadChildren: 
-> use when you want to lazy load an entire feature module with its own routing configuration    
{ path: 'orders', loadChildren: () => import('./features/orders/routes').then(m => m.orderRoutes) },

-> we need to create a separate route.ts config file
export const orderRoutes: Route[] = [
    { path: '', component: OrderComponent, canActivate: [authGuard] },
    { path: 'id', component: OrderDetailedComponent, canActivate: [authGuard] },
]

loadComponent:
-> use when you want to lazy load a standalone component
-> we don't need to create a separate routes.ts config file
{
  path: 'products',
  loadComponent: () => import('./about/about.component').then(c => c.AboutComponent)
}