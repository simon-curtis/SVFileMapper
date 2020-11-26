using System.Collections.Generic;
using Xunit;
using SVFileMapper;
using System;

namespace SvfMapperTests
{
    public class SvfMapperTests
    {
        [Fact]
        public void RemoveDoubleQuotes_Properly_Escapes_String()
        {
            var result = SVFileParser.RemoveDoubleQuotes("\"this is a \"\" test\"");
            Assert.Equal("this is a \" test", result);
        }

        [Fact]
        public void SplitLine_Handles_One_Column()
        {
            var shouldEqual = new List<string>
            {
                "a simple test to test line splitting"
            };

            const string testString = "a simple test to test line splitting";
            var result = SVFileParser.SplitLine(testString, ',');
            Assert.Equal(shouldEqual, result);
        }

        [Fact]
        public void SplitLine_Handles_Multiple_Lines()
        {
            var shouldEqual = new List<string>
            {
                "a simple test",
                "to test line splitting",
            };

            const string testString = "a simple test,to test line splitting";
            var result = SVFileParser.SplitLine(testString, ',');
            Assert.Equal(shouldEqual, result);
        }

        [Fact]
        public void SplitLine_Removes_Double_Quotes_From_Around_Value()
        {
            var shouldEqual = new List<string>
            {
                "a simple test",
                "to test line splitting",
            };

            const string testString = "\"a simple test\",to test line splitting";
            var result = SVFileParser.SplitLine(testString, ',');
            Assert.Equal(shouldEqual, result);
        }

        [Fact]
        public void SplitLine_Handles_Double_Quotes_With_Seperator_Inside()
        {
            const char seperator = ',';

            var shouldEqual = new List<string>
            {
                "a simple test",
                $"to test line {seperator} splitting",
            };

            var testString = $"a simple test,\"to test line {seperator} splitting\"";
            var result = SVFileParser.SplitLine(testString, ',');
            Assert.Equal(shouldEqual, result);
        }

        [Fact]
        public void SplitLine_Reduces_Escaped_Double_Quotes_Inside_Double_Quotes()
        {
            var shouldEqual = new List<string>
            {
                "a simple test",
                "to test line\" splitting",
            };

            const string testString = "a simple test,\"to test line\"\" splitting\"";
            var result = SVFileParser.SplitLine(testString, ',');
            Assert.Equal(shouldEqual, result);
        }

        [Fact]
        public void SplitLine_Handles_Unescaped_Double_Quotes_Inside_Double_Quotes()
        {
            var shouldEqual = new List<string>
            {
                "a simple test",
                "to test line\" splitting",
            };

            const string testString = "a simple test,\"to test line\" splitting\"";
            var result = SVFileParser.SplitLine(testString, ',');
            Assert.Equal(shouldEqual, result);
        }
    }
}