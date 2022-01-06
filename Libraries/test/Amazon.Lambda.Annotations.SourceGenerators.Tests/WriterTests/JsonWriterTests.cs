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
        private const string SampleJsonString = @"{ 'Person': { 'Name': { 'FirstName': 'John', 'LastName': 'Smith', }, 
                'Gender': 'male', 'Age': 32, 'PhoneNumbers': ['123', '456', '789'] } }";

        [Fact]
        public void ExistsTests()
        {
            // ARRANGE
            var rootNode = JObject.Parse(SampleJsonString);
            var jsonWriter = new JsonWriter(rootNode);

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
        public void SetTokenTests()
        {
            // ARRANGE
            var rootNode = new JObject();
            var jsonWriter = new JsonWriter(rootNode);

            // ACT
            jsonWriter.SetToken("Person.Name.FirstName", new JValue("ABC"));
            jsonWriter.SetToken("Person.Name.LastName", new JValue("XYZ"));
            jsonWriter.SetToken("Person.Age", new JValue(50));
            jsonWriter.SetToken("Person.DOB", new JValue(new DateTime(2000, 1, 1)));
            jsonWriter.SetToken("Person.PhoneNumbers", new JArray(new List<string> { "123", "456", "789" }));

            // ASSERT
            var firstName = rootNode["Person"]["Name"]["FirstName"];
            Assert.Equal("ABC", firstName.ToObject<string>());

            var lastName = rootNode["Person"]["Name"]["LastName"];
            Assert.Equal("XYZ", lastName.ToObject<string>());

            var age = rootNode["Person"]["Age"];
            Assert.Equal(50, age.ToObject<int>());

            var dob = rootNode["Person"]["DOB"];
            Assert.True(new DateTime(2000, 1, 1).Equals(dob.ToObject<DateTime>()));

            var phoneNumbers = rootNode["Person"]["PhoneNumbers"];
            Assert.Equal(new List<string> { "123", "456", "789" }, phoneNumbers.ToObject<List<string>>());
        }

        [Fact]
        public void GetTokenTests()
        {
            // ARRANGE
            var rootNode = JObject.Parse(SampleJsonString);
            var jsonWriter = new JsonWriter(rootNode);

            // ACT 
            var firstName = jsonWriter.GetToken("Person.Name.FirstName");
            var lastName = jsonWriter.GetToken("Person.Name.LastName");
            var gender = jsonWriter.GetToken("Person.Gender");
            var age = jsonWriter.GetToken("Person.Age");
            var phoneNumbers = jsonWriter.GetToken("Person.PhoneNumbers");

            // ASSERT
            Assert.Equal("John", firstName.ToObject<string>());
            Assert.Equal("Smith", lastName.ToObject<string>());
            Assert.Equal("male", gender.ToObject<string>());
            Assert.Equal(32, age.ToObject<int>());
            Assert.Equal(new List<string> { "123", "456", "789" }, phoneNumbers.ToObject<List<string>>());
            Assert.Throws<InvalidOperationException>(() => jsonWriter.GetToken("Person.Weight"));
            Assert.Throws<InvalidOperationException>(() => jsonWriter.GetToken("Person.Name.MiddleName"));
        }

        [Fact]
        public void RemoveTokenTests()
        {
            // ARRANGE
            var rootNode = JObject.Parse(SampleJsonString);
            var jsonWriter = new JsonWriter(rootNode);

            // ACT 
            jsonWriter.RemoveToken("Person.Name.LastName");
            jsonWriter.RemoveToken("Person.Name.Age");

            // ASSERT
            Assert.Null(rootNode["Person"]["Name"]["LastName"]);
            Assert.Null(rootNode["Person"]["Name"]["Age"]);
            Assert.NotNull(rootNode["Person"]["Name"]["FirstName"]);
            Assert.NotNull(rootNode["Person"]["Gender"]);
            Assert.NotNull(rootNode["Person"]["PhoneNumbers"]);
        }
    }
}