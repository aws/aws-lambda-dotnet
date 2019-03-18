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
using System.Diagnostics;
using System.Text;

namespace Amazon.Lambda.RuntimeSupport
{
    internal class StackFrameInfo
    {
        public StackFrameInfo(string path, int line, string label)
        {
            Path = path;
            Line = line;
            Label = label;
        }

        public StackFrameInfo(StackFrame stackFrame)
        {
            Path = stackFrame.GetFileName();
            Line = stackFrame.GetFileLineNumber();

            var method = stackFrame.GetMethod();
            if (method != null)
            {
                var methodTypeName = method.DeclaringType?.Name;
                if (methodTypeName == null)
                {
                    Label = method.Name;
                }
                else
                {
                    Label = methodTypeName + "." + method.Name;
                }
            }
        }

        public string Path { get; }
        public int Line { get; }
        public string Label { get; }
    }
}
