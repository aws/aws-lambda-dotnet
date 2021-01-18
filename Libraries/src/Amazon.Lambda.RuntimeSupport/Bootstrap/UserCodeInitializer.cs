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

using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport.Helpers;

namespace Amazon.Lambda.RuntimeSupport.Bootstrap
{
    /// <summary>
    /// Finds and initializes user code.
    /// </summary>
    internal class UserCodeInitializer
    {
        private readonly UserCodeLoader _userCodeLoader;
        private readonly InternalLogger _logger;

        /// <summary>
        /// Creates an instance of UserCodeInitializer
        /// </summary>
        /// <param name="userCodeLoader">UserCodeLoader used for initialize user code</param>
        /// <param name="logger">Logger instance for logging initialization process logs</param>
        public UserCodeInitializer(UserCodeLoader userCodeLoader, InternalLogger logger)
        {
            _userCodeLoader = userCodeLoader;
            _logger = logger;
        }

        /// <summary>
        /// Finds and initializes user code.
        /// </summary>
        /// <returns></returns>
        public Task<bool> InitializeAsync()
        {
            _userCodeLoader.Init(_logger.LogInformation);
            return Task.FromResult(true);
        }
    }
}