using System.CommandLine;
using System.Text;

namespace Amazon.Lambda.TestTool;

public class AppRunner(
    ICommandFactory commandFactory)
{
    public async Task<int> Run(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        return await commandFactory.BuildRootCommand().InvokeAsync(args);
    }
}