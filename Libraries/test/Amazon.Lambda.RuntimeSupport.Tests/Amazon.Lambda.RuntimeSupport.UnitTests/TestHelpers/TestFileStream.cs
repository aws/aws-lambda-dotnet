using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            WriteAction(TrimTrailingNullBytes(buffer).Take(count).ToArray(), offset, count);
        }

        private static IEnumerable<byte> TrimTrailingNullBytes(IEnumerable<byte> buffer)
        {
            // Trim trailing null bytes to make testing assertions easier
            return buffer.Reverse().SkipWhile(x => x == 0).Reverse();
        }
    }
}
