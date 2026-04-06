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