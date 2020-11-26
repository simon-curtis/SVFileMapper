using System;

namespace SvfMapper.Models
{
    [AttributeUsage(
         AttributeTargets.Class |
         AttributeTargets.Constructor |
         AttributeTargets.Field |
         AttributeTargets.Method |
         AttributeTargets.Property,
         AllowMultiple = true)]
    public class CsvColumn : Attribute
    {
        public string Name { get; }

        public CsvColumn(string name)
        {
            Name = name;
        }
    }
}