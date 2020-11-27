using System;

namespace SVFileMapper.Models.DataAnnotations
{
    [AttributeUsage(
         AttributeTargets.Class |
         AttributeTargets.Constructor |
         AttributeTargets.Field |
         AttributeTargets.Method |
         AttributeTargets.Property,
         AllowMultiple = true)]
    public class ColumnName : Attribute
    {
        public string Name { get; }

        public ColumnName(string name)
        {
            Name = name;
        }
    }
}