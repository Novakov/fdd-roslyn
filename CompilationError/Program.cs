using System;
using System.Threading.Tasks;

namespace ConsoleApplication11
{
    class Program
    {
        static void Main()
        {
            M(0).Wait();
        }

        static async Task M(int input)
        {
            for (;;)
            {
                var value = await Task.FromResult(input);
                switch (value)
                {
                    case 0:
                        return;
                    case 3:
                        return;
                    case 4:
                        continue;
                    case 100:
                        return;
                    default:
                        throw new ArgumentOutOfRangeException("Unknown value: " + value);
                }
            }
        }
    }
}