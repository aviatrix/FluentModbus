using FluentModbus;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MultiTester
{
    internal class Program
    {
        private static async System.Threading.Tasks.Task Main(string[] args)
        {
            ;
            var units = new byte[] { 1, 2, 3 };
            var server = new ModbusMultiUnitRtuServer(units, readTimeout: 5000, writeTimeout: 5000);

            server.Start("COM5", false);

            //var client = new ModbusRtuClient();
            //client.Connect("COM5");

            //var data = Enumerable.Range(0, 20).Select(i => (float)i).ToArray();
            //var sw = Stopwatch.StartNew();
            //var iterations = 100;

            //for (int i = 0; i < iterations; i++)
            //{
            //    foreach (var u in units)
            //    {
            //        client.WriteSingleRegister(u, i, (short)i);
            //    }
            //}

            //var timePerRequest = sw.Elapsed.TotalMilliseconds / iterations;
            //Console.WriteLine($"Time per request: {timePerRequest * 1000:F0} us. Frequency: {1 / timePerRequest * 1000:F0} requests per second.");

            //// Assert
            //foreach (var unit in units)
            //{
            //    var resdata = await client.ReadHoldingRegistersAsync(unit, 1, 20);
            //    Console.WriteLine($"unit {unit}: {string.Join(",", resdata)}");
            //}
            //client.Close();
            Console.ReadKey();
        }
    }
}