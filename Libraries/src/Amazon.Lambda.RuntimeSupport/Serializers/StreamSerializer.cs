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

using System.IO;
using Amazon.Lambda.RuntimeSupport.ExceptionHandling;

namespace Amazon.Lambda.RuntimeSupport.Serializers
{
    /// <summary>
    /// Data serializer that converts stream to stream
    /// </summary>
    internal class StreamSerializer
    {
        public static StreamSerializer Instance { get; } = new StreamSerializer();

        /// <summary>
        /// Deserializes a Lambda stream to the input type
        /// </summary>
        /// <param name="lambdaData"></param>
        /// <returns></returns>
        public Stream Deserialize(Stream lambdaData)
        {
            return lambdaData;
        }

        /// <summary>
        /// Serializes the output type to a Lambda stream
        /// </summary>
        /// <param name="customerData"></param>
        /// <param name="outStream"></param>
        /// <returns></returns>
        public void Serialize(Stream customerData, Stream outStream)
        {
            try
            {
                customerData.CopyTo(outStream);
            }
            catch (System.NotSupportedException e)
            {
                if (e.Message.Contains("Unable to expand length of this stream beyond its capacity"))
                {
                    // This exception is thrown when returned stream is bigger than the customerData stream can handle.
                    // The limit is defined by LAMBDA_EVENT_BODY_SIZE in runtime.h
                    throw new System.ArgumentException(Errors.LambdaBootstrap.Internal.LambdaResponseTooLong, e);
                }
                else
                {
                    throw;
                }
            }
        }
    }
}