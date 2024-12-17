var message = 'Hello World';
var isComplete = true;
var message2 = "Hello World";
var isComplete2 = true;
// list of Todos
var todos = [];
// input : title 
// output: Todo object
function AddToDo(title) {
    return {
        id: todos.length + 1, // give new todo a new Id
        title: title,
        completed: false
    };
}
// .push() method adds an item to the end of an array and updates the length of the array
function AddToDo2(title) {
    var newToDo = {
        id: todos.length + 1, // give new todo a new Id
        title: title,
        completed: false
    };
    todos.push(newToDo); // Adds the newToDo to the todos array
    return newToDo;
}
function toggleToDo(id) {
    // todo : can be of type Todo | undefined
    var todo = todos.find(function (t) { return t.id === id; });
    if (todo)
        todo.completed = !todo.completed;
}
AddToDo2("Publish App");
toggleToDo(1);
console.log(AddToDo("Build API"));
console.log(todos);
