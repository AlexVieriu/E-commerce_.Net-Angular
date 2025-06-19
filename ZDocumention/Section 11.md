11.92. Introduction
-> error handling in Angular 
-> Http interceptors (alternative to Try/Catch)
-> adding toast notifications 
-> adding loading indicators

Goal: 
-> handle errors we receive from the API centrally and handled by Http interceptors
-> understand how to troubleshoot API errors


11.93. Creating a test error component
ng g c features/test-error --skip-tests

-- app.routes.ts --
{ path: 'test-error', component: TestErrorComponent }

-- header.component.html --
<a routerLink="/test-error" routerLinkActive="active">Error</a>

-- tets-error.component.ts --
-> in production we need to make Services for each kind of errors(404, 200, 500, etc)   
-> in our case we use methods

-- tets-error.component.html --

Dictionary:
justify-center(html): aligns it's children horizontally in the center of the container


11.94. NotFound and Server Error component
ng g c shared/components/not-found --skip-tests
ng g c shared/components/server-error --skip-tests

-- server-error.component.html --
-> create a h1 with msg: Internal Server Error

-- not-found.component.html --
-> create a h1 with msg: Not Found 

-- app.routes.ts --
-> configure the routes for not-found and server-error components


11.95. Creating an HTTP Interceptor for handling API errors
ng g --help
ng g interceptor core/interceptors/error --dry-run
ng g interceptor core/interceptors/error --skip-tests

-- error.interceptor.ts --
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 400) {
        alert(error.error.title || error.error)
      }
      // ... 401, 403, 404, 500
      return throwError(() => error);
    })
  );
};

-> errorInterceptor this is a function not a class

-- app.config.ts -- 
provideHttpClient(withInterceptors([errorInterceptor]))

Dictionary:
Angular interceptor:
-> is a service that implements HttpInterceptorFn act as a middleware 
int the Http pipeline, providing a centralized way to perform tasks like logging, 
authentication, or adding headers to requests
-> modify Http requests before they are sent to the server 
-> handle or modify Http responses before they are returned to the caller
-> handle cross-cutting concerns like logging, token injection, error handling, caching;

Pipeline Flow:
-> HTTP Request → Interceptor 1 → Interceptor 2 → ... → Server
-> Server Response → Interceptor 2 → Interceptor 1 → HTTP Response

HttpInterceptorFn:
-> interface introduced in Angular 16
-> allows defining HTTP interceptors as standalone functions instead of classes
-> modify outgoing HTTP requests(headers, tokens)
-> handle incoming HTTP responses(process data, handle errors)
-> perform cross-cutting concerns like logging, caching, or authentication in the 
Http request-response pipeline

const router = inject(Router);
inject():
    -> obtain an instance of the Router service
    -> allows interceptor to access the router to navigate to other routes

return next(req).pipe()
next(req):
    -> part of the HttpInterceptorFn  (@angular/common/http)
    -> pass the intercepted Http request(req) to the next handler 
    in the HTTP pipeline

.pipe():
    -> provided by RxJS to compose and chain multiple functions to 
    process data emitted by an observable
    -> transform/handle the observable stream
    -> observable.pipe(operator1, operator2, ...)    

catchError:
    -> part of RxJS(Reactive Extensions for JavaScript)
    -> intercepts errors in the Http response and provides a way to handle them     

(error: HttpErrorResponse) => { ... }
    -> a callback function to handle the intercepted error

throwError:
    -> part of RxJS
    -> function to create an observable that emits an error.

Core concept of RxJS:
1. Observable: -> a data producer that emits values over time 
               -> ex: an HTTP request, user input, real-time data
2. Observer: -> a data consumer that receives values from an Observable
3. Operator: -> a function that transforms an Observables(filtering, mapping, combining)
4. Subscription: -> a mechanism to start listening to an Observable and clean up 
                resources when done 
5. Subject: -> a special type of Observable that can multicast values to multiple Observers      


11.96. Adding toast (snackbar) notifications

https://material.angular.io/components/snack-bar/overview

-> creating our own custom service from snack-bar service
-> display a notification to ours users when something has gone wrong

ng g s core/services/snackbar --skip-tests

-- snackbar.service.ts --
-> inject the MatSnackBar service
-> add and config 2 methods inside MatSnackBar class: success and error

-- styles.scss --
-> .mat-mdc-snack-bar-container.snack-error {...}
-> .mat-mdc-snack-bar-container.snack-success {...}

-- error.interceptor.ts --
-> change alerts with snackbar


Dictionary:
MatSnackBar properties:
-> handsetCssClass: 'mat-mdc-snack-bar-handset'
-> simpleSnackBarComponent: SimpleSnackBar
-> snackBarContainerComponent: MatSnackBarContainer

MatSnackBar Methods:
-> dismiss
-> open:
  -> Parameters:
    -> message: string
    -> action: string
    -> config? : MatSnackBarConfig      
      -> data (optional)
      -> direction (optional)
      -> horizontalPosition (optional)
      -> politeness (optional)
      -> verticalPosition (optional)
      -> viewContainerRef (optional)


97. Handling validation errors from the API

-- error.interceptor.ts --
-> we want to catch validation errors and display them to the user

if (err.status === 400) {
  if (err.error.errors) {
    const modelStateErrors = [];
    for (const key in err.error.errors) {
      if (err.error.errors[key]) {
        modelStateErrors.push(err.error.errors[key])
      }
    }
    throw modelStateErrors.flat(); // flat(): have a single array of strings
  } else {
    snackbar.error(err.error.title || err.error);
  }
}

-- test-error.component.ts --
validationErrors?: string[];

 get400ValidationError(){
  ...
   error: error => this.validationErrors = error
 }

-- test-error.component.html --
@if(validationErrors) {
<div class="mx-auto max-w-lg mt-5 bg-red-100">
    <ul class="space-y-2 p-2">
        @for (error of validationErrors; track $index){
        <li class="text-red-500">{{error}}</li>>
        }
    </ul>
</div>
}


98. Configuring the server error page

-- error.interceptor.ts --
if (err.status === 500) {
  const navigationExtras: NavigationExtras = { state: { error: err.error } }
  router.navigateByUrl('/server-error', navigationExtras);
}

-- server-error.component.ts --
{
  error?: any;

  constructor(private router: Router) {
    const navigation = this.router.getCurrentNavigation();
    this.error = navigation?.extras.state?.['error'];
  }
}

-- server-error.component.html --
-> check @if(error)
-> display {{error.message}}
-> display {{error.details}}


How to get the errors?
-> we take the error from the --error.interceptor.ts--, from HttpErrorResponse
and we pass is to the NavigationExtras
-> router now know about the error, and we can get it in the component 
--server-error.component.ts-- by the prop:

error?: any;
this.error = this.router.getCurrentNavigation()?.extras.state?.['error']; 


Dictionary:

NavigationExtras(interface)
  -> it's a TypeScript interface provided by Angular's Router model
  -> options that modify the 'Router' navigation strategy
  -> supply an object containing any of these properties to a 'Router'
  navigation function to control how the target URL should be constructed

const navigationExtras: NavigationExtras = { state: { error: err.error } }

Properties:
state?
queryParams?
fragment?
preserveFragment?
relativeTo?
replaceUrl?
...

.getCurrentNavigation(): 
-> returns the current Navigation object when the Route is navigating
-> return null when idle  

Properties:
-> extractedUrl, extras, finalUrl?, id, initialUrl, previousNavigation, trigger


.extras:
-> options that controlled the strategy used for this navigation

Properties: 
-> queryParams?, relativeTo?, replaceUrl?, preserveFragment?, fragment?, 
skipLocationChange?, state?, queryParamsHandling?, onSameUrlNavigation?, info?


.state?: {
[k: string]: any;
}


11.99. Configuring the Not found page

-- not-found.component.html --
-> icon: error_outline
-> button: routerLink="/shop" -> "Back to shop"

-- not-found.component.scss --
.icon-display {
    transform: scale(3);
}


11.100. Adding an HTTP Interceptor for loading

ng g s service/services/busy --skip-tests

-- busy.service.ts --
-> create busy(), idle() methods incrementing/decrementing busyRequestCount

ng g interceptor core/interceptors/loading--skip-tests

-> inject the interceptor into loading Service
-- loading.interceptor.ts --
const busyService = inject(BusyService);
busyService.busy();
return next(req).pipe(
    delay(500),
    finalize(() => busyService.idle())
)

-- app.config.ts --
provideHttpClient(withInterceptors([errorInterceptor, loadingInterceptor]))


11.101. Adding an progress bar to indicate loading 

https://material.angular.io/components/progress-spinner/overview
https://material.angular.io/components/progress-bar/overview

We will use:
https://material.angular.io/components/progress-bar/overview#indeterminate

-- header.component.ts --
MatProgressBar
busyService = inject(BusyService);

-- header.component.html --
@if (busyService.loading) {
  <mat-progress-bar mode="indeterminate" class="fixed top-20 z-50"></mat-progress-bar>
}

11.102. Making the header fixed to the top

-- header.component.html --
-> fix the header component and the progress bar to the top

11.103. Summary

Goal:
-> handle errors we receive from the API centrally and handled by Http interceptors
-> understand how to troubleshoot API errors

Questions:
1. Would we create an errors component in a 'real' app?
A: Probably not, but is helpful for learning purposes