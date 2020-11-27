using System.Collections.Generic;
using System.Linq;
using Xunit;
using SVFileMapper;

namespace SVFileMapperTests
{
    public class SvfMapperTests
    {
        [Fact]
        public void SplitString_Comma_Seperation_Test()
        {
            var expected = new List<string>
            {
                "test",
                "string"
            };
            
            const string line = "test,string";
            var result = FileParser.SplitLine(line, ',');
            
            Assert.Equal(expected, result);
        }
        
        [Fact]
        public void SplitString_Pipe_Seperation_Test()
        {
            var expected = new List<string>
            {
                "test",
                "string"
            };
            
            const string line = "test|string";
            var result = FileParser.SplitLine(line, '|');
            
            Assert.Equal(expected, result);
        }
        
        [Fact]
        public void SplitString_Tab_Seperation_Test()
        {
            var expected = new List<string>
            {
                "test",
                "string"
            };
            
            const string line = "test	string";
            var result = FileParser.SplitLine(line, '\t');
            
            Assert.Equal(expected, result);
        }

        [Fact]
        public void SplitLine_Handles_One_Column()
        {
            var shouldEqual = new List<string>
            {
                "a simple test to test line splitting"
            };

            const string testString = "a simple test to test line splitting";
            var result = FileParser.SplitLine(testString, ',');
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
            var result = FileParser.SplitLine(testString, ',');
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
            var result = FileParser.SplitLine(testString, ',');
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
            var result = FileParser.SplitLine(testString, ',');
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
            var result = FileParser.SplitLine(testString, ',');
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
            var result = FileParser.SplitLine(testString, ',');
            Assert.Equal(shouldEqual, result);
        }

        [Fact]
        public void SplitLine_Handles_Long_String()
        {
            const string line =
                "Employee ID|Legal First Name|Legal Surname|Work Email|Date of Birth|Work Location|Sub Division 2|" +
                "Sub Division 3|Sub Division 4|Sub Division 5|Sub Division 6|Sub Division 7|Sub Division 8|" +
                "Workspace (Phase / Floor)|Organisation|Job Code|Business Title|Is People Manager|Manager ID|" +
                "Manager Full Legal Name|Manager Email|Managers Organisation|Secondary Manager ID|" +
                "Secondary Manager Full Name|Secondary Manager Email|Secondary Manager Organisation|HS Representative|" +
                "Is Worker an Expectant Mother|Return from PHI|Change of workspace|Is worker a Commercial driver|" +
                "Worker claimed mileage in previous 12 months|Is worker on leave|Return from Maternity|Worker Type|" +
                "Is employee eligible for DSE|AS Site Manager|AS Regional Manager|AS Apprentice|NI Number|Home Postcode|" +
                "ARC Site Inspection Author|Is Homeworker|Homeworker Home Address Change";

            var attempt = FileParser.SplitLine(line, '|');
            Assert.Equal("Homeworker Home Address Change", attempt.Last());
        }

        [Fact]
        public void SplitString_Handles_Empty_Cell()
        {
            var expected = new List<string>
            {
                "test",
                "",
                "string"
            };
            const string line = "test,,string";
            var result = FileParser.SplitLine(line, ',');
            
            Assert.Equal(expected, result);
        }
    }
}