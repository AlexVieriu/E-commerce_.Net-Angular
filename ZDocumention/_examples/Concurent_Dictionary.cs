using System.Collections.Concurrent;


// Regular Dictionary - not thread-safe
Dictionary<int, string> regularDict = new Dictionary<int, string>();

// ConcurrentDictionary - thread-safe
ConcurrentDictionary<int, string> concurrentDict = new ConcurrentDictionary<int, string>();

// This might throw an exception with regularDict due to concurrent modification
try
{
    Parallel.For(0, 1000, i =>
    {
        // This is unsafe and might cause exceptions
        regularDict[i] = $"Value {i}";

        // The line below might throw if another thread adds the same key first
        // regularDict.Add(i, $"Value {i}");
    });
}
catch (Exception ex)
{
    Console.WriteLine($"Regular dictionary error: {ex.Message}");
}

// This is safe with ConcurrentDictionary
Parallel.For(0, 1000, i =>
{
    // Thread-safe way to add or update
    concurrentDict.AddOrUpdate(
        key: i,
        addValue: $"Value {i}",
        updateValueFactory: (key, oldValue) => $"Updated {i}"
    );

    // Or use GetOrAdd for "add if doesn't exist" semantics
    string value = concurrentDict.GetOrAdd(i + 1000, k => $"New value {k}");
});

Console.WriteLine($"Regular dictionary count: {regularDict.Count}");
Console.WriteLine($"Concurrent dictionary count: {concurrentDict.Count}");
