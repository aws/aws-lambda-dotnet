using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// Represents original method model used for generating the code from the template.
    /// </summary>
    public class LambdaMethodModel
    {
        private AttributeModel<LambdaFunctionAttribute> _lambdaFunctionAttribute;

        /// <summary>
        /// Returns true if original method is an async method
        /// </summary>
        public bool IsAsync { get; set; }

        /// <summary>
        /// Returns true if original method returns void or <see cref="System.Threading.Tasks.Task"/>
        /// </summary>
        public bool ReturnsVoidOrTask { get; set; }

        /// <summary>
        /// Gets or sets the return type of the method.
        /// </summary>
        public TypeModel ReturnType { get; set; }

        /// <summary>
        /// Gets or sets the parameters of original method. If this method has no parameters, returns
        /// an empty list.
        /// </summary>
        public IList<ParameterModel> Parameters { get; set; }

        /// <summary>
        /// Gets or sets name of the original method.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Returns true if original method uses dependency injection.
        /// </summary>
        public bool UsingDependencyInjection { get; set; }

        /// <summary>
        /// Gets or sets the namespace for the nearest enclosing namespace. Returns null if the
        /// symbol isn't contained in a namespace.
        /// </summary>
        public string ContainingNamespace { get; set; }

        /// <summary>
        /// Gets or sets type of Lambda event
        /// </summary>
        public List<EventType> Events { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="TypeModel"/> for the containing type. Returns null if the
        /// symbol is not contained within a type.
        /// </summary>
        public TypeModel ContainingType { get; set; }

        /// <summary>
        /// Gets or sets the attributes of original method. There always exist <see cref="Annotations.LambdaFunctionAttribute"/> in the list.
        /// </summary>
        public IList<AttributeModel> Attributes { get; set; }

        /// <summary>
        /// Gets <see cref="Annotations.LambdaFunctionAttribute"/> attribute.
        /// </summary>
        public AttributeModel<LambdaFunctionAttribute> LambdaFunctionAttribute
        {
            get
            {
                if (_lambdaFunctionAttribute == null)
                {
                    var model = Attributes.First(att => att.Type.FullName == TypeFullNames.LambdaFunctionAttribute);
                    if (model is AttributeModel<LambdaFunctionAttribute> lambdaFunctionAttributeModel)
                    {
                        _lambdaFunctionAttribute = lambdaFunctionAttributeModel;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Lambda method must has a {TypeFullNames.LambdaFunctionAttribute} attribute");
                    }
                }

                return _lambdaFunctionAttribute;
            }
        }
    }
}