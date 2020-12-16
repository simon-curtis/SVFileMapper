using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SVFileMapper.Extensions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
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

        private ICollection<(string name, PropertyInfo property)> Properties { get; set; }

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
            (string filePath, IProgress<ParserProgress> progress = null)
        {
            if (progress != null)
            {
                _logger?.LogCritical("WARNING: You've set a Progress indicator on this class, this will severly slow " +
                                     "down the code as it forces synchronous behavior");
            }

            Properties = typeof(T).GetProperties()
                .Where(p => p.GetCustomAttribute<ColumnIgnored>() == null)
                .Select(p => (p.GetCustomAttribute<ColumnName>()?.Name ?? p.Name, p))
                .ToList();

            var dt = new DataTable();

            var lines = File.ReadAllLines(filePath);
            if (lines.Length < 2)
            {
                _logger.LogCritical("No rows in file");
                return new ParseResults<T>(Array.Empty<T>(), new DataRow[0]);
            }

            dt.Columns.AddRange(
                SplitLine(lines[0], _seperator)
                    .Select(header => new DataColumn(header))
                    .ToArray());

            var convertedRows = await Task.WhenAll(lines.Skip(1).Select(ExtractObjectsAsync));

            if (convertedRows.Length == 0)
            {
                _logger.LogCritical("No rows converted");
                return new ParseResults<T>(Array.Empty<T>(), new DataRow[0]);
            }

            foreach (var row in convertedRows)
                dt.Rows.Add(row);

            var (rowsParsed, rowsFailed) = await ParseRowsAsync<T>(dt.Rows.Cast<DataRow>(), progress);
            var parsed = rowsParsed;
            var failed = rowsFailed;

            return new ParseResults<T>(parsed, failed);
        }

        private Task<object[]> ExtractObjectsAsync(string line)
            => Task.Run(() => SplitLine(line, _seperator).ToArray<object>());

        private async Task<ParseResults<T>> ParseRowsAsync<T>
            (IEnumerable<DataRow> rows, IProgress<ParserProgress> progress = null)
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

            foreach (var (name, property) in Properties)
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
            var possibleIndexes = line.Split(seperator).Length;
            var elements = new string[possibleIndexes];
            var lastSetIndex = 0;
            var startReadingFromIndex = 0;
            var insideString = false;

            void AddToElements(string subString)
            {
                elements[lastSetIndex] = RemoveDoubleQuotes(subString);
                lastSetIndex++;
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
                    AddToElements(
                        line[startReadingFromIndex] == seperator
                            ? ""
                            : line.Substring(startReadingFromIndex, i - startReadingFromIndex));

                    startReadingFromIndex = i + 1;
                }
            }

            return elements.Where(e => e != null).ToArray();
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