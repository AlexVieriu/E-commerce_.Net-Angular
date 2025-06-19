8.63. Introduction 
-> adding components
-> http client module
-> observables
-> Typescript 

Goal:
-> able to use Http client to retrieve data from the API
-> understand the basics of observables and Typescript

8.64. Setting up the folder structure and creating components

-- client -> src -> app -> shared --
-> shared components 
-> shared directives
-> shared models

-- client -> src -> app -> features --
-> shop
-> shopping cart
-> checkout

-- client -> src -> app -> layout --
-> header
-> footer

Open the terminal: 
-> at the end of the terminal we have aliases(snippets) that we will use
cd skinet/client
ng help             
ng generate help 
ng generate component [name] (aliases: ng c [name])
ng generate guard [name] (aliases: ng g guard [name])

ng g c layout/header --dry-run 
    -> creates:
        -> header.component.ts
        -> header.component.html
        -> header.component.spec.ts
        -> header.component.scss
        
ng g c layout/header --skip-tests
    -> creates 
        -> header.component.ts
        -> header.component.html
        -> header.component.scss

Dictionary:
--dry-run : running the command without actually making any changes to the filesystem

What is a Route Guard? (ng generate guard)
-> allows you to control navigation to or from specific routes in your application

Folders:
-> auth.guard.ts
-> auth.guard.spec.ts

Check to see if the header component is created:
cd skinet/client
ng serve
-> browse to http://localhost:4200/


8.65. Adding a Header component
-- client -> src -> app -> layout -> header -> header.component.html --

https://material.angular.io/components/categories
-> Badge
-> Button

-- client -> src -> app -> layout -> header -> header.component.ts --
  imports: [
    MatIcon,
    MatButton,
    MatBadge
  ]

Dictionary:
border-b: 
-> border-bottom-width: 1px;

shadow-md: 
-> medium shadow (sm, md, lg, xl, 2xl) 

p-3:
-> padding: 0.75rem;

w-full:      
-> width: 100%

flex:
-> utilities for controlling how flex items both grow and shrink
https://tailwindcss.com/docs/flex

align-middle: 
-> vertical-align: middle;
-> align items along the center of the container’s cross axis
https://tailwindcss.com/docs/vertical-align#middle

items-center:
-> align-items: center;
-> align items along the center of the container’s cross axis
https://tailwindcss.com/docs/align-items#center

justify-between: 
-> justify-content: space-between; 
-> items along the container’s main axis such that there is an equal amount 
of space between each item 
https://tailwindcss.com/docs/justify-content#space-between

max-w-screen-2xl: 
-> max-width: 1536px;
-> sets the maximum width of the container to the predefined 2xl breakpoint value
-> classes can be used to give an element a max-width matching a specific breakpoint 
https://tailwindcss.com/docs/max-width#constraining-to-your-breakpoints

mx-auto:
-> margin-left: auto;
   margin-right: auto;
-> adds horizontal margin to the left and right of an element
-> This centers the div horizontally within its parent container
https://tailwindcss.com/docs/container#using-the-container

gap-3:
-> gap-* utilities to change the gap between both rows and columns in 
grid and flexbox layouts
-> gap-x-* and gap-y-*
https://tailwindcss.com/docs/gap#setting-the-gap-between-elements


8.66. Improving the header component
-- client -> public -- 
-> copy the images from the project(Course Assets) into client/public
-> restart the server: 
  ng serve

-- head.component.scss --
.customer-badge .mat-badge-content {
    width: 24px;
    height: 24px;
    font-size: 14px;
    line-height: 24px;
}

.customer-badge .mat-icon {
    font-size: 32px;
    width: 32px;
    height: 32px;
}

-- client -> src -> styles.scss --
// get access to the Angular Material
@use '@angular/material' as mat;
...
$customTheme: mat.define-theme();

@include mat.core();

.custom-theme {
    @include mat.all-component-themes($customTheme);

    // override the button color
    .mdc.button,
    .mdc-raised,
    .mdc-stroked-button,
    .mdc-flat-button {
        // use a tailwind class
        @apply rounded-md
    }
}

.container {
    @apply mx-auto max-w-screen-2xl
}

-- client -> src -> index.html --
<html lang="en" class="custom-theme">

Dictionary:
.mdc.button         : Base button
.mdc-raised         : Raised button with a shadow
.mdc-stroked-button : Outlined (stroked) button
.mdc-flat-button    : Flat button with no background

MDC - Material Design Components
-> set of Web Components developed by the Material Design team to provide reusable, 
customizable, and accessible UI components that adhere to Material Design guidelines
https://v15.material.angular.io/guide/mdc-migration
https://www.youtube.com/watch?v=DpCwKpZlcbg 

-> .mdc have now variables to the Material Design Tokens
https://m3.material.io/foundations/design-tokens/overview


8.67. Making http requests in Angular

-> start client app
ng serve

-> start API app
cd skinet/API
dotnet watch

-- client -> src -> app -> app.config.ts --
import { provideHttpClient } from '@angular/common/http';
...
provideHttpClient()

-> Angular use DI(Dependency Injection)

-- app.component.ts --
import { Component, inject, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';

export class AppComponent implements OnInit {
  baseUrl = "https://localhost:7096/api/"
  // inject HTTP Client
  private http = inject(HttpClient);
  title = 'Skinet';
  products: any[] = [];

  ngOnInit(): void {
    this.http.get<any>(this.baseUrl + 'products').subscribe({
      next: response => this.products = response.data,
      error: error => console.log(error),
      complete: () => console.info('complete')
    })
  }
}

-- app.component.html --
<div class="container mt-6">
    <h2 class="text-3xl font-bold underline">Welcome to {{title}}</h2>
    <ul>
        @for (product of products; track product.id){
        <li>{{product.name}}</li>
        }
    </ul>
</div>


Dictionary:
ngOnInit()
-> is one of Angular's lifecycle hooks
-> it is called after the component is initialized and ensures 
that the component’s input bindings are done
-> is often used for initializing data or making API calls 

-> this.http.delete 
this.http.delete<T>(url: string, options?: { headers, params, etc. })

-> this.http.get
this.http.get<T>(url: string, options?: { headers, params, etc. })

-> this.http.head
-> this.http.jsonp
-> this.http.options
-> this.http.patch
-> this.http.post
-> this.http.put
-> this.http.request

track product.id
-> optimize DOM updates when rendering a list of items


Modern Angular are using more signals and less lifecycle hooks

Relevant Traditional Lifecycle Hooks:
-> ngOnInit()       : used for initialization logic
-> ngOnDestroy()    : used for cleanup logic (now replaced by DestroyRef.onDestroy())
-> ngAfterViewInit(): used when you need to interact with fully initialized DOM elements

Rarely Used Traditional Hooks:
-> ngOnChanges()
-> ngDoCheck()
-> ngAfterContentInit()
-> ngAfterContentChecked()
-> ngAfterViewChecked()

New Rendering Hooks in Modern Angular:
-> afterRender()
-> afterNextRender()


8.68. Observables
-> a sequence of items that arrive asynchronously over time

Observables:
-> Asynchronous Data Handling(HTTP requests, WebSockets, etc.)
    -> allow developers to process data as it becomes available 
    rather than waiting for all the data to be received

-> Composability with RxJS Operators
    -> extensive capabilities for transforming, filtering, and combining streams of data

-> Lazy Evaluation
    -> meaning they don't execute until they are subscribed to
    -> ensures that resources are only consumed when needed

-> Multiple Emissions of Data
    -> multiple values can be emitted over time
    -> real-time updates, handling events, or streaming data from APIs

-> Cancellation and Cleanup
    ->  when you unsubscribe, it stops emitting data and cleans up resources


ChatGPT: "Give me an example for each Observable points in Angular 19"

8.68.1.png
8.68.2.png

HTTP, Observables and RxJS
-> HTTP Get request from ShopService
-> Receive the Observable and cast it into a Products array
-> Subscribe to the Observable from component
-> Assign the Products array to a local variable for us in the components template

RxJS
-> Reactive extensions for JavaScript
-> Utility library for working with Observables, similar to lodash or underscore
for JS objects and arrays
-> Uses the pipe() method to chain RxJS operators together


8.69. Typescript

Pro:
-> Type Safety - Catches errors during development
-> Better IDE Support - Enhanced autocomplete and refactoring
-> Code Documentation - Types serve as built-in documentation
-> Easier Refactoring - Safe large-scale code changes
-> Team Collaboration - Clearer contracts between developers

Cons:
-> learning curve and complexity
-> compilation and build overhead
-> false Sense of security with types (even if at compile time everything is safe, at runtime can appear bugs)


8.70. Typescript demo

-- src -> demo.ts --
let message: string | number = 'Hello World';
let isComplete: boolean = true;

let message2 = "Hello World";
let isComplete2 = true;

interface Todo {
    id: number
    title: string
    completed: boolean
}

type Todo2 = {
    id: number
    title: string
    completed: boolean
}

// list of Todos
let todos: Todo[] = []

// input : title 
// output: Todo object
function AddToDo(title: string): Todo {
    return {
        id: todos.length + 1, // give new todo a new Id
        title: title,
        completed: false
    }
}

// .push() method adds an item to the end of an array and updates the length of the array
function AddToDo2(title: string): Todo {
    const newToDo: Todo = {
        id: todos.length + 1, // give new todo a new Id
        title: title,
        completed: false
    }
    todos.push(newToDo) // Adds the newToDo to the todos array
    return newToDo
}

function toggleToDo(id: number): void {
    // todo : can be of type Todo | undefined
    const todo = todos.find(t => t.id === id)
    if (todo)
        todo.completed = !todo.completed
}


AddToDo2("Publish App")
toggleToDo(1)

console.log(AddToDo("Build API"))
console.log(todos)


-- Run the demo --
cd skinet/client
npx tsc src/demo.ts
press enter

-> after running the .ts we will get a .js version of the file

node src/demo.js
OutPut:
{ id: 2, title: 'Build API', completed: false }
[ { id: 1, title: 'Publish App', completed: true } ]


8.71. Using Typescript in our Project

-- shared -> models -> product.ts --
export type Product = {
    id: number;
    name: string;
    description: string;
    price: number;
    pictureUrl: string;
    type: string;
    brand: string;
    quantityInStock: number;
}

-> be sure to use CamelCase for the typescript files
-> make sure to be the same as the one coming form the API

-> shared -> models -> pagination.ts
export type Pagination<T> = {
    pageIdex: number;
    pageSize: number;
    count: number;
    data: T[]
}

-- app.component.ts --
import { Product } from "./shared/models/products";
import { Pagination } from './shared/models/pagination';


products: Product[] = [];

 this.http.get<Pagination<Product>>(this.baseUrl + 'products').subscribe({
    ...
 })

 -- app.component.html --
 -> now we have type safety


 8.72. Summary
 -> use the http client to retrieve data from the API
 -> understand the basics of observables and Typescript


Questions:
1. Can i use CSS instead of Angular Material and Tailwind?
-> with great difficulty
-> this is heavily integrated into the app

2. Are we making a responsive app (desktop, mobile, tablet, web)?
-> No