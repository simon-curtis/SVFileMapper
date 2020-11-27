using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using SVFileMapper.Extensions;
using System.Collections.Generic;

namespace SVFileMapper
{
    public static class SVFileParser
    {
        /// <summary>
        /// Reads through each line of a seperated value file (such as) provided in the class constructor and converts them to
        /// the desired object. This will return both an enumerable of parsed rows and an enumerable rows that failed
        /// to parse so you can check each row for errors.
        /// <br/><br/>
        /// ** TSV's are not currently supported
        /// </summary>
        /// <param name="filePath">The full file path of the file you want to read.</param>
        /// <param name="seperator">The character the lines in the file are seperated by.</param>
        /// <typeparam name="T">The type of object you want to cast each line to.</typeparam>
        /// <returns>An enumerable of parsed and failed lines</returns>
        public static async Task<ParseResults<T>> ParseFileAsync<T>(string filePath, char seperator)
        {
            var txtLines = File.ReadAllLines(filePath);

            var dt = new DataTable();

            var headers = SplitLine(txtLines[0], seperator);

            dt.Columns.AddRange(headers.Select(c => new DataColumn(c)).ToArray());

            for (var i = 1; i < txtLines.Length; i++)
            {
                var values = SplitLine(txtLines[i], seperator);
                dt.Rows.Add(values);
            }

            var (parsed, failed) = await dt.Rows.Cast<DataRow>().ParseRowsAsync<T>();

            return new ParseResults<T>(parsed, failed);
        }

        public static string RemoveDoubleQuotes(string part)
        {
            if (part[0] == '"') part = part.Substring(1);
            if (part.Last() == '"') part = part.Substring(0, part.Length - 1);
            part = part.Replace("\"\"", "\"");
            return part;
        }

        public static string[] SplitLine(string line, char seperator)
        {
            var elements = new List<string>();
            var startReadingFromIndex = 0;
            var insideString = false;

            void AddToElements(string subString)
            {
                var part = RemoveDoubleQuotes(subString);
                elements.Add(part);
            }

            for (var i = 0; i < line.Length; i++)
            {
                if (i + 1 == line.Length)
                {
                    AddToElements(line.Substring(startReadingFromIndex));
                    break;
                }
                
                if (line[i] == '"')
                {
                    if (line[i + 1] == '"')
                    {
                        i++;
                        continue;
                    }

                    insideString = !insideString;
                }
                else if (line[i] == seperator && !insideString)
                {
                    if (line[startReadingFromIndex] == seperator)
                        elements.Add("");
                    else
                        AddToElements(line.Substring(startReadingFromIndex, i - startReadingFromIndex));
                    
                    startReadingFromIndex = i + 1;
                }
            }

            return elements.ToArray();
        }
        
        public static async Task<ParseResults<T>> ParseRowsAsync<T>
            (this IEnumerable<DataRow> rows)
        {
            var count = 0;
            var max = rows.Count();
            
            var tasks = rows
                .Select(async row =>
                {
                    count++;
                    Console.WriteLine($"Rows processed: {count} of {max}");
                    return await row.ParseAsync<T>();
                })
                .ToList();

            var results = await Task.WhenAll(tasks);

            var (matched, unmatched) = results.Match(result => result.Success);
            var parsed = matched.Select(m => m.ParsedObject);
            var failed = unmatched.Select(u => u.Row);
            return new ParseResults<T>(parsed, failed);
        }

        public static Task<CastResult<T>> ParseAsync<T>(this DataRow row)
        {
            var obj = Activator.CreateInstance<T>();

            try
            {
                foreach (var property in obj.GetType().GetProperties())
                {
                    var customColumnName = property.GetCustomAttribute<CsvColumn>();
                    var columnName = customColumnName?.Name ?? property.Name;

                    var value = row[columnName].ToString()?.Trim();
                    if (value == null) continue;

                    if (property.PropertyType == typeof(bool))
                    {
                        property.SetValue(obj, value == "Yes");
                    }
                    else if (property.PropertyType == typeof(DateTime)
                             || property.PropertyType == typeof(DateTime?))
                    {
                        if (DateTime.TryParse(value, out var parsedDate)) property.SetValue(obj, parsedDate);
                    }
                    else
                    {
                        property.SetValue(obj, value);
                    }
                }

                return Task.FromResult(new CastResult<T>(true, obj, row));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return Task.FromResult(new CastResult<T>(false, obj, row));
            }
        }
    }
}