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
using Amazon.Lambda.Serialization.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class TestInitializer
    {
        public const string InitializeExceptionMessage = "Initialize Exception";

        protected JsonSerializer _jsonSerializer = new JsonSerializer();

        public bool InitializerWasCalled { get; protected set; }

        public bool InitializeTrue()
        {
            return Initialize(true);
        }
        public bool InitializeFalse()
        {
            return Initialize(false);
        }
        public bool InitializeThrow()
        {
            return Initialize(null);
        }

        public Task<bool> InitializeTrueAsync()
        {
            return Task.FromResult(Initialize(true));
        }
        public Task<bool> InitializeFalseAsync()
        {
            return Task.FromResult(Initialize(false));
        }
        public Task<bool> InitializeThrowAsync()
        {
            return Task.FromResult(Initialize(null));
        }

        private bool Initialize(bool? result)
        {
            InitializerWasCalled = true;
            if (result.HasValue)
            {
                return result.Value;
            }
            else
            {
                throw new Exception(InitializeExceptionMessage);
            }
        }
    }
}
