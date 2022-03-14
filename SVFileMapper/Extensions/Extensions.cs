using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;

namespace SVFileMapper.Extensions
{
    public readonly record struct FailedColumnConversion(DataColumn? Column, string? Reason);
    public readonly record struct UnmatchedResult(DataRow? Row, IEnumerable<FailedColumnConversion> FailedColumnConversions);
    public readonly record struct ParseResults<T>(ImmutableArray<T> Matched, ImmutableArray<UnmatchedResult> Unmatched);
    public readonly record struct CastResult<T>(bool Success, T ParsedObject, DataRow Row,
        IEnumerable<FailedColumnConversion>? FailedColumnConversions = null, string FailureReason = "");

    internal static class Extensions
    {
        public static (ImmutableArray<TSource> Satisfied, ImmutableArray<TSource> Falsified) Partition<TSource>(
            this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            var satisfied = new List<TSource>();
            var falsified = new List<TSource>();
            foreach (var item in source) if (predicate(item)) satisfied.Add(item); else falsified.Add(item);
            return (satisfied.ToImmutableArray(), falsified.ToImmutableArray());
        }
    }
}