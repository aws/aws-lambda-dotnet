using System;
using System.Text.RegularExpressions;

namespace TestServerlessApp.Services
{
    public interface ISimpleCalculatorService
    {
        int Add(int x, int y);
        int Subtract(int x, int y);
        int Multiply(int x, int y);
        int Divide(int x, int y);
        double PI();
    }

    public class SimpleCalculatorService : ISimpleCalculatorService
    {
        public int Divide(int x, int y)
        {
            return x / y;
        }

        public double PI()
        {
            return Math.PI;
        }

        public int Multiply(int x, int y)
        {
            return x * y;
        }

        public int Add(int x, int y)
        {
            return x + y;
        }

        public int Subtract(int x, int y)
        {
            return x - y;
        }
    }
}