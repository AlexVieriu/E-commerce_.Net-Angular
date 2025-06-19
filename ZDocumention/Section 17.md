183. Introduction
-> Adding the Order Entity
-> Aggregate entities
-> Owned entities
-> Unit of Work pattern


184. Creating the order Aggregate part 1

-- Core -> Entities -> OrderAggregate --
-- ShippingAddress.cs --
Name, Line1, Line2, City, State, PostalCode, Country

-- ProductItemOrdered.cs -- 
ProductId, ProductName, PictureUrl

-- OrderStatus.cs (enum)--
Pending, PaymentReceived, PaymentFailed

-- OrderItem.cs --
ItemOrdered, Price, Quantity

-- PaymentSummary.cs --
Last4, Brand, ExpMonth, ExpYear


185. Creating the order Aggregate part 2

-- Core -> Entities -> OrderAggregate --
-- Order.cs --
OrderDate, BuyerEmail, ShippingAddress, DeliveryMethod, 
PaymentSummary, OrderItems, Subtotal, Status, PaymentIntentId


186. Configuring the order entities

-- Infrastructure -> Config -> OrderConfiguration.cs --
public class OrderConfiguration : IEntityTypeConfiguration<Order {. . .}

-- Infrastructure -> Config -> OrderItemConfiguration.cs --
public void Configure(EntityTypeBuilder<OrderItem> builder){. . .}

-- StoreContext.cs --
public DbSet<Order> Orders { get; set; }
public DbSet<OrderItem> OrderItems { get; set; }

cd skinet
dotnet ef migrations add OrderAggregateAdded -s API -p Infrastructure


Dictionary:

IEntityTypeConfiguration<T>
-> allows configuration for an entity type to be factored into a separate class

EntityTypeBuilder
-> provides a simple API for configuring an <see cref="IMutableEntityType" />

builder.OwnsOne(o => o.ShippingAddress, a => a.WithOwner());
builder.OwnsOne(o => o.PaymentSummary, a => a.WithOwner());

.OwnsOne(...)
-> ShippingAddress, PaymentSummary are owned entities
-> stored in the same tabled as Order.cs
-> mapped as columns as with prefix
-> doesn't have there own key
-> WithOwner()
    -> establishes the relationship back to the owner

builder.HasMany(x => x.OrderItems).WithOne().OnDelete(DeleteBehavior.Cascade);

.HasMany(...)
-> establishes that an Order.cs has many OrderItem entities

.WithOne() 
-> specifies that each OrderItem belongs to one Order


187. Introducing the unit of work

-> creates repository instances as needed
-> EF Tracks the entities state(add, update, remove)
-> at the end of the transaction UoW.Complete()
-> Dispose the DbContext
-> uses the same lifetime as repository(scoped)


188. Implementing the unit of work

-- Core -> Interfaces -> IUnitOfWork.cs --
IGenericRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity;
Task<bool> Complete();

-- Infrastructure -> Data -> UnitOfWork.cs --
-> add StoreContext to constructor
-> Complete() => SaveChangesAsync()
-> Dispose() => DbContext.Dispose()
-> Repository<TEntity>()
    -> get the nameType of TEntity
    -> get the Type of the repository (at runtime: MakeGenericType(...))
    -> return a new instance of the repository (Activator.CreateInstance(...))  

-- Program.cs --
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();


Dictionary:

ConcurrentDictionary<TKey, TValue>
-> represents a thread-safe collection of keys and values

virtual Type MakeGenericType()(params Type[] typeArguments)
-> at runtime
-> substitutes the elements of an array of types for the type parameters of the current 
generic type definition and returns a Type object representing the resulting constructed type

Activator
-> contains methods to create types of objects locally or remotely, or 
obtain references to existing remote objects 
-> this class cannot be inherited

.CreateInstance(Type type, params object?[]? args)
-> creates an instance of the specified type using the constructor that best matches 
the specified parameters


189. Using the unit of work

-- IGenericRepository.cs --
remove SaveAllAsync();

-- IGenericRepository.cs --
remove SaveAllAsync();

-- ProductsController.cs --
-> add IUnitOfWork to constructor
-> replace SaveAllAsync() with unitOfWork.Complete()
-> replace productRepo with unitOfWork.Repository<Product>()

-- PaymentsController.cs --
-> add IUnitOfWork to constructor
-> replace dmRepo with unitOfWork.Repository<DeliveryMethod>()

-- PaymentService.cs --
-> add IUnitOfWork to constructor
-> replace productRepo with unitOfWork.Repository<Product>()
-> replace dmRepo with unitOfWork.Repository<DeliveryMethod>()

Test Add, Get, GetAll, Update, Delete product in Postman.


190. Creating the order controller

-- API -> DTOs -> OrderDto.cs --
CartId, DeliveryMethodId, ShippingAddress, PaymentSummary

-- API -> Controllers -> OrderController.cs --

-> get User.Email (where User = HttpContext?.User)
-> get the cart using CartService
-> check cart == null
-> check cart.PaymentIntentId == null
-> create a new OrderItem List
-> get every cart items from the cart with a foreach loop
    -> create new ProductItemOrdered and OrderItem
    -> OrderItem = ProductItemOrdered + Price + Quantity
-> get deliveryMethod  
-> create new Order with all o the above
-> .Add(order) to the repository (to DB)
-> return order
-> else throw BadRequest


Dictionary:

HttpContext:
-> HttpContext is created very early in the request processing pipeline, 
before the request reaches your controller.


191. Debugging the order creation

-> breakpoint at OrderController.cs

Test Order creation in Postman:
docker-compose up -d
-> login as tom (section 14)
-> create a new Folder: Section 17 - Orders
    -> Update Cart  : {{localhost}}/api/cart
    -> Create payment intent: {{localhost}}/api/payment
    -> Create order: {{localhost}}/api/order


192. Adding the get order methods

-- Core -> Specifications -> OrderSpecification.cs --
public OrderSpecification(string email) : base(o => o.BuyerEmail == email) {...}
public OrderSpecification(string email, int id) : base(o => o.BuyerEmail == email && o.Id == id)

-- API -> Controllers -> OrderController.cs --

[HttpPost]
Task<ActionResult<Order>> CreateOrder(CreateOrderDto orderDto){...}

[HttpGet]
Task<ActionResult<IReadOnlyList<Order>>> GetOrdersForUser(){...}

[HttpGet("{id:int}")]
Task<ActionResult<Order>> GetOrderById(int id){...}

Test in Postman:
Getting Orders from User: {{localhost}}/api/orders
Getting Order by Id     : {{localhost}}/api/order/1

-> DeliveryMethod is returning null
-> DeliveryMethod is a related prop, and EF will not return a related prop

Solution:
-> use projection
-> use eager loading


193. Updating the spec for eager loading

-- ISpecification.cs --
List<Expression<Func<T, object>>> Includes { get; }
List<string> IncludeStrings { get; } 

-- Specifications.cs --
public List<Expression<Func<T, object>>> Includes { get; } = [];
public List<string> IncludeStrings { get; } = [];

protected void AddInclude(Expression<Func<T, object>> includeExpression)
    => Includes.Add(includeExpression);

protected void AddInclude(string includeString)
    => IncludeStrings.Add(includeString);

-- SpecificationEvaluator.cs --
query = spec.Includes.Aggregate(query, (current, include) => current.Include(include));
query = spec.IncludeStrings.Aggregate(query, (current, include) => current.Include(include));


Dictionary:

IQueryable<T>:
-> represents a query that hasn't been executed yet

ISpecification<T>:
-> contains lists of what related data to include

.Aggregate()
-> chains multiple operations together (in this case, multiple Include() calls)

.Include()
-> tells Entity Framework to load related entities along with the main entity


!! Why use Expression? !!
-- ISpecification.cs --
-> so it can work directly with the EF and DB

1. Func<Tin, Tout> 
-> is a delegate that represents a compiled function that takes a 
<Tin> object and returns a <Tout> value
-> it's essentially a reference to a method that can be executed directly in memory

2. Expression<Func<Tin, Tout>> 
-> is an expression tree representation of a function
-> instead of being executable code, it's a data structure that represents the code in a 
form that can be analyzed, modified, or translated to something else (like SQL).

Examples:
// Example with Expression<Func<Customer, bool>>
Expression<Func<Customer, bool>> adultExpr = customer => customer.Age > 18;

// This works with Entity Framework - translates to SQL
var adultsFromDb = dbContext.Customers.Where(adultExpr).ToList();

// You can also compile the expression to get a Func
Func<Customer, bool> adultFunc = adultExpr.Compile();

// Example with Func<Customer, bool>
Func<Customer, bool> isAdult = customer => customer.Age > 18;

// This works with in-memory collections
var inMemoryCustomers = new List<Customer>();
var adultsInMemory = inMemoryCustomers.Where(isAdult).ToList();

// This WON'T translate to SQL - it forces loading all customers into memory first
// and then filters in memory (inefficient)
// var badApproach = dbContext.Customers.Where(isAdult).ToList(); // Not recommended


194. Updating the controller to eagerly load in the get methods

-- OrderSpecification.cs --
{
    AddInclude(x => x.OrderItems);  
    AddInclude(x => x.DeliveryMethod);  
    AddOrderByDescending(x => x.OrderDate); 
}
{
    AddInclude("OrderItems");
    AddInclude("DeliveryMethods");
}

Test with debugger to see what is going on
-> put breakpoint at :
    -> OrderSpecification.cs
    -> OrderController.cs
        -> GetOrdersForUser()

Steps:
-> get spec: OrderSpecification(User.GetEmail())
    -> ClaimsPrincipal User => HttpContext?.User!;
    -> ClaimTypes.Email
    -> OrderSpecification.cs
    -> OrderSpecification(string email)
        -> pass the email to base class: BaseSpecification<Order>
        -> BaseSpecification<Order>.cs
            -> AddInclude(x => x.OrderItems);  
                -> Includes.Add(includeExpression);
            ... and so on  
            
-> get orders: 
    -> get the type of the class: GenericRepository
    -> GetAllAsync(ISpecification<T> spec) 
        -> ApplySpecification(spec).ToListAsync();   
            -> SpecificationEvaluator<T>.GetQuery(context.Set<T>().AsQueryable(), spec);
            -> SpecificationEvaluator.cs
                -> Criteria, OrderBy, OrderByDescending, Skip, Take
                -> query = Aggregate all Queries
                -> return query


.ToListAsync(): 
-> here we execute the IQueryable to get the data from DB
-> From EntityFrameworkQueryableExtensions.cs

public static async Task<List<TSource>> ToListAsync<TSource>(this IQueryable<TSource> source,
    CancellationToken cancellationToken = default)
{
    var list = new List<TSource>();
    await foreach (var element in source.AsAsyncEnumerable().WithCancellation(cancellationToken).ConfigureAwait(false))          
        list.Add(element);    
    
    return list;
}


195. Shaping the data to return

OrderDto.cs
OrderItemDto.cs
OrderMappingExtensions.cs
    -> static OrderDto ToDto(this Order order)
    -> static OrderItemDto ToDto(this OrderItem orderItem)

-- OrderController.cs --
GetOrdersForUser()
    -> var ordersToReturn = orders.Select(x => x.ToDto()).ToList();
    -> return Ok(ordersToReturn);

-- Order.cs --
GetTotal() => Subtotal + DeliveryMethod.Price;

-- OrderDto.cs --
decimal Total { get; set; }

-- OrderMappingExtensions.cs --
 Total = order.GetTotal()


 196. Summary
 -> adding the Order Entity
 -> Aggregate entities
 -> Owned Entities
 -> Unit of Work pattern