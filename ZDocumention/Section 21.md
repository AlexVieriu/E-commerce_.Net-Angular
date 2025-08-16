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