using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SVFileMapper.Extensions;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using SVFileMapper.Models;
using SVFileMapper.Models.DataAnnotations;

namespace SVFileMapper
{
    public class FileParser : IFileParser
    {
        private readonly char _seperator;
        private readonly ParserOptions _options;
        private readonly ILogger _logger;

        public FileParser(Action<ParserOptions> options)
        {
            _options = new ParserOptions();
            options.Invoke(_options);

            _logger = _options.Logger ?? new FakeLogger();

            switch (_options.SeperatingCharacter)
            {
                case Separator.Comma:
                    _seperator = ',';
                    break;
                case Separator.Pipe:
                    _seperator = '|';
                    break;
                case Separator.Tab:
                    _seperator = '\t';
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(_options.SeperatingCharacter),
                        _options.SeperatingCharacter, null);
            }
        }
        
        public async Task<ParseResults<T>> ParseFileAsync<T>
            (string filePath,  IProgress<ParserProgress> progress = null)
        {
            if (progress != null)
            {
                _logger?.LogCritical("WARNING: You've set a Progress indicator on this class, this will severly slow " +
                                    "down the code as it forces synchronous behavior");
            }
            
            var dt = new DataTable();

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var sr = new StreamReader(fs))
            {
                var headers = _options.HasHeaders
                    ? SplitLine(await sr.ReadLineAsync(), _seperator)
                        .Select(c => c.ToString())
                    : typeof(T).GetProperties()
                        .Select(info => info.Name);

                dt.Columns.AddRange(headers
                    .Select(header => new DataColumn(header))
                    .ToArray());

                string line;
                while ((line = await sr.ReadLineAsync()) != null)
                {
                    var values = SplitLine(line, _seperator).ToArray();
                    dt.Rows.Add(values);
                }
            }

            if (dt.Rows.Count == 0)
            {
                _logger.LogCritical("No rows in file");
                return new ParseResults<T>(new T[0], new DataRow[0]);
            }
            
            var (rowsParsed, rowsFailed) = await ParseRowsAsync<T>(dt.Rows.Cast<DataRow>(), progress);
            var parsed = rowsParsed;
            var failed = rowsFailed;

            return new ParseResults<T>(parsed, failed);
        }

        private async Task<ParseResults<T>> ParseRowsAsync<T>
            (IEnumerable<DataRow> rows,  IProgress<ParserProgress> progress = null)
        {
            var count = 0;
            var dataRows = rows as DataRow[] ?? rows.ToArray();
            var max = dataRows.Length;
            
            var tasks = dataRows
                .Select(async row =>
                {
                    progress?.Report(new ParserProgress(++count, max));

                    try
                    {
                        return new CastResult<T>(true, await ParseRowAsync<T>(row), row);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.Message);
                        var obj = Activator.CreateInstance<T>();
                        return new CastResult<T>(false, obj, row);
                    }
                })
                .ToList();
            
            var results = await Task.WhenAll(tasks);

            var (matched, unmatched) = results.Match(result => result.Success);
            var parsed = matched.Select(m => m.ParsedObject);
            var failed = unmatched.Select(u => u.Row);
            return new ParseResults<T>(parsed, failed);
        }

        private Task<T> ParseRowAsync<T>(DataRow row)
        {
            var obj = Activator.CreateInstance<T>();

            foreach (var property in obj.GetType().GetProperties())
            {
                var columnName = _options.HasHeaders 
                    ? property.GetCustomAttribute<ColumnName>()?.Name ?? property.Name 
                    : property.Name;

                var value = row[columnName].ToString()?.Trim();
                if (value == null) continue;

                if (property.PropertyType == typeof(int))
                {
                    if (int.TryParse(value, out var result))
                        property.SetValue(obj, result);
                    continue;
                }

                if (property.PropertyType == typeof(bool))
                {
                    if (bool.TryParse(value, out var result))
                        property.SetValue(obj, result);

                    if (_options.AdditionalBooleanValues.Keys.Contains(value))
                        property.SetValue(obj, _options.AdditionalBooleanValues[value]);
                    continue;
                }

                if (property.PropertyType == typeof(DateTime)
                    || property.PropertyType == typeof(DateTime?))
                {
                    if (DateTime.TryParse(value, out var parsedDate))
                        property.SetValue(obj, parsedDate);
                    continue;
                }

                property.SetValue(obj, value);
            }

            return Task.FromResult(obj);
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

        private static string RemoveDoubleQuotes(string part)
        {
            if (part[0] == '"') part = part.Substring(1);
            if (part.Last() == '"') part = part.Substring(0, part.Length - 1);
            part = part.Replace("\"\"", "\"");
            return part;
        }
    }
}