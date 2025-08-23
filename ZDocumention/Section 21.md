225. Introduction
-> adding roles to the app
-> role based authorization
-> adding a refund feature (we are not going to include a full-on inventory system, 
but we will set up if you want to do that kind of thing)


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

-> we put the UserManager in the middleware because we need it when we seed data


-- StoreContext.cs --
-> ApplyDatabaseSpecificConfigurations(modelBuilder) 
    -> we added for database specific configuration for Sqlite and SqlServer
 
Examples:
SqlServer:
modelBuilder.Entity<Product>()
            .Property(x => x.Price)
            .HasColumnType("decimal(18,2)");

SQLite:
modelBuilder.Entity<Product>()
            .Property(x => x.Price)
            .HasColumnType("REAL");


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


Dictionary:
modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderConfiguration).Assembly);

1. .ApplyConfigurationsFromAssembly
-> applies configuration from all IEntityTypeConfiguration instances that are defined in provided assembly

For more examples: https://aka.ms/efcore-docs-modeling


2. What is trimming?
-> is a .NET deployment optimization that removes unused code from your 
application to make it smaller
-> when you publish with trimming enabled, the compiler analyzes your code 
and removes any types, methods, or assemblies that appear to be unused

High risk trimming with solution for our app:
https://claude.ai/chat/d836deec-11d5-418b-882a-e9bb895f1c0f


227. Using the roles

-- AccountController.cs --
public async Task<ActionResult> GetUserInfo(){
    Roles = User.FindFirstValue(ClaimTypes.Role) // Added
    ...
}


-- BuggyController.cs --
-> add the method GetAdminSecret()
    -> get the name, id, isAdmin and roles from the ControllerBase.User (type ClaimsPrincipal)
    -> return Ok(new { name, id, isAdmin, roles });


-- ProductsController.cs --
-> add the [Authorize(Roles = "Admin")] to:
CreateProduct()
UpdateProduct() 
DeleteProduct()


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


Dictionary:
What is CreatePagedResult?
-> creates a paginated API response
-> returns a subset of data with pagination metadata


230. Adding refund functionality

-- IPaymentService.cs --
Task<string> RefundPayment(string paymentIntentId);

-- PaymentService.cs --
-> implement the RefundPayment method
    -> create refund options
    -> create new service
    -> get the refund 
        -> if fails, Stripe SDK will throw an exception, and my ExceptionMiddleware.cs will catch it
    -> return refund status


-- AdminController.cs --
-> create a new endpoint RefundOrder
    -> get the specification with the order id
    -> get the order
    -> check if order is : null or Pending
    -> check the refund status: access the RefundPayment method from the PaymentService
    -> update order status
    -> wait for the database to update
    -> return the orderDto


-- OrderStatus.cs -- (enum)
-> add Refunded property


The Flow When RefundPayment Fails
1. Client calls your API → RefundPayment("pi_1234567890abcdef")
-> your method receives a valid-looking payment intent ID string

2. Your method creates refund options and service:
var refundOptions = new RefundCreateOptions { PaymentIntent = "pi_1234567890abcdef" };
var refundService = new RefundService();

3. Stripe SDK makes API call → refundService.CreateAsync(refundOptions)
-> Stripe validates the payment intent ID against their database
-> Stripe discovers the payment intent doesn't exist, or is already refunded, or has some other issue

4. Stripe SDK throws exception → StripeException: No such payment_intent: pi_1234567890abcdef
OR StripeException: Charge pi_1234567890abcdef has already been refunded
OR StripeException: This payment_intent cannot be refunded

5. Exception bubbles up through your controller to the middleware

6. ExceptionMiddleware catches it in the catch (Exception ex) block

7. Middleware returns HTTP 500 with JSON error response:
// Example responses based on different Stripe API errors
{
  "statusCode": 500,
  "message": "No such payment_intent: pi_1234567890abcdef",
  "details": "Internal Server Error"
}

// OR
{
  "statusCode": 500, 
  "message": "Charge pi_1234567890abcdef has already been refunded",
  "details": "Internal Server Error"
}



231. Creating the admin components

cd skinet/client
ng g s core/services/admin --skip-tests
ng g c features/admin/admin --skip-tests --flat
ng g g core/guards/admin --skip-tests

-- app.routes.ts --
{ path: 'admin', component: AdminComponent, canActivate: [authGuard, adminGuard] }


-- header.component.html --
<a routerLink="/admin" routerLinkActive="active">Admin</a>

-- error.interceptor.ts --
-> add 403 error (Forbidden)


-- user.ts --
roles: string | string[]; // for single and multiple roles


-- account.service.ts --
return Array.isArray(roles) ? roles.includes('Admin') : roles === 'Admin';
-> is roles an array?
    -> if yes, check if 'Admin' is in the array and return true/false
    -> else check if roles is 'Admin' and return true/false


232. Creating an angular directive

cd skinet/client
ng g --help
ng g d shared/directives/is-admin --dry-run
ng g d shared/directives/is-admin --skip-tests

-- is-admin.directive.ts --
-> inject the AccountService, ViewContainerRef and TemplateRef
-> add effect() method to constructor
    -> if isAdmin, createEmbeddedView
    -> else clear

-- header.component.html --
-> add *appIsAdmin to Admin link


Dictionary:
What is effect()?
-> registers an "effect" that will be scheduled & executed whenever the signals that it reads change

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


Dictionary:
.createEmbeddedView()
-> creates and inserts a view (DOM elements) into the ViewContainerRef on the provided TemplateRef

Template Processing: When Angular sees *appIsAdmin, it transforms your HTML:
<!-- From this: -->
<a *appIsAdmin routerLink="/admin" routerLinkActive="active">Admin</a>

<!-- To this (conceptually): -->
<ng-template appIsAdmin>
  <a routerLink="/admin" routerLinkActive="active">Admin</a>
</ng-template>

https://angular.dev/guide/directives/structural-directives


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
1. What is the purpose of the export keyword when declaring a class, type, or interface?
-> to export a class, type, or interface so that it can be used in other files;
otherwise, it is only visible inside the current file.

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


235. Adding the admin component code

https://material.angular.dev/components/table/overview#pagination

-- admin.component.ts --
Properties:
displayedColumn
statusOptions
dataSource

private adminService
private dialogService
orderParams
totalItems

Methods:
ngOnInit()
loadOrders()
onPageChanged(event: any)
onFilterSelect(event: any)


236. Designing the admin component template

-- admin.component.ts --
imports: [
    CurrencyPipe,
    DatePipe,
    MatButton,
    MatIcon,
    MatLabel,
    MatPaginatorModule,
    MatSelectModule,
    MatTableModule,
    MatTabsModule,
    MatTooltipModule
  ]

-- admin.component.html --


237. Populating the orders table
-- admin.component.html --
refactoring + editing 


238. Adding the order table action button functionality

-- order-detailed.component.ts --
loadOrder()
-> load order data based on isAdmin flag

onReturnClick()
-> return to admin or orders page


-- order-detailed.component.html --
<button (click)="onReturnClick()" mat-stroked-button>{{buttonText}}</button>


-- admin.component.ts --
refundOrder(id:number){}


-- admin.component.html --
-> add click event to refund button


239. Adding a confirmation prompt

ng g c shared/components/confirmation-dialog --skip-tests
ng g s core/services/dialog --skip-tests


-- confirmation-dialog.component.ts --
dialogRef = inject(MatDialogRef<ConfirmationDialogComponent>);
data = inject(MAT_DIALOG_DATA)

onConfirm() {
    this.dialogRef.close(true);
}

onCancel() {
    this.dialogRef.close(false);
}


-- confirmation-dialog.component.html --
data.title
data.message
2 buttons:
    -> Confirm
    -> Cancel


-- dialog.service.ts --
private dialog = inject(MatDialog);

confirm(title: string, message: string) {

// returns an observable
const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
    data: { title, message },
    width: '400px'
    }).afterClosed();

    return firstValueFrom(dialogRef); // returns true or nothing
}


-- admin.component.ts --
async openConfirmDialog(id: number)


-- admin.component.html --
-> replace refundOrder() with openConfirmDialog()