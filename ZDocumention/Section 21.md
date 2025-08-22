225. Introduction
-> adding roles to the app
-> role based authorization
-> adding a refund feature (we are not going to include a full on inventory system, but we will set up if you want to do that kind of thing)

226. Adding roles to the app

-- Program.cs -- 
builder.Services.AddIdentityApiEndpoints<AppUser>()
                .AddRoles<IdentityRole>()
                ....

try{
    ...
    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    ...
}

-- StoreContext.cs --
-> add UserManager<AppUser> to constructor
-> if UserName "admin@test.com" doesn't exist, add it and assign it to role "Admin"


-- Infrastructure -> Config -> RoleConfiguration.cs --
-> using IEntityTypeConfiguration<IdentityRole>, add admin and customer roles

-- Terminal --
cd skinet 

-- Migrations for Development --
dotnet ef migrations remove --context SqliteStoreContext -s API -p Infrastructure --force
dotnet ef migrations add AddRoles_Sqlite --context SqliteStoreContext -s API -p Infrastructure -o Migrations/SQLite

Check migrations on development:
cd skinet/api
dotnet ef migrations list

Update Database with the new migrations:
dotnet watch  


-- Migrations for Production --
dotnet ef migrations remove --context SqlServerStoreContext -s API -p Infrastructure --force
dotnet ef migrations add AddRoles_SqlServer --context SqlServerStoreContext -s API -p Infrastructure -o Migrations/SqlServer

Update Database with the new migrations:
-> go "F:\Programare\E-commerce_.Net-Angular\skinet\API\bin\Release\net10.0" 
-> run the API.exe


Test/See and see the tables in Development and Production


227. Using the roles

-- AccountController.cs --
public async Task<ActionResult> GetUserInfo(){    
    Roles = User.FindFirstValue(ClaimTypes.Role)    
    ...
}

-- BuggyController.cs --
-> add the method GetAdminSecret()
    -> get the name, id, isAdmin and roles from the User obj= ClaimsPrincipal

-- ProductsController.cs --
-> add the [Authorize(Roles = "Admin")] to: 
    GetProducts(), GetProductById(), CreateProduct(), UpdateProduct(), DeleteProduct()

Test with Postman
-> login as admin
-> get admin secrets (Return code 200 - Ok)

-> login as Tom
-> get admin secrets (Return code 403 - Forbidden)


228. Using roles

-- Core -> Specifications --  

-- PagingParams.cs --
private const int MaxPageSize = 50;
public int PageIndex { get; set; } = 1;
public int _pageSize { get; set; } = 6;
public int PageSize
{
    get => _pageSize;
    set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
}


-- ProductsSpecParams.cs --
-> remove the code with pagination
-> derive from PagingParams.cs


-- OrderSpecParams.cs --
-> derive from PagingParams.cs
public string? Status { get; set; }


-- OrderSpecification.cs --
Add:
-> public OrderSpecification(int id)
-> public OrderSpecification(OrderSpecParams specParams)
-> private static OrderStatus? ParseStatus(string status) 

-- AdminController.cs --
-> add the method GetOrders()


Test in postman:
-> dotnet watch
-> "login as admin"
-> "get admin secrets"
-> "get orders"


229. Updating the Base controller CreatePageResult method

-- Core.Interfaces -> IDtoConvertible.cs --


-- Core.Entities.OrderAggregate -> Order.cs --
-> add to inherit from IDtoConvertible


-- API.Controllers -> BaseApiController.cs --
-> add a new method overload 
    
Task<ActionResult> CreatePagedResult<T, TDto>{
    ...
    var pagination = new Pagination<TDto>(pageIndex, pageSize, count, dtoItems);
}


-- AdminController.cs --
-> add the "x => x.ToDto()" to the CreatePagedResult

-> create a new endpoint GetOrderById(int id){ ... }

Test in Postman:
-> Get order by Id : "{{localhost}}/api/admin/orders/1"


230. Adding refund functionality

-- IPaymentService.cs --
Task<string> RefundPayment(string paymentIntentId);

-- PaymentService.cs --
-> implement the RefundPayment method
    -> create refund options
    -> create new service
    -> get the refund (if fails, it will throw an exception that we will manage on the middleware)
    -> return refund status 

-- AdminController.cs --
-> create a new endpoint RefundOrder
    -> get the specification with the order id
    -> get the order
    -> check if order is : null or Pending
    -> check the refund status: access the RefundPayment method from the PaymentService
    -> update order status
    -> w8 for the database to update
    -> return the orderDto

-- OrderStatus.cs -- (enum) 
-> add Refunded property


231. Creating the admin components 

cd skinet/client
ng g s core/services/admin --skip-tests
ng g c features/admin/admin --skip-tests --flat

-- app.routes.ts --
{ path: 'admin', component: AdminComponent, canActivate: [authGuard] }

 ng g g core/guards/admin --skip-tests


-- header.component.html --
<a routerLink="/admin" routerLinkActive="active">Admin</a>


-- error.interceptor.ts --
-> add 403 error


-- user.ts --
roles: string | string[];  // for single and multiple roles


-- account.service.ts --
return Array.isArray(roles) ? roles.includes('Admin') : roles === 'Admin';


232. Creating an angular directive

cd skinet/client
ng g --help 
ng g d shared/directives/is-admin --dry-run
ng g d shared/directives/is-admin --skip-tests

-- is-admin.directive.ts --
-> inject the AccountService, ViewContainerRef and TemplateRef
-> add effect() signal to the constructor
    -> if isAdmin, createEmbeddedView
    -> else clear

-- header.component.html --
-> add *appIsAdmin to Admin link


Dictionary:
What is effect()?
-> register an "effect" that will be scheduled & executed whenever the signals that is reads changes

https://next.angular.dev/api/core/effect

Types of effects:
-> component effects
    -> created when effect() is called from a component, directive, 
    or within a service of a component/directive
    -> the effect dies when the component is destroyed

-> root effects
    -> created when effect() is called from outside the component tree, such as in a root service
    -> the effect runs for the entire application lifetime

https://claude.ai/chat/c0e4185c-560e-40ff-a53f-d048f2bef4f5


233. Creating an admin guard

-- admin.guard.ts --
-> inject AccountService, Router, SnackbarService
-> if isAdmin, return true
-> else snackbar error, router.navigateByUrl('/shop'), return false

-- app.routes.ts --
-> add admin guard to the admin route


234. Adding the admin service methods

-- client -> src -> shared -> models -> order --
-- orderParams.ts --
-> pageNumber, pageSize, filter


-- admin.service.ts --
baseUrl = environment.apiUrl;
private http = inject(HttpClient);

-> getOrders( orderParams: OrderParams )
-> getOrder( id: number )
-> refundOrder( id: number )


Dictionary:
1. What is the purpose of export keyword when declaring a class, type, or interface?
-> to export a class, type, or interface so that it can be used in other files, 
otherwise it is only visible inside the current file 

2. What is the difference between let and var in .ts? 
-> let: block-scoped
-> var: function-scoped


Example:
function example() {
  if (true) {
    var x = 1;
    let y = 2;
  }
  console.log("x value:", x); // This will work
  
  try {
    console.log("y value:", y); // This will throw an error
  } catch (error) {
    console.log("Error accessing y:", error.message);
  }
}

example();