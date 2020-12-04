/*
 * Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
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
using System.Diagnostics;
using System.IO;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public static class Common
    {
        public const string WorkingHandler = "HandlerTest::HandlerTest.CustomerType::PocoInPocoOut";

        /// <summary>
        /// Recursively check the Exception and inner exceptions to see if any of them are AggregateExceptions.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="expectAggregateException">true if AggregateException is expected, false if not expected, null to skip check</param>
        public static void CheckForAggregateException(Exception e, bool? expectAggregateException)
        {
            if (expectAggregateException.HasValue)
            {
                if (e is AggregateException)
                {
                    // We came across an AggregateException.  Make sure we were expecting it.
                    Assert.True(expectAggregateException, "Didn't expect an AggregateException but got one");
                }
                else if (e.InnerException == null)
                {
                    // We didn't come across an AggregateException.  Make sure we weren't expecting to.
                    Assert.False(expectAggregateException, "Expected an AggregateException but didn't get one.");
                }
                else
                {
                    // e isn't an AggregateException and there's an InnerException - call recursively.
                    CheckForAggregateException(e.InnerException, expectAggregateException);
                }
            }
        }

        public static void CheckException(Exception e, string expectedPartialMessage)
        {
            if (!FindMatchingExceptionMessage(e, expectedPartialMessage))
            {
                Assert.True(false, $"Unable to match up expected message '{expectedPartialMessage}' in exception: {GetAllMessages(e)}");
            }
        }

        public static bool FindMatchingExceptionMessage(Exception e, string expectedPartialMessage)
        {
            var isMatch = e.Message.IndexOf(expectedPartialMessage) >= 0;
            if (isMatch)
            {
                return true;
            }

            if (e.InnerException != null)
            {
                return FindMatchingExceptionMessage(e.InnerException, expectedPartialMessage);
            }

            return false;
        }

        public static string GetAllMessages(Exception e)
        {
            using (var writer = new StringWriter())
            {
                while (e != null)
                {
                    writer.WriteLine("[{0}]", e.Message);
                    e = e.InnerException;
                }

                return writer.ToString();
            }
        }
    }
}