/*
 * Copyright 2019 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 * 
 *  http://aws.amazon.com/apache2.0
 * 
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Amazon.Lambda.RuntimeSupport
{
    // TODO rewrite using a JSON library
    internal class LambdaJsonExceptionWriter
    {
        private static readonly Encoding TEXT_ENCODING = Encoding.UTF8;
        private const int INDENT_SIZE = 2;
        private const int MAX_PAYLOAD_SIZE = 256 * 1024; // 256KB
        private const string ERROR_MESSAGE = "errorMessage";
        private const string ERROR_TYPE = "errorType";
        private const string STACK_TRACE = "stackTrace";
        private const string INNER_EXCEPTION = "cause";
        private const string INNER_EXCEPTIONS = "causes";
        private const string TRUNCATED_MESSAGE =
            "{\"" + ERROR_MESSAGE + "\": \"Exception exceeded maximum payload size of 256KB.\"}";

        /// <summary>
        /// Write the formatted JSON response for this exception, and all inner exceptions.
        /// </summary>
        /// <param name="ex">The exception response object to serialize.</param>
        /// <returns>The serialized JSON string.</returns>
        public static string WriteJson(ExceptionInfo ex)
        {
            if (ex == null)
                throw new ArgumentNullException("ex");

            MeteredStringBuilder jsonBuilder = new MeteredStringBuilder(TEXT_ENCODING, MAX_PAYLOAD_SIZE);
            string json = AppendJson(ex, 0, false, MAX_PAYLOAD_SIZE - jsonBuilder.SizeInBytes);
            if (json != null && jsonBuilder.HasRoomForString(json))
            {
                jsonBuilder.Append(json);
            }
            else
            {
                jsonBuilder.Append(TRUNCATED_MESSAGE);
            }
            return jsonBuilder.ToString();
        }

        private static string AppendJson(ExceptionInfo ex, int tab, bool appendComma, int remainingRoom)
        {
            if (remainingRoom <= 0)
                return null;

            MeteredStringBuilder jsonBuilder = new MeteredStringBuilder(TEXT_ENCODING, remainingRoom);
            int nextTabDepth = tab + 1;
            int nextNextTabDepth = nextTabDepth + 1;

            List<string> jsonElements = new List<string>();

            // Grab the elements we want to capture
            string message = JsonExceptionWriterHelpers.EscapeStringForJson(ex.ErrorMessage);
            string type = JsonExceptionWriterHelpers.EscapeStringForJson(ex.ErrorType);
            string stackTrace = ex.StackTrace;
            ExceptionInfo innerException = ex.InnerException;
            List<ExceptionInfo> innerExceptions = ex.InnerExceptions;

            // Create the JSON lines for each non-null element
            string messageJson = null;
            if (message != null)
            {
                // Trim important for Aggregate Exceptions, whose
                // message contains multiple lines by default
                messageJson = TabString($"\"{ERROR_MESSAGE}\": \"{message}\"", nextTabDepth);
            }

            string typeJson = TabString($"\"{ERROR_TYPE}\": \"{type}\"", nextTabDepth);
            string stackTraceJson = GetStackTraceJson(stackTrace, nextTabDepth);


            // Add each non-null element to the json elements list
            if (typeJson != null) jsonElements.Add(typeJson);
            if (messageJson != null) jsonElements.Add(messageJson);
            if (stackTraceJson != null) jsonElements.Add(stackTraceJson);

            // Exception JSON body, comma delimited
            string exceptionJsonBody = string.Join("," + Environment.NewLine, jsonElements);

            jsonBuilder.AppendLine(TabString("{", tab));
            jsonBuilder.Append(exceptionJsonBody);

            bool hasInnerException = innerException != null;
            bool hasInnerExceptionList = innerExceptions != null && innerExceptions.Count > 0;

            // Before we close, check for inner exception(s)
            if (hasInnerException)
            {
                // We have to add the inner exception, which means we need
                // another comma after the exception json body
                jsonBuilder.AppendLine(",");

                jsonBuilder.Append(TabString($"\"{INNER_EXCEPTION}\": ", nextTabDepth));

                string innerJson = AppendJson(innerException, nextTabDepth, hasInnerExceptionList, remainingRoom - jsonBuilder.SizeInBytes);
                if (innerJson != null && jsonBuilder.HasRoomForString(innerJson))
                {
                    jsonBuilder.Append(innerJson);
                }
                else
                {
                    jsonBuilder.AppendLine(TRUNCATED_MESSAGE);
                }
            }

            if (hasInnerExceptionList)
            {
                jsonBuilder.Append(TabString($"\"{INNER_EXCEPTIONS}\": [", nextTabDepth));

                for (int i = 0; i < innerExceptions.Count; i++)
                {
                    var isLastOne = i == innerExceptions.Count - 1;
                    var innerException2 = innerExceptions[i];
                    string innerJson = AppendJson(innerException2, nextNextTabDepth, !isLastOne, remainingRoom - jsonBuilder.SizeInBytes);
                    if (innerJson != null && jsonBuilder.HasRoomForString(innerJson))
                    {
                        jsonBuilder.Append(innerJson);
                    }
                    else
                    {
                        jsonBuilder.AppendLine(TabString(TRUNCATED_MESSAGE, nextNextTabDepth));
                        break;
                    }
                }

                jsonBuilder.AppendLine(TabString($"]", nextTabDepth));
            }

            if (!hasInnerException && !hasInnerExceptionList)
            {
                // No inner exceptions = no trailing comma needed
                jsonBuilder.AppendLine();
            }

            jsonBuilder.AppendLine(TabString("}" + (appendComma ? "," : ""), tab));
            return jsonBuilder.ToString();
        }

        private static string GetStackTraceJson(string stackTrace, int tab)
        {
            if (stackTrace == null)
            {
                return null;
            }

            string[] stackTraceElements = stackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                .Select(s => s.Trim())
                .Where(s => !String.IsNullOrWhiteSpace(s))
                .Select(s => TabString(($"\"{JsonExceptionWriterHelpers.EscapeStringForJson(s)}\""), tab + 1))
                .ToArray();

            if (stackTraceElements.Length == 0)
            {
                return null;
            }

            StringBuilder stackTraceBuilder = new StringBuilder();
            stackTraceBuilder.AppendLine(TabString($"\"{STACK_TRACE}\": [", tab));
            stackTraceBuilder.AppendLine(string.Join("," + Environment.NewLine, stackTraceElements));
            stackTraceBuilder.Append(TabString("]", tab));
            return stackTraceBuilder.ToString();
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

    }
}
