using System;
using System.Collections.Generic;

using System.Reflection;
using System.Reflection.Emit;
using System.Management.Automation;
using System.Globalization;

namespace Amazon.Lambda.PowerShellHost
{
    /// <summary>
    /// Handles converting the errors coming from PowerShell to .NET Exceptions
    /// </summary>
    public class ExceptionManager
    {
        private const string CUSTOM_EXCEPTION_FIELD = "Exception";
        private const string CUSTOM_MESSAGE_FIELD = "Message";

        private IDictionary<string, Type> _customExceptions;

        private Lazy<AssemblyBuilder> _assemblyBuilder;

        private Lazy<ModuleBuilder> _moduleBuilder;

        /// <summary>
        /// Default Constructor
        /// </summary>
        public ExceptionManager()
        {
            _customExceptions = new Dictionary<string, Type>();

            _assemblyBuilder = new Lazy<AssemblyBuilder>(
                () => AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(Guid.NewGuid().ToString()), AssemblyBuilderAccess.Run));
            _moduleBuilder = new Lazy<ModuleBuilder>(
                () => _assemblyBuilder.Value.DefineDynamicModule("CustomExceptions"));
        }


        /// <summary>
        /// Given the exception from the PowerShell host that ran a PowerShell script, this method will determine what type of exception
        /// should be thrown back to Lambda. This can involve creating a dynamic exception if an exception type string is
        /// given from PowerShell.
        /// 
        /// For example if the PS script executes the following command
        /// 
        /// throw @{'Exception'='AccountNotFound';'Message'='The account was not found'}
        /// 
        /// Then this method will create a new .NET exception type called AccountNotFound with a message of "The Acount was not found".
        /// The PS host can then throw this new .NET exception type. If the PowerShell-based Lambda functions is used in AWS StepFunctions 
        /// the state machine can choose to make a choice based on the AccountNotFound error.
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public Exception DetermineExceptionToThrow(Exception e)
        {
            var runtimeException = e as RuntimeException;
            if (runtimeException == null)
                return e;

            if(runtimeException.ErrorRecord?.TargetObject != null)
            {
                string exceptionTypeName = null;
                string exceptionMessage = null;
                if(runtimeException.ErrorRecord.TargetObject is System.Collections.Hashtable fields && fields.ContainsKey(CUSTOM_EXCEPTION_FIELD))
                {
                    exceptionTypeName = fields[CUSTOM_EXCEPTION_FIELD] as string;
                    exceptionMessage = fields[CUSTOM_MESSAGE_FIELD] as string ?? exceptionTypeName;
                }
                else if (runtimeException.ErrorRecord.TargetObject is string str && IsErrorCode(str))
                {
                    exceptionTypeName = str;
                    exceptionMessage = str;
                }

                if (!string.IsNullOrEmpty(exceptionTypeName))
                {
                    var exceptionType = GetCustomExceptionType(exceptionTypeName);
                    var constructor = exceptionType.GetConstructor(new Type[] { typeof(string) });
                    var obj = constructor.Invoke(new object[] { exceptionMessage });
                    var customException = obj as Exception;

                    return customException;
                }
            }
            
            if(runtimeException.InnerException != null)
            {
                return runtimeException.InnerException;
            }

            if(runtimeException.ErrorRecord?.Exception != null)
            {
                return runtimeException.ErrorRecord.Exception;
            }


            return runtimeException;
        }


        /// <summary>
        /// For a given type name dynamically create an exception type using System.Reflection.Emit. The types are
        /// cached so that if the same type name is request again the type is only created once.
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public Type GetCustomExceptionType(string typeName)
        {
            // If the typeName has been request in the past return the already generated type.
            if(_customExceptions.TryGetValue(typeName, out var type))
            {
                return type;
            }

            try
            {
                var baseExceptionType = typeof(Exception);
                var baseConstructor = baseExceptionType.GetConstructor(new Type[] { typeof(string) });

                var tb = _moduleBuilder.Value.DefineType(typeName,
                        TypeAttributes.Public |
                        TypeAttributes.Class |
                        TypeAttributes.AutoClass |
                        TypeAttributes.AnsiClass |
                        TypeAttributes.BeforeFieldInit |
                        TypeAttributes.AutoLayout,
                        baseExceptionType);

                // Create constructor taking in a string parameter that is then passed to the base constructor
                var constructor = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { typeof(string) });
                var ilGenerator = constructor.GetILGenerator();
                ilGenerator.Emit(OpCodes.Ldarg_0); // Adds the "this" argument
                ilGenerator.Emit(OpCodes.Ldarg_1); // Adds the string argument
                ilGenerator.Emit(OpCodes.Call, baseConstructor); // Call the base constructor using the "this" instance taking in the string argument.
                ilGenerator.Emit(OpCodes.Ret);

                var newExceptionType = tb.CreateType();

                // Cache exception type
                _customExceptions[typeName] = newExceptionType;
                return newExceptionType;
            }
            catch(Exception e)
            {
                throw new LambdaPowerShellException($"Error creating customer error type for {typeName}: {e.Message}", e);
            }
        }

        /// <summary>
        /// Check to see if the string coming from a PS throw statement is an error code. That means the string
        /// could be used as a class name for an Exception class.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsErrorCode(string value)
        {
            return IsValidTypeNameOrIdentifier(value, true);
        }


        // Utility methods taken from CSharpHelpers from the corefx repo 
        // https://github.com/dotnet/corefx/blob/a10890f4ffe0fadf090c922578ba0e606ebdd16c/src/Common/src/System/CSharpHelpers.cs
        #region Type name validation utilities
        internal static bool IsValidTypeNameOrIdentifier(string value, bool isTypeName)
        {
            bool nextMustBeStartChar = true;

            if (value.Length == 0)
                return false;

            // each char must be Lu, Ll, Lt, Lm, Lo, Nd, Mn, Mc, Pc
            //
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                switch (uc)
                {
                    case UnicodeCategory.UppercaseLetter:        // Lu
                    case UnicodeCategory.LowercaseLetter:        // Ll
                    case UnicodeCategory.TitlecaseLetter:        // Lt
                    case UnicodeCategory.ModifierLetter:         // Lm
                    case UnicodeCategory.LetterNumber:           // Lm
                    case UnicodeCategory.OtherLetter:            // Lo
                        nextMustBeStartChar = false;
                        break;

                    case UnicodeCategory.NonSpacingMark:         // Mn
                    case UnicodeCategory.SpacingCombiningMark:   // Mc
                    case UnicodeCategory.ConnectorPunctuation:   // Pc
                    case UnicodeCategory.DecimalDigitNumber:     // Nd
                        // Underscore is a valid starting character, even though it is a ConnectorPunctuation.
                        if (nextMustBeStartChar && ch != '_')
                            return false;

                        nextMustBeStartChar = false;
                        break;
                    default:
                        // We only check the special Type chars for type names.
                        if (isTypeName && IsSpecialTypeChar(ch, ref nextMustBeStartChar))
                        {
                            break;
                        }

                        return false;
                }
            }

            return true;
        }

        // This can be a special character like a separator that shows up in a type name
        // This is an odd set of characters.  Some come from characters that are allowed by C++, like < and >.
        // Others are characters that are specified in the type and assembly name grammar.
        internal static bool IsSpecialTypeChar(char ch, ref bool nextMustBeStartChar)
        {
            switch (ch)
            {
                case ':':
                case '.':
                case '$':
                case '+':
                case '<':
                case '>':
                case '-':
                case '[':
                case ']':
                case ',':
                case '&':
                case '*':
                    nextMustBeStartChar = true;
                    return true;

                case '`':
                    return true;
            }
            return false;
        }
        #endregion
    }
}
