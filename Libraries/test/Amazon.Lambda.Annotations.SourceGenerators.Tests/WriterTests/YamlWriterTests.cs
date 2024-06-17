using System;
using System.Collections.Generic;
using System.IO;
using Amazon.Lambda.Annotations.SourceGenerator.Writers;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.WriterTests
{
    public class YamlWriterTests
    {
        const string yamlContent = @"
                        Description: !Sub '${AWS::Region}'
                        Person:
                          Name:
                            FirstName: John
                            LastName: Smith
                          Gender: male
                          Age: 32
                          PhoneNumbers:
                            - '123'
                            - '456'
                            - '789'
                        ";
        [Fact]
        public void Exists()
        {
            // ARRANGE
            ITemplateWriter yamlWriter = new YamlWriter();
            yamlWriter.Parse(yamlContent);

            // ACT and ASSERT
            Assert.True(yamlWriter.Exists("Description"));
            Assert.True(yamlWriter.Exists("Person"));
            Assert.True(yamlWriter.Exists("Person.Name"));
            Assert.True(yamlWriter.Exists("Person.Name.LastName"));
            Assert.True(yamlWriter.Exists("Person.Age"));
            Assert.True(yamlWriter.Exists("Person.PhoneNumbers"));

            Assert.False(yamlWriter.Exists("description"));
            Assert.False(yamlWriter.Exists("person"));
            Assert.False(yamlWriter.Exists("Person.FirstName"));
            Assert.False(yamlWriter.Exists("Person.DOB"));
            Assert.False(yamlWriter.Exists("Person.Name.MiddleName"));
            Assert.False(yamlWriter.Exists("Person.Gender.IsMale"));

            Assert.Throws<InvalidDataException>(() => yamlWriter.Exists("Person..Name.FirstName"));
            Assert.Throws<InvalidDataException>(() => yamlWriter.Exists("  "));
            Assert.Throws<InvalidDataException>(() => yamlWriter.Exists("..."));
            Assert.Throws<InvalidDataException>(() => yamlWriter.Exists(""));
        }

        [Fact]
        public void GetToken()
        {
            // ARRANGE
            ITemplateWriter yamlWriter = new YamlWriter();
            yamlWriter.Parse(yamlContent);

            // ACT
            var firstName = yamlWriter.GetToken<string>("Person.Name.FirstName");
            var lastName = yamlWriter.GetToken<string>("Person.Name.LastName");
            var gender = yamlWriter.GetToken<string>("Person.Gender");
            var age = yamlWriter.GetToken<int>("Person.Age");
            var phoneNumbers = yamlWriter.GetToken<List<string>>("Person.PhoneNumbers");

            // ASSERT
            Assert.Equal("John", firstName);
            Assert.Equal("Smith", lastName);
            Assert.Equal("male", gender);
            Assert.Equal(32, age);
            Assert.Equal(new List<string> { "123", "456", "789" }, phoneNumbers);
            Assert.Throws<InvalidOperationException>(() => yamlWriter.GetToken("Person.Weight"));
            Assert.Throws<InvalidOperationException>(() => yamlWriter.GetToken("Person.Name.MiddleName"));
        }

        [Fact]
        public void SetToken()
        {
            // ARRANGE
            ITemplateWriter yamlWriter = new YamlWriter();
            yamlWriter.Parse(yamlContent);

            // ACT
            yamlWriter.SetToken("Person.Name.FirstName", "ABC");
            yamlWriter.SetToken("Person.Name.MiddleName", "Blah");
            yamlWriter.SetToken("Person.Name.LastName", "XYZ");
            yamlWriter.SetToken("Person.Age", 50);
            yamlWriter.SetToken("Person.DOB", new DateTime(2000, 1, 1));
            yamlWriter.SetToken("Person.PhoneNumbers", new List<int> { 1, 2, 3 }, TokenType.List);
            yamlWriter.SetToken("Person.Address", new Dictionary<string, string> { { "City", "AmazingCity" }, { "State", "AmazingState" } }, TokenType.KeyVal);
            yamlWriter.SetToken("Person.IsAlive", true);

            // ASSERT
            Assert.Equal("ABC", yamlWriter.GetToken<string>("Person.Name.FirstName"));
            Assert.Equal("Blah", yamlWriter.GetToken<string>("Person.Name.MiddleName"));
            Assert.Equal("XYZ", yamlWriter.GetToken<string>("Person.Name.LastName"));
            Assert.Equal(50, yamlWriter.GetToken<int>("Person.Age"));
            Assert.Equal(new DateTime(2000, 1, 1), yamlWriter.GetToken<DateTime>("Person.DOB"));
            Assert.Equal(new List<int> { 1, 2, 3 }, yamlWriter.GetToken<List<int>>("Person.PhoneNumbers"));
            Assert.True(yamlWriter.GetToken<bool>("Person.IsAlive"));
            Assert.Equal("AmazingCity", yamlWriter.GetToken<string>("Person.Address.City"));
            Assert.Equal("AmazingState", yamlWriter.GetToken<string>("Person.Address.State"));
            Assert.Throws<InvalidOperationException>(() => yamlWriter.SetToken("Person.PhoneNumbers.Mobile", 10));
            Assert.Throws<InvalidOperationException>(() => yamlWriter.SetToken("Person.Name.FirstName.MiddleName", "PQR"));
        }

        [Fact]
        public void RemoveToken()
        {
            // ARRANGE
            ITemplateWriter yamlWriter = new YamlWriter();
            yamlWriter.Parse(yamlContent);

            // ACT
            yamlWriter.RemoveToken("Description");
            yamlWriter.RemoveToken("Person.Name.LastName");
            yamlWriter.RemoveToken("Person.Age");

            // ASSERT
            Assert.False(yamlWriter.Exists("Description"));
            Assert.False(yamlWriter.Exists("Person.Name.LastName"));
            Assert.False(yamlWriter.Exists("Person.Age"));
            Assert.True(yamlWriter.Exists("Person.Name"));
            Assert.True(yamlWriter.Exists("Person.Name.FirstName"));
        }

        [Fact]
        public void RemoveTokenIfNullOrEmpty()
        {
            var yamlContent = @"
                        Description: !Sub '${AWS::Region}'
                        Person:
                          Name:
                            FirstName: John
                            LastName: Smith
                          Gender: male
                          Age: null
                          Address: 
                        ";

            // ARRANGE
            ITemplateWriter yamlWriter = new YamlWriter();
            yamlWriter.Parse(yamlContent);

            // ACT
            yamlWriter.RemoveTokenIfNullOrEmpty("Person.Age"); // Should be deleted
            yamlWriter.RemoveTokenIfNullOrEmpty("Person.Address"); // Should be deleted

            yamlWriter.RemoveTokenIfNullOrEmpty("Person.Name"); // Should not be deleted
            yamlWriter.RemoveTokenIfNullOrEmpty("Person.Gender"); // Should not be deleted

            // ASSERT
            Assert.False(yamlWriter.Exists("Person.Age"));
            Assert.False(yamlWriter.Exists("Person.Address"));
            Assert.True(yamlWriter.Exists("Person.Name"));
            Assert.Equal("John", yamlWriter.GetToken<string>("Person.Name.FirstName"));
            Assert.Equal("Smith", yamlWriter.GetToken<string>("Person.Name.LastName"));
            Assert.Equal("male", yamlWriter.GetToken<string>("Person.Gender"));
        }

        [Fact]
        public void GetContent()
        {
            // ARRANGE
            ITemplateWriter yamlWriter = new YamlWriter();
            yamlWriter.SetToken("Person.Name.FirstName", "John");
            yamlWriter.SetToken("Person.Name.LastName", "Smith");
            yamlWriter.SetToken("Person.Age", 50);
            yamlWriter.SetToken("Person.PhoneNumbers", new List<int> { 1, 2, 3 }, TokenType.List);
            yamlWriter.SetToken("Person.Address", new Dictionary<string, string> { { "City", "AmazingCity" }, { "State", "AmazingState" } }, TokenType.KeyVal);
            yamlWriter.SetToken("Person.IsAlive", true);
            yamlWriter.SetToken("Person.Aliases", new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { {"Alias", "Johnny" } },
                new Dictionary<string, string> { {"Alias", "Johnny Boy" } }
            }, TokenType.List);

            // ACT
            var actualSnapshot = yamlWriter.GetContent();

            // ASSERT
            var expectedSnapshot = File.ReadAllText(Path.Combine("WriterTests", "snapshot.yaml"));
            actualSnapshot = SanitizeFileContents(actualSnapshot);
            expectedSnapshot = SanitizeFileContents(expectedSnapshot);
            Assert.Equal(expectedSnapshot, actualSnapshot);
        }

        [Fact]
        public void GetValueOrRef()
        {
            // ARRANGE
            ITemplateWriter yamlWriter = new YamlWriter();

            // ACT
            var stringVal = yamlWriter.GetValueOrRef("Hello");
            var refNode = (YamlMappingNode)yamlWriter.GetValueOrRef("@Hello");

            Assert.Equal("Hello", stringVal);
            Assert.Equal("Hello", refNode.Children["Ref"]);
        }

        private string SanitizeFileContents(string content)
        {
            return content.Replace("\r\n", Environment.NewLine)
                .Replace("\n", Environment.NewLine)
                .Replace("\r\r\n", Environment.NewLine)
                .Trim();
        }

        [Fact]
        public void GetKeys()
        {
            // ARRANGE
            const string yamlContent = @"
                      Resources:
                        Function1:
                          Description:
                            Fn::Sub:
                              - '${var1}'
                              - var1: Test
                        Function2:
                          Description: !Sub '${AWS::Region}'
                        ";
            ITemplateWriter yamlWriter = new YamlWriter();
            yamlWriter.Parse(yamlContent);

            // ACT
            var functionNames = yamlWriter.GetKeys("Resources");
            var function1Keys = yamlWriter.GetKeys("Resources.Function1");

            // ASSERT
            Assert.NotEmpty(functionNames);
            Assert.Equal(2, functionNames.Count);
            Assert.Equal("Function1", functionNames[0]);
            Assert.Equal("Function2", functionNames[1]);
            Assert.NotEmpty(function1Keys);
            var description = Assert.Single(function1Keys);
            Assert.Equal("Description", description);
            Assert.Throws<InvalidOperationException>(() => { yamlWriter.GetKeys("Resources.Function2"); });
        }
    }
}