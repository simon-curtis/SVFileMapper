using System;

namespace SVFileMapper.Models.DataAnnotations
{
    [AttributeUsage(
        AttributeTargets.Field |
        AttributeTargets.Property,
        AllowMultiple = true)]
    public abstract class ColumnName : Attribute
    {
        protected ColumnName(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}