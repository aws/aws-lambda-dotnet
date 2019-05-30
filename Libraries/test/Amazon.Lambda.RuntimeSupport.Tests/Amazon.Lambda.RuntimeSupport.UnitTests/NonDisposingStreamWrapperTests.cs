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
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class NonDisposingStreamWrapperTests
    {
        private const string TestString = "testing123";
        [Fact]
        public async Task MakeSureDisposeWorks()
        {
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(TestString));
            using (var streamWrapper = new NonDisposingStreamWrapper(memoryStream))
            {
                var buffer = new byte[memoryStream.Length];
                await streamWrapper.ReadAsync(buffer);
                Assert.Equal(TestString, Encoding.UTF8.GetString(buffer));
            }

            // show that it's not disposed
            memoryStream.Position = 0;

            memoryStream.Dispose();

            // show that it's disposed now
            var caughtException = false;
            try
            {
                memoryStream.Position = 0;
            }
            catch (ObjectDisposedException)
            {
                caughtException = true;
            }
            Assert.True(caughtException);
        }
    }
}
