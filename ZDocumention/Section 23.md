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