using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            var txtLines = await File.ReadAllLinesAsync(filePath);
            
            var dt = new DataTable();

            dt.Columns.AddRange(
                SplitLine(txtLines[0], seperator)
                    .Select(c => new DataColumn(c)).ToArray()
            );

            foreach (var line in txtLines[1..])
            {
                var values = SplitLine(line, seperator);
                dt.Rows.Add(values);
            }

            var (parsed, failed) = await dt.Rows.Cast<DataRow>().ParseRowsAsync<T>();

            return new ParseResults<T>(parsed, failed);
        }

        public static string RemoveDoubleQuotes(string part)
        {
            if (part[0] == '"') part = part[1..];
            if (part[^1] == '"') part = part[..^1];
            part = part.Replace("\"\"", "\"");
            return part;
        }
        
        public static IEnumerable<string> SplitLine(string line, char seperator)
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
                    AddToElements(line[startReadingFromIndex..]);
                }
                else if (line[i] == '"')
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
                    AddToElements(line[startReadingFromIndex..i]);
                    startReadingFromIndex = i + 1;
                }
            }

            return elements;
        }
    }
}