namespace BlueprintBaseName._1
{
    /// <summary>
    /// An interface for a service that implements the business logic of our Lambda functions
    /// </summary>
    public interface ICalculatorService
    {
        /// <summary>
        /// Adds x and y together
        /// </summary>
        /// <param name="x">Addend</param>
        /// <param name="y">Addend</param>
        /// <returns>Sum of x and y</returns>
        int Add(int x, int y);

        /// <summary>
        /// Subtracts y from x
        /// </summary>
        /// <param name="x">Minuend</param>
        /// <param name="y">Subtrahend</param>
        /// <returns>x - y</returns>
        int Subtract(int x, int y);

        /// <summary>
        /// Multiplies x and y
        /// </summary>
        /// <param name="x">Multiplicand</param>
        /// <param name="y">Multiplier</param>
        /// <returns>x * y</returns>
        int Multiply(int x, int y);

        /// <summary>
        /// Divides x by y
        /// </summary>
        /// <param name="x">Dividend</param>
        /// <param name="y">Divisor</param>
        /// <returns>x / y</returns>
        int Divide(int x, int y);
    }
}
