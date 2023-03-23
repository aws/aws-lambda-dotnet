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
using System.IO;
using System.Buffers;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Collections.Concurrent;

namespace Amazon.Lambda.RuntimeSupport.Helpers
{
    /// <summary>
    /// This class wraps the utility of writing to the Lambda telemetry file descriptor for logging into a standard .NET Stream.
    /// The message
    /// </summary>
    public static class FileDescriptorLogFactory
    {
        private readonly static ConcurrentDictionary<string, StreamWriter> _writers = new ConcurrentDictionary<string, StreamWriter>();

        // max cloudwatch log event size, 256k - 26 bytes of overhead.
        internal const int MaxCloudWatchLogEventSize = 256 * 1024 - 26;
        internal const int LambdaTelemetryLogHeaderLength = 16;
        internal const uint LambdaTelemetryLogHeaderFrameType = 0xa55a0003;
        internal static readonly DateTimeOffset UnixEpoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// Get the StreamWriter for the particular file descriptor ID. If the same ID is passed the same StreamWriter instance is returned.
        /// </summary>
        /// <param name="fileDescriptorId"></param>
        /// <returns></returns>
        public static StreamWriter GetWriter(string fileDescriptorId)
        {
            var writer = _writers.GetOrAdd(fileDescriptorId,
                (x) => {
                    SafeFileHandle handle = new SafeFileHandle(new IntPtr(int.Parse(fileDescriptorId)), false);
                    return InitializeWriter(new FileStream(handle, FileAccess.Write));
                });
            return writer;
        }

        /// <summary>
        /// Initialize a StreamWriter for the given Stream.
        /// This method is internal as it is tested in Amazon.RuntimeSupport.Tests
        /// </summary>
        /// <param name="fileDescriptorStream"></param>
        /// <returns></returns>
        internal static StreamWriter InitializeWriter(Stream fileDescriptorStream)
        {
            // AutoFlush must be turned out otherwise the StreamWriter might not send the data to the stream before the Lambda function completes.
            // Set the buffer size to the same max size as CloudWatch Logs records.
            // Encoder has encoderShouldEmitUTF8Identifier = false as Lambda FD will assume UTF-8 so there is no need to emit an extra log entry.
            // In fact this extra log entry is cast to UTF-8 and results in an empty log entry which will be rejected by CloudWatch Logs.
            return new NonDisposableStreamWriter(new FileDescriptorLogStream(fileDescriptorStream),
                    new UTF8Encoding(false), MaxCloudWatchLogEventSize)
                { AutoFlush = true };
        }

        /// <summary>
        /// Write log message to the file descriptor which will make sure the message is recorded as a single CloudWatch Log record.
        /// The format of the message must be:
        /// 0                      4                        8                      16
        /// +----------------------+------------------------+-----------------------+-----------------------+
        /// | Frame Type - 4 bytes | Length (len) - 4 bytes | Timestamp - 8 bytes   | Message - 'len' bytes |
        /// +----------------------+------------------------+-----------------------+-----------------------+
        /// The first 4 bytes are the frame type. For logs with timestamps this is always 0xa55a0003.
        /// The second 4 bytes are the length of the message.
        /// Next is 8 bytes timestamp of emitting the message expressed as microseconds since UNIX epoch.
        /// The remaining bytes are the message itself. Byte order is big-endian.
        /// </summary>
        private class FileDescriptorLogStream : Stream
        {
            private readonly Stream _fileDescriptorStream;
            private readonly byte[] _frameTypeBytes;

            public FileDescriptorLogStream(Stream logStream)
            {
                _fileDescriptorStream = logStream;

                _frameTypeBytes = BitConverter.GetBytes(LambdaTelemetryLogHeaderFrameType);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(_frameTypeBytes);
                }
            }

            public override void Flush()
            {
                _fileDescriptorStream.Flush();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                var messageLengthBytes = BitConverter.GetBytes(count - offset);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(messageLengthBytes);
                }

                var now = (DateTimeOffset.UtcNow - UnixEpoch).Ticks / 10; // There are 10 tick per microsecond
                var nowInBytes = BitConverter.GetBytes(now);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(nowInBytes);
                }
                var typeAndLength = ArrayPool<byte>.Shared.Rent(LambdaTelemetryLogHeaderLength);
                try
                {
                    Buffer.BlockCopy(_frameTypeBytes, 0, typeAndLength, 0, 4);
                    Buffer.BlockCopy(messageLengthBytes, 0, typeAndLength, 4, 4);
                    Buffer.BlockCopy(nowInBytes, 0, typeAndLength, 8, 8);

                    _fileDescriptorStream.Write(typeAndLength, 0, LambdaTelemetryLogHeaderLength);
                    _fileDescriptorStream.Write(buffer, offset, count);
                    _fileDescriptorStream.Flush();
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(typeAndLength);
                }
            }

            #region Not implemented read and seek operations
            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotSupportedException();

            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }
            #endregion
        }

        /// <summary>
        /// This class is used to ensure the StreamWriter that is returned can not be unintentionally closed/disposed by users.
        /// If we allow the stream to be closed/disposed then future logging in the Lambda function will fail with object disposed exceptions.
        /// This situation was discovered for a function using NUnitLite to run tests and that library would trigger a dispose on Console.Out
        /// https://github.com/nunit/nunit/blob/92180f13381621e308b01f0abd1a397cc1350c12/src/NUnitFramework/nunitlite/TextRunner.cs#L104
        /// </summary>
        class NonDisposableStreamWriter : StreamWriter
        {
            public NonDisposableStreamWriter(Stream stream, Encoding encoding, int buffersize)
                : base(stream, encoding, buffersize)
            {

            }

            protected override void Dispose(bool disposing)
            {
                // This StreamWriter must never be disposed. If disposed logging will fail in the function.
            }

            public override void Close()
            {
                // This StreamWriter must never be disposed. If disposed logging will fail in the function.
            }
        }
    }
}
