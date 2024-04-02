using Amazon.Lambda.RuntimeSupport.Helpers.Logging;

using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{

    public class MessagePropertyTests
    {
        [Fact]
        public void SimpleName()
        {
            var property = new MessageProperty("name");
            Assert.Equal(MessageProperty.Directive.Default, property.FormatDirective);
            Assert.Equal("name", property.Name);
            Assert.Equal("{name}", property.MessageToken);
            Assert.Null(property.FormatArgument);
        }

        [Fact]
        public void WithFormatArgument()
        {
            var property = new MessageProperty("count:000");
            Assert.Equal(MessageProperty.Directive.Default, property.FormatDirective);
            Assert.Equal("count", property.Name);
            Assert.Equal("{count:000}", property.MessageToken);
            Assert.Equal("000", property.FormatArgument);
        }

        [Fact]
        public void WithJsonDirective()
        {
            var property = new MessageProperty("@user");
            Assert.Equal(MessageProperty.Directive.JsonSerialization, property.FormatDirective);
            Assert.Equal("user", property.Name);
            Assert.Equal("{@user}", property.MessageToken);
            Assert.Null(property.FormatArgument);
        }

        [Fact]
        public void WithJsonDirectiveAndIgnorableFormatArgument()
        {
            var property = new MessageProperty("@user:000");
            Assert.Equal(MessageProperty.Directive.JsonSerialization, property.FormatDirective);
            Assert.Equal("user", property.Name);
            Assert.Equal("{@user:000}", property.MessageToken);
            Assert.Equal("000", property.FormatArgument);
        }

        [Fact]
        public void WithFormatArgumentMissingValues()
        {
            var property = new MessageProperty("count:");
            Assert.Equal(MessageProperty.Directive.Default, property.FormatDirective);
            Assert.Equal("count", property.Name);
            Assert.Equal("{count:}", property.MessageToken);
            Assert.Null(property.FormatArgument);
        }

        [Fact]
        public void NameWithSpace()
        {
            var property = new MessageProperty(" first last ");
            Assert.Equal(MessageProperty.Directive.Default, property.FormatDirective);
            Assert.Equal("first last", property.Name);
            Assert.Equal("{ first last }", property.MessageToken);
            Assert.Null(property.FormatArgument);
        }

        [Fact]
        public void NameAndFormatArgumentWithSpace()
        {
            var property = new MessageProperty(" first last : 000 ");
            Assert.Equal(MessageProperty.Directive.Default, property.FormatDirective);
            Assert.Equal("first last", property.Name);
            Assert.Equal("{ first last : 000 }", property.MessageToken);
            Assert.Equal("000", property.FormatArgument);
        }

        [Fact]
        public void ApplyFormat()
        {
            var property = new MessageProperty("count:000");
            var formattedValue = MessageProperty.ApplyFormatArgument(10, property.FormatArgument);
            Assert.Equal("010", formattedValue);
        }

        [Fact]
        public void ApplyFormatTriggersFormatException()
        {
            var property = new MessageProperty("count:0{0");

            // Since a FormatException was thrown the code should fall back to "ToString()" on the value
            var formattedValue = MessageProperty.ApplyFormatArgument(10, property.FormatArgument);
            Assert.Equal("10", formattedValue);
        }
    }
}
