using Microsoft.AspNetCore.Mvc;

namespace BlueprintBaseName._1.Controllers;

[ApiController]
[Route("[controller]")]
public class CalculatorController : ControllerBase
{
    private readonly ILogger<CalculatorController> _logger;

    public CalculatorController(ILogger<CalculatorController> logger)
    {
        _logger = logger;
    }

    // GET calculator/add/4/2/
    [HttpGet("add/{x}/{y}")]
    public int Add(int x, int y)
    {
        _logger.LogInformation($"{x} plus {y} is {x + y}");
        return x + y;
    }

    // GET calculator/subtract/4/2/
    [HttpGet("subtract/{x}/{y}")]
    public int Subtract(int x, int y)
    {
        _logger.LogInformation($"{x} subtract {y} is {x - y}");
        return x - y;
    }

    // GET calculator/multiply/4/2/
    [HttpGet("multiply/{x}/{y}")]
    public int Multiply(int x, int y)
    {
        _logger.LogInformation($"{x} multiply {y} is {x * y}");
        return x * y;
    }

    // GET calculator/divide/4/2/
    [HttpGet("divide/{x}/{y}")]
    public int Divide(int x, int y)
    {
        _logger.LogInformation($"{x} divide {y} is {x / y}");
        return x / y;
    }
}
