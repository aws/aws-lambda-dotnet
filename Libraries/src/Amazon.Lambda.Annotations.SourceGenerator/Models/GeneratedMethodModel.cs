using System.Collections.Generic;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// Represents generated method model used for generating the code from the template.
    /// </summary>
    public class GeneratedMethodModel
    {
        /// <summary>
        /// Gets or sets type of response returned by the generated Lambda function.
        /// </summary>
        public TypeModel ResponseType { get; set; }

        /// <summary>
        /// Gets or sets type of request accepted by the generated Lambda function.
        /// </summary>
        public TypeModel RequestType { get; set; }

        /// <summary>
        /// Gets or sets list of namespaces required to facilitate generated statements.
        /// </summary>
        public IList<string> Usings { get; set; }

        /// <summary>
        /// Gets or sets containing type of the generated Lambda function.
        /// </summary>
        public TypeModel ContainingType { get; set; }
    }
}