namespace Amazon.Lambda.TestTool.Services;

public class ConsoleInteractiveService : IToolInteractiveService
{
    public ConsoleInteractiveService()
    {
        Console.Title = Constants.ProductName;
    }
    
    public void WriteLine(string? message)
    {
        Console.WriteLine(message);
    }

    public void WriteErrorLine(string? message)
    {
        var color = Console.ForegroundColor;

        try
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(message);
        }
        finally
        {
            Console.ForegroundColor = color;
        }
    }
}