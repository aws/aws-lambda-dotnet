using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Amazon.Lambda.AspNetCoreServer.Internal
{
    /// <summary>
    /// 
    /// </summary>
    public static class Utilities
    {
        internal static Stream ConvertLambdaRequestBodyToAspNetCoreBody(string body, bool isBase64Encoded)
        {
            Byte[] binaryBody;
            if (isBase64Encoded)
            {
                binaryBody = Convert.FromBase64String(body);
            }
            else
            {
                binaryBody = UTF8Encoding.UTF8.GetBytes(body);
            }

            return new MemoryStream(binaryBody);
        }

        internal static (string body, bool isBase64Encoded) ConvertAspNetCoreBodyToLambdaBody(Stream aspNetCoreBody, ResponseContentEncoding rcEncoding)
        {

            // Do we encode the response content in Base64 or treat it as UTF-8
            if (rcEncoding == ResponseContentEncoding.Base64)
            {
                // We want to read the response content "raw" and then Base64 encode it
                byte[] bodyBytes;
                if (aspNetCoreBody is MemoryStream)
                {
                    bodyBytes = ((MemoryStream)aspNetCoreBody).ToArray();
                }
                else
                {
                    using (var ms = new MemoryStream())
                    {
                        aspNetCoreBody.CopyTo(ms);
                        bodyBytes = ms.ToArray();
                    }
                }
                return (body: Convert.ToBase64String(bodyBytes), isBase64Encoded: true);
            }
            else if (aspNetCoreBody is MemoryStream)
            {
                return (body: UTF8Encoding.UTF8.GetString(((MemoryStream)aspNetCoreBody).ToArray()), isBase64Encoded: false);
            }
            else
            {
                aspNetCoreBody.Position = 0;
                using (StreamReader reader = new StreamReader(aspNetCoreBody, Encoding.UTF8))
                {
                    return (body: reader.ReadToEnd(), isBase64Encoded: false);
                }
            }
        }

        /// <summary>
        /// Generate different casing permutations of the input string.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public  static IEnumerable<string> Permute(string input)
        {
            // Determine the number of alpha characters that can be toggled.
            int alphaCharCount = 0;
            foreach(var c in input)
            {
                if(char.IsLetter(c))
                {
                    alphaCharCount++;
                }
            }

            // Map the indexes to the position of the alpha characters in the original input string.
            var alphaIndexes = new int[alphaCharCount];
            var alphaIndex = 0;
            for(int i = 0; i < input.Length; i++)
            {
                if(char.IsLetter(input[i]))
                {
                    alphaIndexes[alphaIndex++] = i;
                }
            }

            // Number of permutations is 2^n
            int max = 1 << alphaCharCount;

            // Converting string
            // to lower case
            input = input.ToLower();

            // Using all subsequences 
            // and permuting them
            for (int i = 0; i < max; i++)
            {
                char[] combination = input.ToCharArray();

                // If j-th bit is set, we 
                // convert it to upper case
                for (int j = 0; j < alphaIndexes.Length; j++)
                {
                    if (((i >> j) & 1) == 1)
                    {
                        combination[alphaIndexes[j]] = (char)(combination[alphaIndexes[j]] - 32);
                    }
                }

                yield return new string(combination);
            }
        }

        internal static string CreateQueryStringParameter(string key, string value)
        {
            return $"{key}={value}";
        }
    }
}
