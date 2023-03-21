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

using System.Runtime.CompilerServices;

namespace Amazon.Lambda.RuntimeSupport.Helpers
{
    internal static class NativeAotHelper
    {
        public static bool IsRunningNativeAot()
        {
            // If dynamic code is not supported we are most likely running in an AOT environment. 
#if NET6_0_OR_GREATER
            return !RuntimeFeature.IsDynamicCodeSupported;
#else
            return false;
#endif
        }
    }
}
