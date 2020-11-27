using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SVFileMapper.Extensions
{
    public sealed class ParseResults<T>
    {
        public IEnumerable<T> Matched { get; }
        public IEnumerable<DataRow> UnmatchedLines { get; }

        public ParseResults(IEnumerable<T> matched, IEnumerable<DataRow> unmatchedLines)
        {
            Matched = matched;
            UnmatchedLines = unmatchedLines;
        }

        public void Deconstruct(out IEnumerable<T> parsed, out IEnumerable<DataRow> failed)
        {
            parsed = Matched;
            failed = UnmatchedLines;
        }
    }

    public sealed class CastResult<T>
    {
        public bool Success { get; }
        public T ParsedObject { get; }
        public DataRow Row { get; }

        public CastResult(bool success, T parsedObject, DataRow row)
        {
            Success = success;
            ParsedObject = parsedObject;
            Row = row;
        }
    }

    internal static class Extensions
    {
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