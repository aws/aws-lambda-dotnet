using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// Represents a type.
    /// </summary>
    public class TypeModel
    {
        private readonly HashSet<string> _primitiveTypes = new HashSet<string>()
        {
            "bool",
            "char", "string",
            "byte", "sbyte",
            "double", "decimal", "float",
            "short", "int", "long",
            "ushort", "uint", "ulong",
            "System.DateTime"
        };

        /// <summary>
        /// Gets or sets the name of the type.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the full qualified name of the type.
        /// In some cases such as value types (ex. int), FullName will be the alias i.e. int in spite of System.Int32.
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// True if this type is a value type.
        /// </summary>
        public bool IsValueType { get; set; }

        /// <summary>
        /// True if this type or some containing type has type parameters.
        /// </summary>
        public bool IsGenericType { get; set; }

        /// <summary>
        /// Gets or sets the type arguments that have been substituted for the type parameters.
        /// Returns empty when there are not type arguments
        /// </summary>
        public IList<TypeModel> TypeArguments { get; set; }

        /// <summary>
        /// True if the type has the "?" annotation for nullable.
        /// </summary>
        public bool HasNullableAnnotations { get; set; }


        /// <summary>
        /// Gets the full qualified name of the type without annotations.
        /// </summary>
        public string FullNameWithoutAnnotations
        {
            get
            {
                if(!HasNullableAnnotations)
                {
                    return this.FullName;
                }

                return this.FullName.Substring(0, this.FullName.Length - 1);
            }
        }


        /// <summary>
        /// Gets type argument of the <see cref="Task{TResult}"/> type.
        /// If the type is not a generic or <see cref="Task{TResult}"/> type, returns null.
        /// </summary>
        public string TaskTypeArgument
        {
            get
            {
                if (!IsGenericType)
                {
                    return null;
                }

                if (!FullName.StartsWith($"{TypeFullNames.Task}<"))
                {
                    return null;
                }

                return TypeArguments[0].FullName;
            }
        }

        /// <summary>
        /// True, if the type implements IEnumerable interface.
        /// </summary>
        public bool IsEnumerable { get; set; }

        /// <summary>
        /// True if this type is a <see cref="string"/> type.
        /// </summary>
        /// <returns></returns>
        public bool IsString()
        {
            return FullName == "string";
        }

        /// <summary>
        /// True, if the type is a primitive .NET type.
        /// </summary>
        public bool IsPrimitiveType()
        {
            return _primitiveTypes.Contains(FullNameWithoutAnnotations);
        }

        /// <summary>
        /// True, if type model is an enumerable and its argument type is a primitive .NET type.
        /// </summary>
        public bool IsPrimitiveEnumerableType()
        {
            if (!IsEnumerable)
                return false;
            
            if (TypeArguments.Count != 1)
            {
                throw new NotSupportedException("Exactly one type argument is required for enumerables");
            }
            var typeArgument = TypeArguments.First();
            return _primitiveTypes.Contains(typeArgument.FullNameWithoutAnnotations);
        }
    }
}