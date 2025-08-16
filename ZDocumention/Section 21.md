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