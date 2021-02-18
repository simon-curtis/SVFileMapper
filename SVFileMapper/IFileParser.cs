using System;
using System.Threading.Tasks;
using SVFileMapper.Extensions;
using SVFileMapper.Models;

namespace SVFileMapper
{
    public interface IFileParser<T>
    {
        /// <summary>
        ///     Reads through each line of a seperated value file (such as) provided in the class constructor and converts them to
        ///     the desired object. This will return both an enumerable of parsed rows and an enumerable rows that failed
        ///     to parse so you can check each row for errors.
        ///     <br /><br />
        ///     ** TSV's are not currently supported
        /// </summary>
        /// <param name="filePath">The full file path of the file you want to read.</param>
        /// <param name="progress">Use this if you need to </param>
        /// <typeparam name="T">The type of object you want to cast each line to.</typeparam>
        /// <returns>An enumerable of parsed and failed lines</returns>
        Task<ParseResults<T>> ParseFileAsync
            (string filePath, IProgress<ParserProgress> progress = null);
    }
}