namespace BlueprintBaseName._1
{
    /// <summary>
    /// The implementation of <see cref="ICalculatorService"/> 
    /// that will be used by our Lambda functions.
    /// </summary>
    public class CalculatorService : ICalculatorService
    {
        /// <inheritdoc/>
        public int Add(int x, int y)
        {
            return x + y;
        }

        /// <inheritdoc/>
        public int Subtract(int x, int y)
        {
            return x - y;
        }

        /// <inheritdoc/>
        public int Multiply(int x, int y)
        {
            return x * y;
        }

        /// <inheritdoc/>
        public int Divide(int x, int y)
        {
            return x / y;
        }
    }
}
