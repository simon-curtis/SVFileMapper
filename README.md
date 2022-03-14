# SV File Mapper

A very simple mapping tool for SV (Separated Values) files such as CSV (comma) or PSV (pipe).

** Please note that this is a personal project and not fit for production use yet.

## Compatability

This library targets .NET 6.0, to keep it at the cutting edge of performance

## Getting Started

Add package to project.

| Agent | Command |
|:-|:-|
| Dotnet CLI:            | dotnet add package SVFileMapper|
| NuGet Package Manager: | Install-Package SVFileMapper |

In your program:

```c#
using SVFileMapper;
using SvfMapper.Models;

namespace YourProgram {

  public async static class Program {
  
    private const string FilePath = @"~\Example.csv";
  
    public static async Task Main() {
      var parseResults = await new FileParser<Line>()
          .ParseFileAsync<Employee>(filePath);
      
      foreach (var obj in parseResults.Matched)
          Console.WriteLine(obj.EmployeeId);

      foreach (var obj in parseResults.UnmatchedLines)
          Console.WriteLine(obj["EmployeeId"]);  
      
    }
  }
  
  internal class Employee {
    // You can omit the attribute if column is the same as the name of the property,
    // this can be handy if the column name contains a space.
    [CsvColumn("Employee Id")] 
    public string EmployeeId { get; set; }
  }
}


```
