using Amazon.Lambda.Annotations.SourceGenerator.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YamlDotNet.Core.Tokens;

namespace Amazon.Lambda.Annotations.SourceGenerator.Templates
{
    public partial class LambdaFunctionTemplate
    {
        private readonly LambdaFunctionModel _model;

        public LambdaFunctionTemplate(LambdaFunctionModel model)
        {
            _model = model;
        }

        private List<string> GetClassDocumentation()
        {
            // Provide a class-level XMLDoc summary so the generated handler class does not trigger
            // CS1591 ("Missing XML comment for publicly visible type or member") when the consuming
            // project enables <GenerateDocumentationFile>true</GenerateDocumentationFile>.
            var lambdaMethod = _model.LambdaMethod;
            return new List<string>
            {
                "/// <summary>",
                $"/// The generated Lambda function handler for <see cref=\"{FormatCrefType(lambdaMethod.ContainingType.FullName)}\"/>.",
                "/// </summary>"
            };
        }

        private List<string> GetMethodDocumentation()
        {
            var lambdaMethod = _model.LambdaMethod;
            var generatedMethod = _model.GeneratedMethod;
            var docStringlines = new List<string>
            {
                "/// <summary>"
            };
            if (lambdaMethod.Parameters.Any())
            {
                docStringlines.Add($"/// The generated Lambda function handler for <see cref=\"{lambdaMethod.Name}({string.Join(", ", lambdaMethod.Parameters.Select(p => FormatCrefType(p.Type.FullName)))})\"/>");
            }
            else
            {
                docStringlines.Add($"/// The generated Lambda function handler for <see cref=\"{lambdaMethod.Name}\"/>");
            }
            docStringlines.Add("/// </summary>");

            foreach (var parameter in generatedMethod.Parameters)
            {
                docStringlines.Add($"/// <param name=\"{parameter.Name}\">{parameter.Documentation}</param>");
            }

            docStringlines.Add("/// <returns>Result of the Lambda function execution</returns>");

            return docStringlines;
        }

        private static string FormatCrefType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return typeName;
            }

            return typeName.Replace('<', '{').Replace('>', '}');
        }
    }
}