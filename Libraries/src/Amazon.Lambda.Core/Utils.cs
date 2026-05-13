// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.Core
{
    internal static class Utils
    {
        internal static bool IsUsingMultiConcurrency
        {
            get
            {
                return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Constants.ENV_VAR_AWS_LAMBDA_MAX_CONCURRENCY));
            }
        }
    }
}
