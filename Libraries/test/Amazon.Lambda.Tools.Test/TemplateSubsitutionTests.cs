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
                {"$.Resources.WorkFlow.Properties.DefinitionString", "state-machine.json" }
            };

            var newTemplateBody = Utilities.ProcessTemplateSubstitions(templateBody, substitutions, fullPath);

            var root = JsonConvert.DeserializeObject(newTemplateBody) as JObject;
            var value = root.SelectToken("$.Resources.WorkFlow.Properties.DefinitionString") as JValue;
            Assert.NotNull(value);
            Assert.Equal(value.Type, JTokenType.String);

            var stateMachineContent = File.ReadAllText(Path.Combine(fullPath, "state-machine.json"));
            Assert.Equal(stateMachineContent, value.Value);
        }
    }
}
