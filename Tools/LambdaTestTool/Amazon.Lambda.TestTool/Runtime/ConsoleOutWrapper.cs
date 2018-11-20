using System;
using System.IO;
using System.Text;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.TestTool.Runtime
{
    /// <summary>
    /// This class is used to capture standard out and standard error when executing the Lambda function.
    /// </summary>
    public class ConsoleOutWrapper : IDisposable
    {
        private readonly TextWriter _standardOut;
        private readonly TextWriter _standardError;

        
        public ConsoleOutWrapper(ILambdaLogger logger)
        {
            _standardOut = Console.Out;
            Console.SetOut(new WrapperTextWriter(_standardOut, logger, false));
            
            _standardError = Console.Error;
            Console.SetError(new WrapperTextWriter(_standardError, logger, false));

        }

        public void Dispose()
        {
            Console.SetOut(_standardOut);
            Console.SetError(_standardError);
        }


        class WrapperTextWriter : TextWriter
        {
            private readonly TextWriter _innerWriter;
            private readonly ILambdaLogger _logger;
            private readonly bool _writeToInnerWriter;

            public WrapperTextWriter(TextWriter innerWriter, ILambdaLogger logger, bool writeToInnerWriter)
            {
                _innerWriter = innerWriter;
                _logger = logger;
                _writeToInnerWriter = writeToInnerWriter;
            }
            
            public override Encoding Encoding 
            {
                get { return Encoding.UTF8;}
            }

            public override void Write(string value)
            {
                _logger.Log(value);
                if (this._writeToInnerWriter)
                {
                    this._innerWriter?.Write(value);
                }
            }
        }


    }
}