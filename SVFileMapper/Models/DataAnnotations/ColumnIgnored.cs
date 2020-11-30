using System;

namespace SVFileMapper.Models.DataAnnotations
{
    [AttributeUsage(
        AttributeTargets.Field |
        AttributeTargets.Property,
        AllowMultiple = true)]
    public class ColumnIgnored : Attribute
    {
    }
}