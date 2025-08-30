248. Updating the .Net project
-> update projects to .net 10

-- skinet -> docker-compose.yml --
https://hub.docker.com/r/keinos/sqlite3
https://hub.docker.com/_/redis 

docker compose down -v
docker compose up -d


249. Update the Angular project
-> update to Angular 20

-> update Angular from 18 to 20 ( can be other instructions so look at the update guide !!!)
https://angular.dev/update-guide

1. ng update @angular/core@19 @angular/cli@19

2. ng update @angular/material@19

3. Angular directives, components and pipes are now standalone by default. 
Specify "standalone: false" for declarations that are currently declared in an NgModule. 
The Angular CLI will automatically update your code to reflect that.

And so on... 


Style Guid was massively update for Angular 20
https://angular.dev/style-guide


*ngIf* - deprecated
Instead of:
<section *ngIf="!signalrService.orderSignal()" class="bg-white py-16">

Write like this:
@if(signalrService.orderSignal();)
{
    <section class="bg-white py-16">
    ...
}


250. Update to Tailwind 4 and Angular Material 20

-- package.json --
-> when we run the update of tailwindcss, it doesn't remove the 
tailwindcss and postcss from the package.json, so we will remove them manually:
 "devDependencies": {
"tailwindcss": "^3.4.17",
"postcss": "^8.5.6"
...
 }

Docs:
https://tailwindcss.com/docs/installation/framework-guides/angular

1. 
cd skinet/client
npm install tailwindcss @tailwindcss/postcss

-- package.json --
"@tailwindcss/postcss": "^4.1.12",
"tailwindcss": "^4.1.12"

2. client -> .postcssrc.json
{
  "plugins": {
    "@tailwindcss/postcss": {}
  }
}

3.  
-> we don't have .css, we have .scss
-> we create a new file:
-- src -> styles.css --
@import "tailwindcss";

-> we have "important: true" on tailwind.config.json.js
-> to replicate that in our style.css:
@import "tailwindcss" important;
-> tailwind.config.json.js

4. we need to tell angular about our tailwind.css file

-- angular.json --
"styles": [
    "@angular/material/prebuilt-themes/azure-blue.css",
    "src/tailwind.css",
    "src/styles.scss"
],

Test it:
ng s
-> make modifications on styles.scss

Take in consideration VSC don't know Tailwind commands 

Angular Material:
https://material.angular.dev/components/button/styling
https://material.angular.dev/components/snack-bar/styling


-> change color in .html
there is not "warn" color now:  <mat-icon color="warn">undo</mat-icon>

How to find where a package is used:
npm ls [name of the package]
nmp ls cross-spawn