using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace SVFileMapper.Extensions
{
    public class FailedColumnConversion
    {
        public DataColumn? Column {get;set;}
        public string? Reason { get; set; } = "";
    }
    
    public class UnmatchedResult
    {
        public DataRow? Row { get; set; }

        public IEnumerable<FailedColumnConversion> FailedColumnConversions { get; set; } =
            Array.Empty<FailedColumnConversion>();
    }

    public sealed class ParseResults<T>
    {
        public IEnumerable<T> Matched { get; }
        public IEnumerable<UnmatchedResult> UnmatchedLines { get; }

        public ParseResults(IEnumerable<T> matched, IEnumerable<UnmatchedResult> unmatchedLines)
        {
            Matched = matched;
            UnmatchedLines = unmatchedLines;
        }

        public void Deconstruct(out IEnumerable<T> parsed, out IEnumerable<UnmatchedResult> failed)
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
        public IEnumerable<FailedColumnConversion> FailedColumnConversions { get; }
        public string FailureReason { get; }

        public CastResult(bool success, T parsedObject, DataRow row,
            IEnumerable<FailedColumnConversion>? failedColumnConversions = null, 
            string failureReason = "")
        {
            Success = success;
            ParsedObject = parsedObject;
            Row = row;
            FailedColumnConversions = failedColumnConversions ?? Array.Empty<FailedColumnConversion>();
            FailureReason = failureReason;
        }
    }

    internal static class Extensions
    {
        public static (IReadOnlyList<TSource> Satisfied, IReadOnlyList<TSource> Falsified) Partition<TSource>(
            this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            var results = source.ToLookup(predicate.Invoke);
            return (results[true].ToList(), results[false].ToList());
        }
    }
}