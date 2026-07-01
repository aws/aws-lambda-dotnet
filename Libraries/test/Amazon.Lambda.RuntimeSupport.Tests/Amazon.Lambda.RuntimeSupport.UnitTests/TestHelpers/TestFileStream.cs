using System;
using System.IO;

namespace Amazon.Lambda.RuntimeSupport.UnitTests.TestHelpers
{
    public class TestFileStream : FileStream
    {
        private Action<byte[], int, int> WriteAction { get; }

        public TestFileStream(Action<byte[], int, int> writeAction)
            : base(Path.GetTempFileName(), FileMode.Append, FileAccess.Write)
        {
            WriteAction = writeAction;
        }

        public override bool CanWrite => true;

        public override void Write(byte[] buffer, int offset, int count)
        {
            // Capture exactly the bytes that were written: [offset, offset + count).
            // The previous implementation trimmed trailing null bytes from the buffer, which was
            // flaky: a log header ends with an 8-byte big-endian microsecond timestamp, and roughly
            // 1 in 256 timestamps ends in a 0x00 byte. Trimming that legitimate byte made the
            // captured header 15 bytes instead of 16 and failed MaxSizeProducesOneLogFrame.
            var written = new byte[count];
            Array.Copy(buffer, offset, written, 0, count);
            WriteAction(written, offset, count);
        }
    }
}
