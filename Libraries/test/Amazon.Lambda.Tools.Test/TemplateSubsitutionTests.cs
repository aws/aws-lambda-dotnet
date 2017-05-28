using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

namespace Amazon.Lambda.Tools.Test
{
    public class TemplateSubsitutionTests
    {
        private string GetTestProjectPath(string project)
        {
            var assembly = this.GetType().GetTypeInfo().Assembly;
            var fullPath = Path.GetFullPath(Path.GetDirectoryName(assembly.Location) + "../../../../../TemplateSubstitutionTestProjects/" + project);
            return fullPath;
        }


        [Fact]
        public void SubstituteStateMachine()
        {
            var fullPath = GetTestProjectPath("StateMachineDefinitionStringTest");
            var templateBody = File.ReadAllText(Path.Combine(fullPath, "serverless.template"));
            var substitutions = new Dictionary<string, string>
            {
                {"$.Resources.WorkFlow.Properties.DefinitionString.Fn::Sub", "state-machine.json" }
            };

            var newTemplateBody = Utilities.ProcessTemplateSubstitions(null, templateBody, substitutions, fullPath);

            var root = JsonConvert.DeserializeObject(newTemplateBody) as JObject;
            var value = root.SelectToken("$.Resources.WorkFlow.Properties.DefinitionString.Fn::Sub") as JValue;
            Assert.NotNull(value);
            Assert.Equal(value.Type, JTokenType.String);

            var stateMachineContent = File.ReadAllText(Path.Combine(fullPath, "state-machine.json"));
            Assert.Equal(stateMachineContent, value.Value);
        }

        [Fact]
        public void SubstituteObject()
        {
            var templateBody = "{'Name':'test', 'Data' : {'MyObj': {}}}";
            var swapData = "{'Foo' : 'Bar'}";

            var substitutions = new Dictionary<string, string>
            {
                {"$.Data.MyObj", swapData }
            };

            var newTemplateBody = Utilities.ProcessTemplateSubstitions(null, templateBody, substitutions, null);

            var root = JsonConvert.DeserializeObject(newTemplateBody) as JObject;
            var value = root.SelectToken("$.Data.MyObj") as JObject;

            Assert.Equal("Bar", value["Foo"].ToString());
        }


        [Fact]
        public void SubstituteArray()
        {
            var templateBody = "{'Name':'test', 'Data' : {'MyArray': []}}";
            var swapData = "['Foo', 'Bar']";

            var substitutions = new Dictionary<string, string>
            {
                {"$.Data.MyArray", swapData }
            };

            var newTemplateBody = Utilities.ProcessTemplateSubstitions(null, templateBody, substitutions, null);

            var root = JsonConvert.DeserializeObject(newTemplateBody) as JObject;
            var value = root.SelectToken("$.Data.MyArray") as JArray;

            Assert.Equal(2, value.Count);
            Assert.Equal("Foo", value[0]);
            Assert.Equal("Bar", value[1]);
        }

        [Fact]
        public void SubstituteBool()
        {
            var templateBody = "{'Name':'test', 'Data' : {'MyBool': false}}";
            var swapData = "true";

            var substitutions = new Dictionary<string, string>
            {
                {"$.Data.MyBool", swapData }
            };

            var newTemplateBody = Utilities.ProcessTemplateSubstitions(null, templateBody, substitutions, null);

            var root = JsonConvert.DeserializeObject(newTemplateBody) as JObject;
            var value = root.SelectToken("$.Data.MyBool") as JValue;

            Assert.Equal(true, value.Value);
        }

        [Fact]
        public void SubstituteInt()
        {
            var templateBody = "{'Name':'test', 'Data' : {'MyInt': 0}}";
            var swapData = "100";

            var substitutions = new Dictionary<string, string>
            {
                {"$.Data.MyInt", swapData }
            };

            var newTemplateBody = Utilities.ProcessTemplateSubstitions(null, templateBody, substitutions, null);

            var root = JsonConvert.DeserializeObject(newTemplateBody) as JObject;
            var value = root.SelectToken("$.Data.MyInt") as JValue;

            Assert.Equal(100L, value.Value);
        }

        [Fact]
        public void SubstituteNullString()
        {
            var templateBody = "{'Name':'test', 'Data' : {'MyString': null}}";
            var swapData = "100";

            var substitutions = new Dictionary<string, string>
            {
                {"$.Data.MyString", swapData }
            };

            Assert.Throws<LambdaToolsException>(() => Utilities.ProcessTemplateSubstitions(null, templateBody, substitutions, null));
        }
    }
}
