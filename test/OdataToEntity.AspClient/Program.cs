using OdataToEntity.Test;
using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntityCore.AspClient
{
    class Program
    {
        static void Main(String[] args)
        {
            //new SelectTest().SelectName().Wait();

            RunTest(new BatchTest()).GetAwaiter().GetResult();
            RunTest(new SelectTest()).GetAwaiter().GetResult();

            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private async static Task RunTest<T>(T testClass)
        {
            foreach (MethodInfo methodInfo in testClass.GetType().GetMethods().Where(m => m.GetCustomAttributes(typeof(FactAttribute), false).Length == 1))
            {
                var testMethod = (Func<T, Task>)methodInfo.CreateDelegate(typeof(Func<T, Task>));
                Console.WriteLine(methodInfo.Name);
                try
                {
                    await testMethod(testClass);
                }
                catch (HttpRequestException e)
                {
                    ConsoleWriteException(e, ConsoleColor.Yellow);
                    return;
                }
                catch (NotSupportedException e)
                {
                    ConsoleWriteException(e, ConsoleColor.Yellow);
                }
                catch (InvalidOperationException e)
                {
                    ConsoleWriteException(e, ConsoleColor.Red);
                }
            }
        }
        private static void ConsoleWriteException(Exception e, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(e.Message);
            Console.ResetColor();
        }
    }
}
