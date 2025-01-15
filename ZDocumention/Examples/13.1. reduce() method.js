// 1. Sum of Array
const numbers1 = [1, 2, 3, 4, 5];

const sum = numbers.reduce((accumulator, currentValue) => {
    return accumulator + currentValue;
}, 0);

console.log(sum); // Output: 15


// 2. Max Value
const numbers2 = [10, 5, 100, 25, 75];

const max = numbers.reduce((accumulator, currentValue) => {
    return currentValue > accumulator ? currentValue : accumulator;
}, numbers[0]);

console.log(max); // Output: 100


// 3. Flattening an Array of Arrays
const arrays2 = [[1, 2, 3], [4, 5], [6, 7, 8]];

const flattened = arrays.reduce((accumulator, currentValue) => {
    return accumulator.concat(currentValue);
}, []);

console.log(flattened); // Output: [1, 2, 3, 4, 5, 6, 7, 8]


// 4. Grouping by Property
const people = [
    { name: 'John', age: 25 },
    { name: 'Jane', age: 30 },
    { name: 'Jack', age: 25 },
    { name: 'Jill', age: 30 }
];

const groupedByAge = people.reduce((accumulator, currentValue) => {
    const age = currentValue.age;
    if (!accumulator[age]) {
        accumulator[age] = [];
    }
    accumulator[age].push(currentValue);
    return accumulator;
}, {});

console.log(groupedByAge);
// Output:
// {
//   25: [{ name: 'John', age: 25 }, { name: 'Jack', age: 25 }],
//   30: [{ name: 'Jane', age: 30 }, { name: 'Jill', age: 30 }]
// }


// 5. Counting Occurrences of Elements
const fruits = ['apple', 'banana', 'orange', 'apple', 'banana', 'banana'];

const fruitCount = fruits.reduce((accumulator, currentValue) => {
    if (accumulator[currentValue]) {
        accumulator[currentValue] += 1;
    } else {
        accumulator[currentValue] = 1;
    }
    return accumulator;
}, {});

console.log(fruitCount);
// Output: { apple: 2, banana: 3, orange: 1 }


// 6. Creating a Comma-Separated String
const words = ['apple', 'banana', 'cherry'];

const sentence = words.reduce((accumulator, currentValue) => {
    return accumulator + ', ' + currentValue;
});

console.log(sentence); // Output: "apple, banana, cherry"

