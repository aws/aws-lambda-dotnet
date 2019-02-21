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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// Wrap the user's output stream in this so we can give them control over when it gets disposed.
    /// </summary>
    internal class NonDisposingStreamContent : StreamContent
    {
        public NonDisposingStreamContent(Stream content) : base(content) { }

        protected override void Dispose(bool disposing)
        {
            // Skip the Dispose on StreamContent by casting to its base class, HttpContent.
            ((HttpContent)this).Dispose();
        }
    }
}
