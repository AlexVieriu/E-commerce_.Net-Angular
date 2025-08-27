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