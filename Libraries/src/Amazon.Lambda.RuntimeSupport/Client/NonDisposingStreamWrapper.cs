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
using System.IO;

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// This class is used to wrap the function response stream.
    /// It allows the wrapped stream to be reused.
    /// </summary>
    internal class NonDisposingStreamWrapper : Stream
    {
        Stream _wrappedStream;

        public NonDisposingStreamWrapper(Stream wrappedStream)
        {
            _wrappedStream = wrappedStream;
        }

        public override bool CanRead
        {
            get
            {
                return _wrappedStream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return _wrappedStream.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return _wrappedStream.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                return _wrappedStream.Length;
            }
        }

        public override long Position
        {
            get
            {
                return _wrappedStream.Position;
            }

            set
            {
                _wrappedStream.Position = value;
            }
        }

        public override void Flush()
        {
            _wrappedStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _wrappedStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _wrappedStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _wrappedStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _wrappedStream.Write(buffer, offset, count);
        }
    }
}
