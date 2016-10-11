using System;
using System.Linq.Expressions;

namespace OdataToEntity.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            //new BatchTest().Delete().Wait();
            Console.WriteLine();
            new SelectTest().OrderBy().Wait();

            Console.ReadLine();
        }
    }
}
