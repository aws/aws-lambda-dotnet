using System.Collections.Generic;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// Represents parameters of the original method used for generating the code from the template.
    /// </summary>
    public class ParameterModel
    {
        /// <summary>
        /// Gets or sets type of the parameter.
        /// </summary>
        public TypeModel Type { get; set; }

        /// <summary>
        /// Gets or sets the display name of the parameter.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the attributes of parameter. If this parameter has no attributes, returns
        /// an empty list.
        /// </summary>
        public IList<AttributeModel> Attributes { get; set; } = new List<AttributeModel>();
    }
}