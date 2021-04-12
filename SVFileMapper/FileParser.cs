using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SVFileMapper.Extensions;
using SVFileMapper.Models;
using SVFileMapper.Models.DataAnnotations;

namespace SVFileMapper
{
    public class FileParser<T> : IFileParser<T>
    {
        private readonly ILogger _logger;
        private readonly ParserOptions _options;
        private readonly char _seperator;

        public FileParser()
        {
            _seperator = ',';
            _options = new ParserOptions();
            _logger = new FakeLogger();
        }

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

        private ICollection<(string name, PropertyInfo property)> Properties { get; set; }
            = new List<(string name, PropertyInfo property)>();

        public async Task<ParseResults<T>> ParseFileAsync
            (string filePath, IProgress<ParserProgress>? progress = null)
        {
            if (_logger is not FakeLogger)
            {
                _logger.LogCritical("WARNING: You've set a Progress indicator on this class, this will severly slow " +
                                    "down the code as it forces synchronous behavior");
            }
            
            Properties = typeof(T).GetProperties()
                .Where(p => p.GetCustomAttribute<ColumnIgnored>() == null)
                .Select(p => (p.GetCustomAttribute<ColumnName>()?.Name ?? p.Name, p))
                .ToList();

            var dt = new DataTable();

            using (var reader = new StreamReader(filePath))
            {
                var processedHeaders = !_options.HasHeaders;
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (!processedHeaders)
                    {
                        var columns = FileParserTools.SplitLine(line, _seperator)
                            .Select(header => new DataColumn(header))
                            .ToArray();
                        
                        dt.Columns.AddRange(columns);
                        processedHeaders = true;
                        continue;
                    }

                    var dr = await ExtractObjectsAsync(line);
                    dt.Rows.Add(dr);
                }
            } 
            
            if (dt.Rows.Count == 0)
            {
                _logger.LogCritical("No rows in file");
                return new ParseResults<T>(Array.Empty<T>(), Array.Empty<UnmatchedResult>());
            }

            var (rowsParsed, rowsFailed) = await ParseRowsAsync(dt.Rows.Cast<DataRow>(), progress);
            
            return new ParseResults<T>(rowsParsed, rowsFailed);
        }

        private Task<object[]> ExtractObjectsAsync(string line)
        {
            return Task.Run(() => FileParserTools.SplitLine(line, _seperator).ToArray<object>());
        }

        private async Task<ParseResults<T>> ParseRowsAsync
            (IEnumerable<DataRow> rows, IProgress<ParserProgress>? progress = null)
        {
            var count = 0;
            var dataRows = rows as DataRow[] ?? rows.ToArray();
            var max = dataRows.Length;

            Task<CastResult<T>> ParseRowTask(DataRow row)
            {
                progress?.Report(new ParserProgress(++count, max));

                CastResult<T> result;
                try
                {
                    (T parsedObject, var failedColumns) = ParseRow(row);
                    var columnConversions = failedColumns.ToList();

                    result = columnConversions.Any()
                        ? new CastResult<T>(false, parsedObject, row, columnConversions)
                        : new CastResult<T>(true, parsedObject, row);
                }
                catch (Exception ex)
                {
                    result = new CastResult<T>(false, Activator.CreateInstance<T>(), row,
                        Array.Empty<FailedColumnConversion>(), ex.Message);
                }

                return Task.FromResult(result);
            }

            var tasks = dataRows.Select(ParseRowTask).ToList();

            var results = await Task.WhenAll(tasks);

            var (matched, unmatched) = results.Partition(result => result.Success);
            var parsed = matched.Select(m => m.ParsedObject);
            var failed = unmatched.Select(u => new UnmatchedResult
            {
                Row = u.Row,
                FailedColumnConversions = u.FailedColumnConversions
            });
            return new ParseResults<T>(parsed, failed);
        }

        private (T parsedObject, IEnumerable<FailedColumnConversion> failedColumnConversions) ParseRow(DataRow row)
        {
            T obj = Activator.CreateInstance<T>();
            var failures = new List<FailedColumnConversion>();

            foreach (var (name, property) in Properties)
                try
                {
                    if (!row.Table.Columns.Contains(name))
                        continue;

                    var value = row[name].ToString()?.Trim();
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
                        if (DateTime.TryParse(value, out DateTime parsedDate))
                            property.SetValue(obj, parsedDate);
                        continue;
                    }

                    if (property.PropertyType == typeof(string))
                    {
                        property.SetValue(obj, value.Trim());
                        continue;
                    }

                    property.SetValue(obj, value);
                }
                catch (Exception ex)
                {
                    failures.Add(new FailedColumnConversion
                    {
                        Column = row.Table.Columns[name],
                        Reason = ex.Message
                    });
                }

            return (obj, failures);
        }
    }

    public static class FileParserTools
    {
        public static IEnumerable<string> SplitLine(string line, char seperator)
        {
            Span<char> lineSpan = line.ToArray();
            var parts = new List<string>();

            var insideString = false;
            var escapeChar = false;
            var readFromIndex = 0;
            for (int i = 0; i < lineSpan.Length; i++)
            {
                switch (lineSpan[i])
                {
                    case '\\':
                        escapeChar = true;
                        break;
                    
                    case '"' when escapeChar == false:
                        insideString = !insideString;
                        break;
                }

                if (lineSpan[i] == seperator && !insideString && !escapeChar)
                {
                    parts.Add(lineSpan[readFromIndex..i].ToString());
                    readFromIndex = i + 1;
                }

                if (i + 1 == lineSpan.Length)
                {
                    parts.Add(lineSpan[readFromIndex..(i + 1)].ToString());
                    break;
                }

                escapeChar = false;
            }

            return parts.Select(RemoveDoubleQuotes);
        }
        
        private static string RemoveDoubleQuotes(string part)
        {
            if (part == "") return part;
            if (part[0] == '"') part = part.Substring(1);
            if (part.Last() == '"') part = part.Substring(0, part.Length - 1);
            part = part.Replace("\"\"", "\"");
            return part;
        }
    }
}