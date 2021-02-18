using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace SVFileMapper.Models
{
    public class ParserOptions
    {
        /// <summary>
        ///     The character the lines in the file are seperated by.
        /// </summary>
        /// <options>
        ///     Separator.Comma<br />
        ///     Separator.Pipe<br />
        ///     Separator.Tab
        /// </options>
        /// <default>
        ///     Separator.Comma
        /// </default>
        public Separator SeperatingCharacter { get; set; }

        /// <summary>
        ///     If the target type of the property is boolean, it will check this to check
        ///     the value's truthiness.<br />
        ///     You do not need to add "true" and "false" as these are automatic.
        /// </summary>
        /// <code>
        /// Example:
        /// = new Dictionary&#060;string, bool&#062; { <br />
        ///     { "Yes", true  },<br />
        ///     { "No",  false }<br />
        /// }
        /// </code>
        public Dictionary<string, bool> AdditionalBooleanValues { get; set; } = new();

        /// <summary>
        ///     Use this to attach a logger to output any commentry from the parsing process.<br />
        ///     <b>WARNING: Logging severly slows down the code as it forces synchronous behavior</b>
        /// </summary>
        public ILogger? Logger { get; set; }
    }
}