using System;

namespace SVFileMapper.Models.DataAnnotations
{
    [AttributeUsage(
        AttributeTargets.Field |
        AttributeTargets.Property,
        AllowMultiple = true)]
    public class ColumnName : Attribute
    {
        public ColumnName(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}