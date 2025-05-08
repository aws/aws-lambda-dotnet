// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace TestHostBuilderApp
{
    /// <summary>
    /// An interface for a service that implements the business logic of our Lambda functions
    /// </summary>
    public interface ICalculatorService
    {
        /// <summary>
        /// Adds x and y together
        /// </summary>
        /// <param name="x">Addend</param>
        /// <param name="y">Addend</param>
        /// <returns>Sum of x and y</returns>
        int Add(int x, int y);
    }
}
