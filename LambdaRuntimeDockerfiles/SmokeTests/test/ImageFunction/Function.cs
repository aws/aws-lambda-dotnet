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
using System.Net.Http;
using System.Threading.Tasks;

using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ImageFunction
{
    internal class Function
    {
        private const string FailureResult = "FAILURE";
        private const string SuccessResult = "SUCCESS";
        private const string TestUrl = "https://www.amazon.com";
        private static readonly Lazy<string> SevenMbString = new Lazy<string>(() => new string('X', 1024 * 1024 * 7));

        #region Test methods

        public string ToUpper(string input, ILambdaContext context)
        {
            return input?.ToUpper();
        }

        public string Ping(string input)
        {
            if (input == "ping")
            {
                return "pong";
            }
            else
            {
                throw new Exception($"Expected input: ping but recevied {input}");
            }
        }

        public async Task<string> HttpsWorksAsync()
        {
            var isSuccess = false;

            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(TestUrl);
                if (response.IsSuccessStatusCode)
                {
                    isSuccess = true;
                }
                Console.WriteLine($"Response from HTTP get: {response}");
            }

            return GetResponse(isSuccess);
        }

        public async Task<string> ThrowExceptionAsync()
        {
            // do something async so this function is compiled as async
            var dummy = await Task.FromResult("xyz");
            throw new Exception("Exception thrown from an async handler.");
        }

        public void ThrowException()
        {
            throw new Exception("Exception thrown from a synchronous handler.");
        }

        public async Task<string> AggregateExceptionNotUnwrappedAsync()
        {
            // do something async so this function is compiled as async
            var dummy = await Task.FromResult("xyz");
            throw new AggregateException("AggregateException thrown from an async handler.");
        }

        public void AggregateExceptionNotUnwrapped()
        {
            throw new AggregateException("AggregateException thrown from a synchronous handler.");
        }

        public string TooLargeResponseBody()
        {
            return SevenMbString.Value;
        }

        public string VerifyLambdaContext(ILambdaContext lambdaContext)
        {
            AssertNotNull(lambdaContext.AwsRequestId, nameof(lambdaContext.AwsRequestId));
            AssertNotNull(lambdaContext.ClientContext, nameof(lambdaContext.ClientContext));
            AssertNotNull(lambdaContext.FunctionName, nameof(lambdaContext.FunctionName));
            AssertNotNull(lambdaContext.FunctionVersion, nameof(lambdaContext.FunctionVersion));
            AssertNotNull(lambdaContext.Identity, nameof(lambdaContext.Identity));
            AssertNotNull(lambdaContext.InvokedFunctionArn, nameof(lambdaContext.InvokedFunctionArn));
            AssertNotNull(lambdaContext.Logger, nameof(lambdaContext.Logger));
            AssertNotNull(lambdaContext.LogGroupName, nameof(lambdaContext.LogGroupName));
            AssertNotNull(lambdaContext.LogStreamName, nameof(lambdaContext.LogStreamName));

            AssertTrue(lambdaContext.MemoryLimitInMB >= 128,
                $"{nameof(lambdaContext.MemoryLimitInMB)}={lambdaContext.MemoryLimitInMB} is not >= 128");
            AssertTrue(lambdaContext.RemainingTime > TimeSpan.Zero,
                $"{nameof(lambdaContext.RemainingTime)}={lambdaContext.RemainingTime} is not >= 0");

            return GetResponse(true);
        }

        #endregion

        #region Private methods

        private static string GetResponse(bool isSuccess)
        {
            return $"{(isSuccess ? SuccessResult : FailureResult)}";
        }

        private static void AssertNotNull(object value, string valueName)
        {
            if (value == null)
            {
                throw new Exception($"{valueName} cannot be null.");
            }
        }

        private static void AssertTrue(bool value, string errorMessage)
        {
            if (!value)
            {
                throw new Exception(errorMessage);
            }
        }

        #endregion
    }
}
