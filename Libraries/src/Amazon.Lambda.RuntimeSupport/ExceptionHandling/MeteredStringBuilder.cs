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

namespace Amazon.Lambda.RuntimeSupport
{
    internal class MeteredStringBuilder
    {
        private readonly int _maxSize;
        private readonly Encoding _encoding;
        private readonly StringBuilder _stringBuilder;

        public int SizeInBytes { get; private set; }

        public MeteredStringBuilder(Encoding encoding, int maxSize)
        {
            if (maxSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize));
            }

            _stringBuilder = new StringBuilder();
            SizeInBytes = 0;
            _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            _maxSize = maxSize;
        }

        public void Append(string str)
        {
            int strSizeInBytes = _encoding.GetByteCount(str);
            _stringBuilder.Append(str);
            SizeInBytes += strSizeInBytes;
        }

        public void AppendLine(string str)
        {
            string strWithLine = str + Environment.NewLine;
            int strSizeInBytes = _encoding.GetByteCount(strWithLine);
            _stringBuilder.Append(strWithLine);
            SizeInBytes += strSizeInBytes;
        }

        public void AppendLine()
        {
            AppendLine("");
        }

        public bool HasRoomForString(string str)
        {
            return SizeInBytes + _encoding.GetByteCount(str) < _maxSize;
        }

        public override string ToString()
        {
            return _stringBuilder.ToString();
        }
    }
}
