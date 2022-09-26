using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Amazon.Lambda.RuntimeSupport.UnitTests.TestHelpers
{
    internal class TestOutputTextWriter : TextWriter
    {
        public List<string> Lines { get; } = new List<string>();

        public override Encoding Encoding => new UTF8Encoding(false, false);

        public override void WriteLine(string line)
        {
            Lines.Add(line);
        }
    }
}
