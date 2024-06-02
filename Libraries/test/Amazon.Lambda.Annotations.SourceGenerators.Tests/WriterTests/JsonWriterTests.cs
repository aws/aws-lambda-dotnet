using System;
using System.Collections.Generic;
using System.IO;
using Amazon.Lambda.Annotations.SourceGenerator.Writers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.WriterTests
{
    public class JsonWriterTests
    {
        private const string SampleJsonString =
            @"{
               'Person':{
                  'Name':{
                     'FirstName':'John',
                     'LastName':'Smith'
                  },
                  'Gender':'male',
                  'Age':32,
                  'PhoneNumbers':[
                     '123',
                     '456',
                     '789'
                  ]
               }
            }";

        [Fact]
        public void Exists()
        {
            // ARRANGE
            ITemplateWriter jsonWriter = new JsonWriter();
            jsonWriter.Parse(SampleJsonString);

            // ACT and ASSERT
            Assert.True(jsonWriter.Exists("Person.Name.FirstName"));
            Assert.True(jsonWriter.Exists("Person.PhoneNumbers"));
            Assert.True(jsonWriter.Exists("Person.Gender"));
            Assert.False(jsonWriter.Exists("Person.Weight"));
            Assert.False(jsonWriter.Exists("Person.Name.MiddleName"));
            Assert.Throws<InvalidDataException>(() => jsonWriter.Exists("Person..Name.FirstName"));
            Assert.Throws<InvalidDataException>(() => jsonWriter.Exists("  "));
            Assert.Throws<InvalidDataException>(() => jsonWriter.Exists("..."));
            Assert.Throws<InvalidDataException>(() => jsonWriter.Exists(""));
        }

        [Fact]
        public void SetToken()
        {
            // ARRANGE
            ITemplateWriter jsonWriter = new JsonWriter();

            // ACT
            jsonWriter.SetToken("Person.Name.FirstName", "ABC");
            jsonWriter.SetToken("Person.Name.LastName", "XYZ");
            jsonWriter.SetToken("Person.Age", 50);
            jsonWriter.SetToken("Person.DOB", new DateTime(2000, 1, 1));
            jsonWriter.SetToken("Person.PhoneNumbers", new List<string> { "123", "456", "789" }, TokenType.List);

            // ASSERT
            var firstName = jsonWriter.GetToken<string>("Person.Name.FirstName");
            var lastName =  jsonWriter.GetToken<string>("Person.Name.LastName");
            var age = jsonWriter.GetToken<int>("Person.Age");
            var dob = jsonWriter.GetToken<DateTime>("Person.DOB");
            var phoneNumbers = jsonWriter.GetToken<List<string>>("Person.PhoneNumbers");

            Assert.Equal("ABC", firstName);
            Assert.Equal("XYZ", lastName);
            Assert.Equal(50, age);
            Assert.Equal(new DateTime(2000, 1, 1), dob);
            Assert.Equal(new List<string> { "123", "456", "789" }, phoneNumbers);
            Assert.Throws<InvalidOperationException>(() => jsonWriter.SetToken("Person.PhoneNumbers.Mobile", "789"));
            Assert.Throws<InvalidOperationException>(() => jsonWriter.SetToken("Person.Name.FirstName.MiddleName", "PQR"));
        }

        [Fact]
        public void GetToken()
        {
            // ARRANGE
            ITemplateWriter jsonWriter = new JsonWriter();
            jsonWriter.Parse(SampleJsonString);

            // ACT
            var firstName = jsonWriter.GetToken<string>("Person.Name.FirstName");
            var lastName = jsonWriter.GetToken<string>("Person.Name.LastName");
            var gender = jsonWriter.GetToken<string>("Person.Gender");
            var age = jsonWriter.GetToken<int>("Person.Age");
            var phoneNumbers = jsonWriter.GetToken<List<string>>("Person.PhoneNumbers");

            // ASSERT
            Assert.Equal("John", firstName);
            Assert.Equal("Smith", lastName);
            Assert.Equal("male", gender);
            Assert.Equal(32, age);
            Assert.Equal(new List<string> {"123", "456", "789"}, phoneNumbers);
            Assert.Throws<InvalidOperationException>(() => jsonWriter.GetToken("Person.Weight"));
            Assert.Throws<InvalidOperationException>(() => jsonWriter.GetToken("Person.Name.MiddleName"));
        }

        [Fact]
        public void RemoveToken()
        {
            // ARRANGE
            ITemplateWriter jsonWriter = new JsonWriter();
            jsonWriter.Parse(SampleJsonString);

            // ACT
            jsonWriter.RemoveToken("Person.Name.LastName");
            jsonWriter.RemoveToken("Person.Name.Age");

            // ASSERT
            Assert.False(jsonWriter.Exists("Person.Name.LastName"));
            Assert.False(jsonWriter.Exists("Person.Name.Age"));
            Assert.True(jsonWriter.Exists("Person.Name.FirstName"));
            Assert.True(jsonWriter.Exists("Person.Gender"));
            Assert.True(jsonWriter.Exists("Person.PhoneNumbers"));
        }

        [Fact]
        public void GetContent()
        {
            // ARRANGE
            ITemplateWriter jsonWriter = new JsonWriter();
            jsonWriter.SetToken("Person.Name.FirstName", "John");
            jsonWriter.SetToken("Person.Name.LastName", "Smith");
            jsonWriter.SetToken("Person.Age", 50);
            jsonWriter.SetToken("Person.PhoneNumbers", new List<int> { 1, 2, 3 }, TokenType.List);
            jsonWriter.SetToken("Person.Address", new Dictionary<string, string> { { "City", "AmazingCity" }, { "State", "AmazingState" } }, TokenType.KeyVal);
            jsonWriter.SetToken("Person.IsAlive", true);
            jsonWriter.SetToken("Person.Aliases", new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { {"Alias", "Johnny" } },
                new Dictionary<string, string> { {"Alias", "Johnny Boy" } }
            });

            // ACT
            var actualSnapshot = jsonWriter.GetContent();

            // ASSERT
            var expectedSnapshot = File.ReadAllText(Path.Combine("WriterTests", "snapshot.json"));
            actualSnapshot = SanitizeFileContents(actualSnapshot);
            expectedSnapshot = SanitizeFileContents(expectedSnapshot);
            Assert.Equal(expectedSnapshot, actualSnapshot);
        }

        [Fact]
        public void GetValueOrRef()
        {
            // ARRANGE
            ITemplateWriter jsonWriter = new JsonWriter();

            // ACT
            var stringVal = jsonWriter.GetValueOrRef("Hello");
            var refNode = (JObject)jsonWriter.GetValueOrRef("@Hello");

            Assert.Equal("Hello", stringVal);
            Assert.Equal("Hello", refNode["Ref"]);
        }

        private string SanitizeFileContents(string content)
        {
            return content.Replace("\r\n", Environment.NewLine)
                .Replace("\n", Environment.NewLine)
                .Replace("\r\r\n", Environment.NewLine)
                .Trim();
        }
    }
}