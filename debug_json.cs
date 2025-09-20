using System;
using System.Text.Json;

var testObject = new { MaxConcurrency = 2147483647 };
var json = JsonSerializer.Serialize(testObject, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine($"JSON: {json}");

var deserialized = JsonSerializer.Deserialize<TestClass>(json, new JsonSerializerOptions 
{
    PropertyNameCaseInsensitive = true
});

Console.WriteLine($"Deserialized MaxConcurrency: {deserialized.MaxConcurrency}");

public class TestClass 
{
    public int MaxConcurrency { get; set; } = 1;
}
