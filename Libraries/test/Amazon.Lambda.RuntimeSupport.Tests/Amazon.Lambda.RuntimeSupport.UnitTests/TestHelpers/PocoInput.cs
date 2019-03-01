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

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class PocoInput
    {
        public String InputString { get; set; }
        public int InputInt { get; set; }

        public override bool Equals(object value)
        {
            PocoInput pocoInput = value as PocoInput;

            return !Object.ReferenceEquals(null, pocoInput)
                && String.Equals(InputString, pocoInput.InputString)
                && int.Equals(InputInt, pocoInput.InputInt);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashBase = (int)2166136261;
                int hashMultiplier = 16777619;

                int hash = hashBase;
                hash = (hash * hashMultiplier) ^ (!Object.ReferenceEquals(null, InputString) ? InputString.GetHashCode() : 0);
                hash = (hash * hashMultiplier) ^ (!Object.ReferenceEquals(null, InputInt) ? InputInt.GetHashCode() : 0);
                return hash;
            }
        }
    }
}
