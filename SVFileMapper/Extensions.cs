using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SVFileMapper.Extensions
{
    public sealed record ParseResults<T>(IEnumerable<T> Matched, IEnumerable<DataRow> UnmatchedLines);
    public sealed record CastResult<T> (bool Success, T ParsedObject, DataRow Row);

    internal static class Extensions
    {
        public static async Task<(IEnumerable<T> Parsed, IEnumerable<DataRow> Failed)> ParseRowsAsync<T>
            (this IEnumerable<DataRow> rows)
        {
            var tasks = rows
                .Select(async row => await row.ParseAsync<T>())
                .ToList();

            var results = await Task.WhenAll(tasks);

            var (matched, unmatched) = results.Match(result => result.Success);
            var parsed = matched.Select(m => m.ParsedObject);
            var failed = unmatched.Select(u => u.Row);
            return (parsed, failed);
        }

        public static Task<CastResult<T>> ParseAsync<T>(this DataRow row)
        {
            var obj = Activator.CreateInstance<T>();

            try
            {
                foreach (var property in obj!.GetType().GetProperties())
                {
                    var customColumnName = property.GetCustomAttribute<ColumnAttribute>();
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

        public static (IEnumerable<T> Matched, IEnumerable<T> Unmatched) Match<T>
            (this IEnumerable<T> objects, Predicate<T> filter)
        {
            var matched = new List<T>();
            var unmatched = new List<T>();
            foreach (var obj in objects)
            {
                if (filter(obj))
                    matched.Add(obj);
                else
                    unmatched.Add(obj);
            }

            return (matched, unmatched);
        }
    }
}