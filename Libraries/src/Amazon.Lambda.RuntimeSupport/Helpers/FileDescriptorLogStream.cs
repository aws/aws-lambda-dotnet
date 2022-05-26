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

        /// <summary>
        /// Get the StreamWriter for the particular file descriptor ID. If the same ID is passed the same StreamWriter instance is returned.
        /// </summary>
        /// <param name="fileDescriptorId"></param>
        /// <returns></returns>
        public static StreamWriter GetWriter(string fileDescriptorId)
        {
            // AutoFlush must be turned out otherwise the StreamWriter might not send the data to the stream before the Lambda function completes.
            // Set the buffer size to the same max size as CloudWatch Logs records.
            var writer = _writers.GetOrAdd(fileDescriptorId, (x) => new StreamWriter(new FileDescriptorLogStream(fileDescriptorId), Encoding.UTF8, 256 * 1024) { AutoFlush = true });
            return writer;
        }

        /// <summary>
        /// Write log message to the file descriptor which will make sure the message is recorded as a single CloudWatch Log record.
        /// The format of the message must be:
        /// +----------------------+------------------------+-----------------------+
        /// | Frame Type - 4 bytes | Length (len) - 4 bytes | Message - 'len' bytes |
        /// +----------------------+------------------------+-----------------------+
        /// The first 4 bytes are the frame type. For logs this is always 0xa55a0001.
        /// The second 4 bytes are the length of the message.
        /// The remaining bytes are the message itself. Byte order is big-endian.
        /// </summary>
        private class FileDescriptorLogStream : Stream
        {
            private const uint FRAME_TYPE = 0xa55a0001;
            private readonly Stream _fileDescriptorStream;
            private readonly byte[] _frameTypeBytes;

            public FileDescriptorLogStream(string fileDescriptorId)
            {
                SafeFileHandle handle = new SafeFileHandle(new IntPtr(int.Parse(fileDescriptorId)), false);
                _fileDescriptorStream = new FileStream(handle, FileAccess.Write);

                _frameTypeBytes = BitConverter.GetBytes(FRAME_TYPE);
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

                var typeAndLength = ArrayPool<byte>.Shared.Rent(8);
                try
                {
                    Buffer.BlockCopy(_frameTypeBytes, 0, typeAndLength, 0, 4);
                    Buffer.BlockCopy(messageLengthBytes, 0, typeAndLength, 4, 4);

                    _fileDescriptorStream.Write(typeAndLength, 0, 8);
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
    }
}