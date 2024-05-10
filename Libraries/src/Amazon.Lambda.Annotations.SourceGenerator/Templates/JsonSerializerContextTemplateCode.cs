using Amazon.Lambda.Annotations.SourceGenerator.Models;
using System.Collections.Generic;

namespace Amazon.Lambda.Annotations.SourceGenerator.Templates
{
    public partial class JsonSerializerContextTemplate
    {
        private readonly string _containingNamespace;
        private readonly HashSet<string> _serializableTypes;

        public JsonSerializerContextTemplate(string containingNamespace,  HashSet<string> serializableTypes)
        {
            _containingNamespace = containingNamespace;
            _serializableTypes = serializableTypes;
        }
    }
}
