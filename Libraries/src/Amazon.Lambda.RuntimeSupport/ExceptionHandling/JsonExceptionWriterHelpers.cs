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
using System.Text;

namespace Amazon.Lambda.RuntimeSupport
{
    internal class JsonExceptionWriterHelpers
    {
        /// <summary>
        /// This method escapes a string for use as a JSON string value.
        /// It was adapted from the PutString method in the LitJson.JsonWriter class.
        ///
        /// TODO: rewrite the *JsonExceptionWriter classes to use a JSON library instead of building strings directly.
        /// </summary>
        /// <param name="str"></param>
        public static string EscapeStringForJson(string str)
        {
            if (str == null)
                return null;

            int n = str.Length;
            var sb = new StringBuilder(n * 2);
            for (int i = 0; i < n; i++)
            {
                char c = str[i];
                switch (c)
                {
                    case '\n':
                        sb.Append(@"\n");
                        break;

                    case '\r':
                        sb.Append(@"\r");
                        break;

                    case '\t':
                        sb.Append(@"\t");
                        break;

                    case '"':
                        sb.Append(@"\""");
                        break;

                    case '\\':
                        sb.Append(@"\\");
                        break;

                    case '\f':
                        sb.Append(@"\f");
                        break;

                    case '\b':
                        sb.Append(@"\b");
                        break;

                    case '\u0085': // Next Line
                        sb.Append(@"\u0085");
                        break;

                    case '\u2028': // Line Separator
                        sb.Append(@"\u2028");
                        break;

                    case '\u2029': // Paragraph Separator
                        sb.Append(@"\u2029");
                        break;

                    default:
                        if (c < ' ')
                        {
                            // Turn into a \uXXXX sequence
                            sb.Append(@"\u");
                            sb.Append(IntToHex((int)c));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString().Trim();
        }

        private static char[] IntToHex(int n)
        {
            int num;
            char[] hex = new char[4];

            for (int i = 0; i < 4; i++)
            {
                num = n % 16;

                if (num < 10)
                    hex[3 - i] = (char)('0' + num);
                else
                    hex[3 - i] = (char)('A' + (num - 10));

                n >>= 4;
            }
            return hex;
        }
    }
}
