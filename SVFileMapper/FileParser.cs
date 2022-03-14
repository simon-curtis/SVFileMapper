using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
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
        private readonly char _separator;

        public FileParser()
        {
            _separator = ',';
            _options = new ParserOptions();
            _logger = new FakeLogger();
        }

        public FileParser(Action<ParserOptions> options)
        {
            _options = new ParserOptions();
            options.Invoke(_options);

            _logger = _options.Logger ?? new FakeLogger();
            _separator = _options.SeparatingCharacter switch
            {
                Separator.Comma => ',',
                Separator.Pipe => '|',
                Separator.Tab => '\t',
                _ => throw new ArgumentException(
                    nameof(_options.SeparatingCharacter), $"Separator not supported: {_options.SeparatingCharacter}")
            };
        }

        private ICollection<(string name, PropertyInfo property)> Properties { get; set; }
            = new List<(string name, PropertyInfo property)>();

        public async Task<ParseResults<T>> ParseFileAsync
            (string filePath, IProgress<ParserProgress>? progress = null)
        {
            if (_logger is not FakeLogger)
            {
                _logger.LogCritical("WARNING: You've set a Progress indicator on this class, this will severely slow " +
                                    "down the code as it forces synchronous behavior");
            }

            Properties = typeof(T).GetProperties()
                .Where(p => p.GetCustomAttribute<ColumnIgnored>() == null)
                .Select(p => (p.GetCustomAttribute<ColumnName>()?.Name ?? p.Name, p))
                .ToList();

            var dt = new DataTable();

            using var reader = new StreamReader(filePath);

            var processedHeaders = false;
            while (await reader.ReadLineAsync() is { } line)
            {
                if (_options.HasHeaders && !processedHeaders)
                {
                    var columns = FileParserTools.SplitLine(line, _separator)
                        .Select(header => new DataColumn(header))
                        .ToArray();

                    dt.Columns.AddRange(columns);
                    processedHeaders = true;
                    continue;
                }

                var dr = FileParserTools.SplitLine(line, _separator).ToArray<object>();
                dt.Rows.Add(dr);
            }

            return dt.Rows.Count is 0
                ? new ParseResults<T>(ImmutableArray<T>.Empty, ImmutableArray<UnmatchedResult>.Empty)
                : await ParseRows(dt, progress);
        }

        private async Task<ParseResults<T>> ParseRows(DataTable data, IProgress<ParserProgress>? progress = null)
        {
            var count = 0;
            var max = data.Rows.Count;

            var results = new ConcurrentDictionary<int, CastResult<T>>();

            await Parallel.ForEachAsync(
                data.Rows.Cast<DataRow>().Select((r, i) => (r, i)), 
                CancellationToken.None, 
                (item, _) =>
                {
                    var (row, index) = item;

                    progress?.Report(new ParserProgress(++count, max));

                    CastResult<T> result;
                    try
                    {
                        var (parsedObject, failedColumns) = ParseRow(row);
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

                    while (!results.TryAdd(index, result)) { }
                    return ValueTask.CompletedTask;
                });
            
            var (satisfied, falsified) = results
                .OrderBy(_ => _.Key)
                .Select(_ => _.Value)
                .Partition(result => result.Success);

            var succeeded = satisfied
                .Select(_ => _.ParsedObject)
                .ToImmutableArray();

            var failed = falsified
                .Select(u => new UnmatchedResult
                {
                    Row = u.Row,
                    FailedColumnConversions = u.FailedColumnConversions ?? ArraySegment<FailedColumnConversion>.Empty
                })
                .ToImmutableArray();

            return new ParseResults<T>(succeeded, failed);
        }

        private (T ParsedObject, IEnumerable<FailedColumnConversion> FailedColumnConversions) ParseRow(DataRow row)
        {
            var obj = Activator.CreateInstance<T>();
            var failures = new List<FailedColumnConversion>();

            var columns = row.Table.Columns
                .Cast<DataColumn>()
                .ToDictionary(_ => _.ColumnName.Trim().ToLower());
            
            foreach (var (name, property) in Properties)
                try
                {
                    if (!columns.TryGetValue(name.ToLower(), out var column))
                        continue;

                    if (row[column].ToString()?.Trim() is not {} value) 
                        continue;

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

                        if (_options.AdditionalBooleanValues.ContainsKey(value))
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
                        Column = row.Table.Columns[name]!,
                        Reason = ex.Message
                    });
                }

            return (obj, failures);
        }
    }

    public static class FileParserTools
    {
        public static ImmutableArray<string> SplitLine(string line, char separator)
        {
            Span<char> lineSpan = line.ToArray();
            var result = new List<string>();

            var insideString = false;
            var escapeChar = false;
            var readFromIndex = 0;
            for (var i = 0; i < lineSpan.Length; i++)
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

                if (lineSpan[i] == separator && !insideString && !escapeChar)
                {
                    result.Add(RemoveDoubleQuotes(lineSpan[readFromIndex..i]));
                    readFromIndex = i + 1;
                }

                if (i + 1 == lineSpan.Length)
                {
                    result.Add(RemoveDoubleQuotes(lineSpan[readFromIndex..(i + 1)]));
                    break;
                }

                escapeChar = false;
            }

            return result.ToImmutableArray();
        }

        private static string RemoveDoubleQuotes(ReadOnlySpan<char> part)
        {
            if (part is { Length: 0 }) return string.Empty;
            if (part[0] == '"') part = part[1..];
            if (part[^1] == '"') part = part[..^1];
            return part.ToString().Replace("\"\"", "\"");
        }
    }
}