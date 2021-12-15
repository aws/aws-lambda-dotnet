using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Amazon.Lambda.RuntimeSupport
{
    internal class LambdaXRayExceptionWriter
    {
        private const int INDENT_SIZE = 2;
        private const string EXCEPTION = "exceptions";
        private const string WORKING_DIR = "working_directory";
        private const string PATHS = "paths";
        private const string ERROR_MESSAGE = "message";
        private const string ERROR_TYPE = "type";
        private const string STACK_TRACE = "stack";
        private const string STACK_FRAME_METHOD = "label";
        private const string STACK_FRAME_FILE = "path";
        private const string STACK_FRAME_LINE = "line";


        public static string WriteJson(ExceptionInfo ex)
        {
            string workingDir = JsonExceptionWriterHelpers.EscapeStringForJson(System.IO.Directory.GetCurrentDirectory());
            string exceptionTxt = CreateExceptionJson(ex, 1);

            string workingDirJson = TabString($"\"{WORKING_DIR}\": \"{workingDir}\"", 1);
            string exceptionJson = TabString($"\"{EXCEPTION}\": [ {exceptionTxt} ]", 1);

            var paths = new string[0];
            // Build the paths list by getting all the unique file names in the stack trace elements
            if (ex.StackFrames != null)
            {
                paths = (
                    from sf in ex.StackFrames
                    where sf.Path != null
                    select "\"" + JsonExceptionWriterHelpers.EscapeStringForJson(sf.Path) + "\""
                ).Distinct().ToArray();
            }

            StringBuilder pathsBuilder = new StringBuilder();
            pathsBuilder.Append(TabString($"\"{PATHS}\": ", 1));
            pathsBuilder.Append(CombinePartsIntoJsonObject(2, '[', ']', paths));
            string pathsJson = pathsBuilder.ToString();

            // Add each non-null element to the json elements list
            string[] jsonElements = GetNonNullElements(workingDirJson, exceptionJson, pathsJson);
            return CombinePartsIntoJsonObject(1, '{', '}', jsonElements.ToArray());
        }

        private static string CreateExceptionJson(ExceptionInfo ex, int tab)
        {

            // Grab the elements we want to capture
            string message = JsonExceptionWriterHelpers.EscapeStringForJson(ex.ErrorMessage);
            string type = JsonExceptionWriterHelpers.EscapeStringForJson(ex.ErrorType);
            var stackTrace = ex.StackFrames;

            // Create the JSON lines for each non-null element
            string messageJson = null;
            if (message != null)
            {
                // Trim important for Aggregate Exceptions, whose
                // message contains multiple lines by default
                messageJson = TabString($"\"{ERROR_MESSAGE}\": \"{message}\"", tab + 1);
            }

            string typeJson = TabString($"\"{ERROR_TYPE}\": \"{type}\"", tab + 1);
            string stackTraceJson = GetStackTraceJson(stackTrace, tab + 1);

            // Add each non-null element to the json elements list
            string[] jsonElements = GetNonNullElements(typeJson, messageJson, stackTraceJson);
            return CombinePartsIntoJsonObject(tab + 1, '{', '}', jsonElements);
        }

        // Craft the JSON element (ex: "stack": [ {...}, {...} ]) for the stack trace
        private static string GetStackTraceJson(StackFrameInfo[] stackTrace, int tab)
        {
            // Null stack trace means the entire stack trace json should be null, and therefore not included
            if (stackTrace == null)
            {
                return null;
            }

            // Convert each ExceptionStackFrameResponse object to string using CreateStackFrameJson
            string[] stackTraceElements = (
                    from frame in stackTrace
                    where frame != null
                    select CreateStackFrameJson(frame, tab + 1)
                ).ToArray();

            // If there aren't any frames, return null and therefore don't include the stack trace
            if (stackTraceElements.Length == 0)
            {
                return null;
            }

            // Create JSON property name and create the JSON array holding each element
            StringBuilder stackTraceBuilder = new StringBuilder();
            stackTraceBuilder.Append(TabString($"\"{STACK_TRACE}\": ", tab));
            stackTraceBuilder.Append(CombinePartsIntoJsonObject(tab + 1, '[', ']', stackTraceElements));
            return stackTraceBuilder.ToString();
        }

        // Craft JSON object {...} for a particular stack frame
        private static string CreateStackFrameJson(StackFrameInfo stackFrame, int tab)
        {
            string file = JsonExceptionWriterHelpers.EscapeStringForJson(stackFrame.Path);
            string label = JsonExceptionWriterHelpers.EscapeStringForJson(stackFrame.Label);
            int line = stackFrame.Line;

            string fileJson = null;
            string lineJson = null;
            if (file != null)
            {
                fileJson = TabString($"\"{STACK_FRAME_FILE}\": \"{file}\"", tab);
                lineJson = TabString($"\"{STACK_FRAME_LINE}\": {line}", tab);
            }

            string labelJson = null;
            if (label != null)
            {
                labelJson = TabString($"\"{STACK_FRAME_METHOD}\": \"{label}\"", tab);
            }

            string[] jsonElements = GetNonNullElements(fileJson, labelJson, lineJson);
            return CombinePartsIntoJsonObject(tab, '{', '}', jsonElements);
        }

        private static string TabString(string str, int tabDepth)
        {
            if (tabDepth == 0) return str;

            StringBuilder stringBuilder = new StringBuilder();
            for (int x = 0; x < tabDepth * INDENT_SIZE; x++)
            {
                stringBuilder.Append(" ");
            }
            stringBuilder.Append(str);

            return stringBuilder.ToString();
        }

        private static string CombinePartsIntoJsonObject(int tab, char openChar, char closeChar, params string[] parts)
        {
            string jsonBody = string.Join(",", parts);

            StringBuilder jsonBuilder = new StringBuilder();
            jsonBuilder.Append(TabString(openChar.ToString(), tab));
            jsonBuilder.Append(jsonBody);
            jsonBuilder.Append(TabString(closeChar.ToString(), tab));
            return jsonBuilder.ToString();
        }

        private static string[] GetNonNullElements(params string[] elements)
        {
            return (from x in elements where x != null select x).ToArray();
        }
    }
}
