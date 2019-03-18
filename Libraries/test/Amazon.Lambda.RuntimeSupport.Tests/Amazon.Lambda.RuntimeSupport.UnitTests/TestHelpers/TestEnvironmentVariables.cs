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
using System.Collections;
using System.Collections.Generic;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    class TestEnvironmentVariables : IEnvironmentVariables
    {
        Dictionary<string, string> environmentVariables = new Dictionary<string, string>();

        public TestEnvironmentVariables(IDictionary<string, string> initialValues = null)
        {
            environmentVariables = initialValues == null ? 
                new Dictionary<string, string>() :
                new Dictionary<string, string>(initialValues);
        }

        public string GetEnvironmentVariable(string variable)
        {
            environmentVariables.TryGetValue(variable, out var value);
            return value;
        }

        public IDictionary GetEnvironmentVariables()
        {
            return new Dictionary<string, string>(environmentVariables);
        }

        public void SetEnvironmentVariable(string variable, string value)
        {
            environmentVariables[variable] = value;
        }
    }
}
