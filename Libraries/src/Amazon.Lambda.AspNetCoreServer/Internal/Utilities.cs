using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.AspNetCoreServer.Internal
{
    /// <summary>
    /// 
    /// </summary>
    public static class Utilities
    {

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
    }
}
