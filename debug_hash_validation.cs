using System;
using System.Text.RegularExpressions;

class Program {
    static void Main() {
        string hash = "192:znnnb:n";
        var parts = hash.Split(':');
        Console.WriteLine($"Hash: {hash}");
        Console.WriteLine($"Parts count: {parts.Length}");
        for (int i = 0; i < parts.Length; i++) {
            Console.WriteLine($"Part {i}: '{parts[i]}' (length: {parts[i].Length})");
        }
        
        bool blockSizeValid = int.TryParse(parts[0], out _);
        Console.WriteLine($"Block size valid: {blockSizeValid}");
        
        bool part1Valid = Regex.IsMatch(parts[1], @"^[A-Za-z0-9+/]*$");
        bool part2Valid = Regex.IsMatch(parts[2], @"^[A-Za-z0-9+/]*$");
        Console.WriteLine($"Part 1 valid: {part1Valid}");
        Console.WriteLine($"Part 2 valid: {part2Valid}");
    }
}
