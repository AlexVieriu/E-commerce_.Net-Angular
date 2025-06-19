112. Introduction

-> adding the cart feature
-> angular signals
-> environment variables

Goal:
-> add cart features to angular app
-> understand the usage of signals in Angular


113. Creating the cart components

ng g s core/services/cart --skip-tests
ng g c features/cart --skip-tests

-- app.routes.ts --
{ path: 'cart', component: CartComponent }

!! the order of the route is very important !!

-- header.component.html --
-> add the link to shopping cart
<a routerLink="/cart" routerLinkActive="active"...


echo "" > src/app/shared/models/cartItem.ts
--cartItem.ts--

echo "" > src/app/shared/models/cartType.ts
-- cartType.ts --

echo "" > src/app/shared/models/cart.ts
-- cart.ts --

-> a tiny, secure, URL-friendly, unique string ID generator for JavaScript
-> https://github.com/ai/nanoid
npm install nanoid


Dictionary:
-- Nanoid vs GUID --

Use nanoid when:
-> You need shorter, readable, and customizable IDs
-> You want a lightweight solution without adhering to UUID specifications
-> Performance and efficiency are important (e.g., generating many IDs quickly)
-> The IDs will not need to conform to standardized UUID formats 
(e.g., for APIs expecting GUIDs)

Use GUID when:
-> You require strict adherence to UUID standards (e.g., 
for database keys or APIs that expect GUIDs)
-> Interoperability with other systems or languages that expect UUIDs is necessary
-> The larger size and format aren't a concern

More about app.routes.ts:
https://next.angular.dev/tutorials/learn-angular/12-enable-routing#create-an-approutests-file

Types or routes:
const routes: Routes = [
  // 1. Basic Routes - https://angular.dev/guide/routing/common-router-tasks#define-your-routes-in-your-routes-array
  { path: '', component: HomeComponent },

  // 2. Route Parameters - https://angular.dev/guide/routing/common-router-tasks#link-parameters-array
  { path: 'details/:id', component: ParentComponent },

  // 3. Nesting routes/Child routes - https://angular.dev/guide/routing/common-router-tasks#nesting-routes
  {
    path: 'parent',
    component: ParentComponent,
    children: [
      { path: 'child', component: ChildComponent },
    ],
  },

  // 4. Lazy Loaded Module - https://angular.dev/guide/routing/common-router-tasks#lazy-loaded-module
  {
    path: 'lazy',
    loadChildren: () => import('./lazy/lazy.module').then(m => m.LazyModule),
  },

  // 5. Routes Guards - https://angular.dev/guide/routing/common-router-tasks#preventing-unauthorized-access
  { path: '', component: HomeComponent },
  { path: 'login', component: LoginComponent },
  {
    path: 'dashboard',
    component: DashboardComponent,
    canActivate: [AuthGuard], // Protecting the dashboard route
  },

  // 6. Redirect
  { path: 'home', redirectTo: '', pathMatch: 'full' },
];


114. Angular signals

https://next.angular.dev/essentials/signals

What is a signal?
-> a lightweight wrapper around a value

Pros:
-> Simplicity
    -> gives as a API for managing the state of the app, 
    without the complexity of observables
    -> small footprint
-> Performance    
-> Readability

Cons:
-> Limited flexibility
    -> less mature (Angular 16) 
    -> observables: more mature (Angular 2)
-> Scalability

# We will use 3 main Types:
-> signal 
-> computed
-> effect

-- Signal example --
const counter = signal(0);

// Signals are getting functions - calling them reads their value
console.log("The count is: " + count());

a. update()
this.counter.update(count => count + 1);

b. set()
this.counter.set(10);

c. value (getter and setter)
const currentValue = this.counter.value;
this.counter.value = currentValue + 1;

d. computed (Creates a derived signal based on other signals.)
doubleValue = computed(() => this.counter() * 2);

e. effect (Creates a reactive effect that runs whenever the signals it depends on change)
effect(() => {
  console.log(`Counter changed: ${this.counter()}`);
});

ChatGPT:
1. "Canvas Give me 3 example with signal in Angular from really basic to advanced
(make examples: basic with signal, medium with .compute, advanced with effect). 
Create the project also so i can test it.
Give me the commands to create the Angular project, but make it minimal Angular. 
Give me also the structure where to put the code" 

2. What signals in Angular do different instead of doing things the traditional way?


115. Adding the Cart service methods

ng g --help
ng g environments

-- environment.development.ts --
export const environment = {
    production: false,
    apiUrl: 'https://localhost:7096/api/'
};

-- environment.ts --
export const environment = {
    production: true,
    apiUrl: 'api/'
};

-- cart.service.ts --
baseUrl = environment.apiUrl;
private http = inject(HttpClient);
cart = signal<Cart | null>(null);

 getCartAsync(){...}
 setCartAsync(cart: Cart){...}

Why not adding a .subscribe() method in the GET requests?
-> in RxJS, it's considered best practice to avoid subscribing too early or deep inside
 a service, especially if the data needs to be transformed or further processed
 by the consumer. Returning an observable gives the consumer full control


Dictionary:

-- .get properties --
.pipe() 
  -> RxJS operator
  -> allows to combine multiple RxJS operators to 
  transform, filter, manipulate the data stream before subscription

.subscribe() 
  -> RxJS operator
  -> where you actually execute the HTTP request and handle the response
  -> without subscribing, the HTTP request won't be made
  -> can handle success and error cases here

.forEach() 
  -> NOT RxJS operator   
  -> takes each value emitted by an Observable
  -> executes a callback function for each value
  -> return a Promise that resolves when the Observable completes
  -> cannot be unsubscribed from (this is important)


-- .pipe() & .subscribe() --
this.http.get<User[]>('api/users')
  .pipe(
    map(users => users.filter(user => user.active)),
    catchError(error => {
      console.error('Error fetching users:', error);
      return of([]); // Return empty array on error
    }),
    delay(1000) // Add artificial delay
  )
  .subscribe(users => {
    this.activeUsers = users;
  });

-- .forEach() --
const numbers$ = interval(1000); // Emits 0, 1, 2, 3... every second

async function logNumbers() {
  await numbers$.pipe(take(5)).forEach(num => {
      console.log(`Number emitted: ${num}`);
  })};

ChatGPT:
"Why we don't have a .subscribe() method in GET requests?"

Separation of Concerns: 
-> Services provide data, but components decide when and how to consume it, ensuring 
clear division of responsibilities

Flexibility: 
-> Returning an observable lets components control subscription, add operators, 
or handle results as needed.

Avoid Multiple Subscriptions: 
-> Subscribing in the service could lead to duplicate calls or memory leaks; 
components manage their own subscriptions.

Efficient Observable Handling: 
-> HttpClient observables auto-complete, so returning them is clean and aligns with 
Angular's reactive model

Signal Integration: 
-> Returning observables allows seamless integration with signals for reactive 
state management.

ChatGPT: "Canvas Popular RxJS Operators for .pipe() when we are talking about HTTP?"

-- .pipe() --
.map()        : Transforms the response data into the desired format
.catchError() : 
.filter()     : Filters the stream to process only certain HTTP requests or responses
.tap          : Performs side effects such as logging or debugging without affecting the data
.retry()      : Retries the HTTP request if it fails
.share()      : Shares the Observable between multiple subscribers
.switchMap()  : Cancels the previous HTTP request if a new one is made, 
                often used in search or type-ahead scenarios
.mergeMap()   : Allows multiple HTTP requests to run concurrently, but doesn’t cancel 
                previous requests     
.concatMap()  : Processes HTTP requests one at a time in order, ensuring the previous 
                one completes before the next begins
.exhaustMap() : Ignores subsequent HTTP requests while the first one is still in progress.  
                Often used in form submission     


116. Adding item to cart

-- cart.service.ts --
addItemToCart(item: CartItem | Product, quantity = 1){...}
private createCart(): Cart{...} 
private mapProductToCartItem(product: Product): CartItem {...}

// type guard
-> when we have 2 different types in the same method use type guard
private isProduct(item: CartItem | Product): item is Product {
    return (item as Product).id !== undefined;
}

-> the function returns "item is Product"


Dictionary:

What is a type guard?
-> a function that checks if a value is of a specific type
-> it helps TypeScript understand the type of a variable at Runtime
base on conditions

"undefined"
-> a variable has been declared but was never assigned a value
-> if a function doesn't return a value, it returns "undefined"

Ex 1:
let x;
console.log(x); // Output: undefined

Ex 2:
const person = { name: "Alice" };
console.log(person.age); // Output: undefined

-- "!= vs !==" --

0 !== false         -> true 
null !== undefined  -> true 

0 != false          -> false 
null != undefined   -> false 

0 == false          -> true
0 === false         -> false

null == undefined   -> true
null === undefined  -> false

ChatGPT:
"Why not check the object type directly in the body?"

private isProduct(item: object): void {
  if (item instanceof Product) {
    // do something for Product
  } else if (item instanceof CartItem) {
    // do something else for CartItem
  }
}

"instanceof"
-> used to check whether an object is an instance of a class
-> check if item is instance of Product or CartItem

Compile-Time: TypeScript types, interfaces, generics
Runtime     : JavaScript objects, classes, functions, variables.

-> if Product and CartItem are types/interfaces, you will get 
compile-time errors with "instanceof"
-> you need to rely on property checks or type guards

private isProduct(item: object): void {
  if ((item as Product).id !== undefined) {
    // do something for Product
  } else if ((item as CartItem).id !== undefined) {
    // do something else for CartItem
  }
}


117. Using the add item functionality in the product item

-- product-item.component.ts --
  cartService = inject(CartService);

-- product-item.component.html --
<mat-card-actions (click)="$event.stopPropagation()">
(click)="cartService.addItemToCart(product)"

Testing:
-> chrome -> network
    -> check headers (we see a POST)
    -> check payload (we see the items[])
-> check redis

Dictionary:
"$event.stopPropagation()":
-> prevent routerLink="/shop/{{product.id}}" to be access
when we use the click event


118. Persisting the cart

-> cart.id need to be in localStorage
-> with that id, we go to redis, and store it in the Angular signal 
that we are using for the cart

ng g s core/services/init --skip-tests

-- cart.service.ts --
 getCartAsync(id: string) {
    return this.http.get<Cart>(this.baseUrl + 'cart?id=' + id).pipe(map(cart => {
      this.cart.set(cart);
      return cart;
    }))
  }

-- init.service.ts --
init() {
  const cartId = localStorage.getItem('cart_id');
  const cart$ = cartId ? this.cartService.getCartAsync(cartId) : of(null);

  return cart$;
}

-- app.config.ts --

-> we create some HTML that we will display, while initialization is taking place
-> in this case it's going to get our cart(if possible), in the meantime 
will display our slash screen

function initializeApp() {
  const initService = inject(InitService);
  return lastValueFrom(initService.init()).finally(() => {
    const splash = document.getElementById('initial-splash');
    if (splash) {
      splash.remove();
    }
  });
}

provideAppInitializer(initializeApp)

-- idex.html --
-> display the logo.png

Test the functionality:
-> run the program
-> open the browser -> Network  
  -> cart?id=


Dictionary:

Upgrading to Angular 19
https://medium.com/sekrabgarage/upgrading-to-angular-version-19-2e3ba8d61d35

provideAppInitializer():
-> the provided function is injected at app startup and executed during app initialization
-> if the function returns a/an Promise/Observable, initialization does not
complete until the Promise/Observable resolves/completed
-> works with Observables but not with signals

.init():
-> returns an Observable
-> checks if the Id , if true is in localStorage, if id is not found,
 send a request to the API to get the cart
-> in the response, set the cart to the signal first using .map(), 
then returns the cart (as Observable)


.lastValueFrom() - https://rxjs.dev/api/index/function/lastValueFrom
-> Converts an observable to a promise by subscribing to the observable, 
waiting for it to complete, and resolving the returned promise with the
last value from the observed stream.

**WARNING**: Only use this with observables you *know* will complete. If the source
 * observable does not complete, you will end up with a promise that is hung up, and
 * potentially all of the state of an async function hanging out in memory

.firstValueFrom() - https://rxjs.dev/api/index/function/firstValueFrom 
-> Converts an observable to a promise by subscribing to the observable,
 and returning a promise that will resolve as soon as the first value
 arrives from the observable. The subscription will then be closed.

.finally()
-> ensures that cleanup tasks are performed regardless of weather the Observable
succeeds or fails
  -> it looks for an element with the ID initial-splash, typically used 
  as a loading or splash screen displayed during app startup
  -> if found, the splash element is removed from the DOM, signaling 
  that the app has fully initialized

ChatGPT: 
"Canvas Tell me about Observables in Angular 19. The example needed to be 
in a E-commerce app. Write it in deep details so i can understand. 
I m new in angular , coming from C#"

Observables:
https://next.angular.dev/guide/http/making-requests#http-observables


13.119. Updating the nav bar with the cart item count

-- Computed signals -- (https://angular.dev/guide/signals#computed-signals) 
-> read-only signals that derive their value from other signals

ex: 
const count: WritableSignal<number> = signal(0);
const doubleCount: Signal<number> = computed(() => count() * 2);

-- cart.service.ts --
itemCount = computed(() => {
  return this.cart()?.items.reduce((sum, item) => sum + item.quantity, 0)
})

-- header.component.ts --
 cartService = inject(CartService);

-- header.component.html --
<a routerLink="/cart" routerLinkActive="active" 
matBadge="{{cartService.itemCount()}}" matBadgeSize="large"
...
</a>

-> test the basket

Dictionary:

-- .reduce() -- https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Array/reduce#callbackfn
-> calls the specified callback function for all the elements in an array. 
-> the return value of the callback function is the accumulated result, and is 
provided as an argument in the next call to the callback function

Syntax:
array.reduce((accumulator, currentValue, currentIndex, array) => {
    // function logic
}, initialValue);

ChatGPT:
"give me examples with JavaScript .reduce() method"

Test JS in VSC:
-> create a .js folder 
-> write the code
-> in the terminal write: node "[name of the js folder]
ex: node "sum.js"


120. Styling the cart

-- cart.component.ts --
cartService = inject(CartService);

-- cart.component.html --
@for(item of cartService.cart()?.items; track item.productId){
<div>{{item.productName}} - {{item.quantity}}</div>
}

ng g c features/cart/cart-item --skip-tests

-- cart-item.component.ts --
item = input.required<CartItem>();

-- cart-item.component.html --

-- cart.component.html --
<app-cart-item [item]="item"></app-cart-item>


Dictionary:
-- input --
-> are signals 
-> allows declaration of Angular inputs in directives

Types of inputs:
1. **Optional inputs** with an initial value.
2. **Required inputs** that consumers need to set.

ChatGPT:
"I have this list of methods for the .input in Angular in Visual Studio Code. 
Please explain each of them with a simple example and also put the output"


What is a directive? - https://angular.dev/guide/directives 
-> is a class that allows you to attach behavior to elements in the DOM
(Document Object Model)
-> used to manipulate the appearance, behavior or structure of DOM
elements in the app


Types of directives:
-> Structural directives - https://angular.dev/guide/directives#built-in-structural-directives
-> Attribute directives - https://angular.dev/guide/directives#built-in-attribute-directives
-> Component directives - https://angular.dev/guide/components 


121. Creating the order summary component - part 1

ng g c shared/components/order-summary --skip-tests

-- order-summary.component.html --

-- cart.component.html --
<app-order-summary></app-order-summary>


122. Creating the order summary component - part 2

-- order-summary.component.html --

-- order-summary.component.ts --
imports: [MatButton, RouterLink, MatFormField, MatLabel, MatInput]


123. Creating the order totals

-- cart.service.ts --
-> create a computed signal property: totals
  -> use computed()
  -> include the cart value: cart = signal<Cart | null>(null);
  -> use cart.items.reduce((accumulator, currentValue) => ...)
  -> return an anonyms object with: subtotal, shipping, discount, total

-- order-summary.component.html --
-> cartService = inject(CartService);

-- order-summary.component.html --
-> add subtotal, shipping, discount, totals


124. Adding additional functions to the service

removeItemFromCart(productId: number, quantity = 1)
deleteCart()

Dictionary:
.findIndex -> returns the index of the first element in the array where predicate
            is true, and -1 otherwise
.find      -> returns the value of the first element in the array where predicate
            is true, and undefined otherwise

Use:
.find()       -> You need to directly modify or read the element
.findIndex()  -> You need the position (index) of the element

Most used functions for an Array:

// .push([element1, element2, ...])
let arr = [1, 2, 3];
arr.push(4);  // [1, 2, 3, 4]

// .pop()
let arr = [1, 2, 3];
arr.pop();  // [1, 2]

// .shift()
let arr = [1, 2, 3];
arr.shift();  // [2, 3]

// .unshift([element1, element2, ...])
let arr = [1, 2, 3];
arr.unshift(0);  // [0, 1, 2, 3]

// .concat([array1, array2, ...])
let arr1 = [1, 2];
let arr2 = [3, 4];
let arr3 = arr1.concat(arr2);  // [1, 2, 3, 4]

// .slice([indexStart], [indexEnd])
let arr = [1, 2, 3, 4];
let newArr = arr.slice(1, 3);  // [2, 3]

// .splice([indexStart], [deleteCount], [item1, item2, ...])
let arr = [1, 2, 3];
arr.splice(1, 1);  // [1, 3]

// .forEach([callbackFn])
let arr = [1, 2, 3];
arr.forEach(element => console.log(element));  // 1 2 3

// .map([callbackFn])
let arr = [1, 2, 3];
let newArr = arr.map(x => x * 2);  // [2, 4, 6]

// .filter([callbackFn])
let arr = [1, 2, 3, 4];
let newArr = arr.filter(x => x % 2 === 0);  // [2, 4]

// .reduce([callbackFn], [initialValue])
let arr = [1, 2, 3];
let sum = arr.reduce((acc, val) => acc + val, 0);  // 6

// .reduceRight([callbackFn], [initialValue])
let arr = [1, 2, 3];
let sum = arr.reduceRight((acc, val) => acc + val, 0);  // 6

// .some([callbackFn])
let arr = [1, 2, 3];
let hasEven = arr.some(x => x % 2 === 0);  // true

// .every([callbackFn])
let arr = [1, 2, 3];
let allEven = arr.every(x => x % 2 === 0);  // false

// .find([callbackFn])
let arr = [1, 2, 3];
let found = arr.find(x => x > 1);  // 2

// .findIndex([callbackFn])
let arr = [1, 2, 3];
let index = arr.findIndex(x => x > 1);  // 1

// .indexOf([searchElement], [fromIndex])
let arr = [1, 2, 3];
let index = arr.indexOf(2);  // 1

// .join([separator])
let arr = [1, 2, 3];
let joined = arr.join('-');  // "1-2-3"

// .sort([compareFn])
let arr = [3, 1, 2];
arr.sort();  // [1, 2, 3]

// .reverse()
let arr = [1, 2, 3];
arr.reverse();  // [3, 2, 1]


125. Adding these functions to the cart 

-- cart-item.component.ts --
incrementQuantity() {
  this.cartService.addItemToCart(this.item());
}

decrementQuantity() {
  this.cartService.removeItemFromCart(this.item().productId);
}

removeItemFromCart() {
  this.cartService.removeItemFromCart(this.item().productId, this.item().quantity);
}

-- cart-item.component.html --
-> add the methods 


126. Adding the update cart functionality to the product details

-- product-details.component.ts --

updateQuantityInCart() {
  if (this.product) {
    this.quantityInCart = this.cartService.cart()?.items
      .find(i => i.productId === this.product?.id)?.quantity || 0;

    this.quantity = this.quantityInCart || 1;
  }
}

updateCart() {
  if (!this.product) return;
  if (this.quantity > this.quantityInCart) {
    const itemsToAdd = this.quantity - this.quantityInCart;
    this.quantityInCart += itemsToAdd;
    this.cartService.addItemToCart(this.product, itemsToAdd);
  } else {
    const itemsToRemove = this.quantityInCart - this.quantity;
    this.quantityInCart -= itemsToRemove;
    this.cartService.removeItemFromCart(this.product.id, itemsToRemove);
  }
}

getButtonText() {
  return this.quantityInCart > 0 ? 'Update cart' : 'Add to cart'
}

-- product-details.component.html --

<p>You have {{quantityInCart}} of this item in your cart</p>
...
<button [disabled]="quantity === quantityInCart" (click)="updateCart()" mat-flat-button
...
Add to cart -> {{getButtonText()}} // for Text change based on quantity
...
<input matInput min="0" [(ngModel)]="quantity" type="number">


127. Creating the checkout components 

ng g s core/services/checkout --skip-tests
ng g c features/checkout --skip-tests

-- app.routes.ts --
{ path: 'checkout', component: CheckoutComponent }

-- checkout.component.html --


128. Summary 

-> add cart features to angular app
-> understand the usage of signals in Angular