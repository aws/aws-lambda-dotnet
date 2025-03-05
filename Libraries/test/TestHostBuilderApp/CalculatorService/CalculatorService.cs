// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace TestHostBuilderApp
{
    public class CalculatorService : ICalculatorService
    {
        /// <inheritdoc/>
        public int Add(int x, int y)
        {
            return x + y;
        }
    }
}
