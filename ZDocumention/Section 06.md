6.50. Introduction
-> error handling and exception
-> validation error (at the middleware level)
-> Http response errors
-> middleware
-> CORS (cross origin resource sharing)

HttpResponse(most used):
200 -> Ok
201 -> Created
204 -> No Content
400 -> Bad Request
401 -> Unauthorized
403 -> Forbidden
404 -> Not Found
500 -> Internal Server Error
502 -> Bad Gateway
503 -> Service Unavailable

100-199: Informational Responses
200-299: Successful Responses
300-399: Redirection Messages
400-499: Client Error Responses
500-599: Server Error Responses


6.52. Adding a test controller for error handling

Why we need a BuggyController?

1. Test error handling systematically
-> you can verify how your app handles different errors scenarios without having to trigger
actual errors in your core business logic 

2. Test middleware and filters
-> helps test global exception handling, authentication, logging and other cross-cutting concerns

3. Test client-side error handling
-> Frontend developers can use these endpoints to ensure their client code properly 
handles different error scenarios

4. Documentation and demonstration
-> the controller demonstrates the app's error response format from API consumer

This is especially useful during development, testing, and integration phases. 
In production, you might want to disable or secure these endpoints.

-- API -> Controllers -> BuggyController.cs --
-> add 4 HttpGet methods
    -> GetUnauthorized
    -> GetBadRequest
    -> GetNotFound
    -> GetInternalServerError

-> add HttpPost method
    -> GetValidationError

Postman Collection links for all 5 methods:
-> {{localhost}}/api/buggy/[action name]



6.52. Exception handling middleware

https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-9.0

-- API -> Errors -> ApiErrorResponse.cs --
int StatusCode
string Message
string? Details

-- API -> Middleware -> ExceptionMiddleware.cs --
public async Task InvokeAsync(HttpContext context)
private static Task HandleExceptionAsync(HttpContext context, Exception ex, IHostEnvironment env)

Dictionary:
RequestDelegate : a task that represents the compilation of request processing
IHostEnvironment: provides information about the hosting environment an application is running in

Testing with Postman(Internal Error)


6.53. Validation error Responses

-- Product.cs --

-> because of the "required" attribute, we can't create a JSON file 
when we handle the POST("validationerror") method

Why we used required attribute in the first place?

What we want to do at this chapter?
-> we don't want to create e product from our entity class
-> we want to create a different type of object that we can apply validation to, at the API level

-- API -> DTOs -> CreateProductDTO.cs --
-> use [Required], [Range()] attributes instead of "request"

-- API -> Controllers -> BuggyController.cs --
-> use CreateProductDTO

Postman:
-> test validationerror 

Dictionary:
[ApiController] -> handles models state errors from us


6.54. Adding CORS support on the API

CORS(Cross Origin Resource Sharing)

ChatGPT: 
-> give me a complex implementation of CORS in .net 9. 
Let's say i'm working at a top payment company, what is the implementation?

-- API -> Program.cs --
builder.Services.AddCors();

app.UseCors(options => options.AllowAnyHeader()
                              .AllowAnyMethod()
                              .AllowCredentials()
                              .WithOrigins("http://localhost:4200", "https://localhost:4200"));

Postman:
-> add header: Origin: http://localhost:4200                              
-> add test Script:
pm.test("CORS header is present", function(){
    pm.response.to.have.header("Access-Control-Allow-Origin");
    pm.expect(pm.response.headers.get("Access-Control-Allow-Origin")).to.eql
    ("https://localhost:4200");
});
