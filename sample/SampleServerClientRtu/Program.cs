using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentModbus.ServerMultiUnit;

namespace FluentModbus.SampleMaster
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            /* Modbus RTU uses a COM port for communication. Therefore, to run
             * this sample, you need to make sure that there are real or virtual
             * COM ports available. The easiest way is to install one of the free
             * COM port bridges available in the internet. That way, the Modbus
             * server can connect to e.g. COM1 which is virtually linked to COM2,
             * where the client is connected to.
             *
             * When you only want to use the client and communicate to an external
             * Modbus server, simply remove all server related code parts in this
             * sample and connect to real COM port using only the client.
             */

            /* define COM ports */
            var serverPort = "COM5";
            var clientPort = "COM6";

            /* create logger */
            var loggerFactory = LoggerFactory.Create(loggingBuilder =>
            {
                loggingBuilder.SetMinimumLevel(LogLevel.Debug);
                loggingBuilder.AddConsole();
            });

            var serverLogger = loggerFactory.CreateLogger("Server");
            var clientLogger = loggerFactory.CreateLogger("Client");

            /* create Modbus RTU server */
            var server = new MultiUnitRtuServer(new byte[] { 1, 2, 3 }, false)
            {
                // see 'RegistersChanged' event below
                EnableRaisingEvents = true
            };

            /* subscribe to the 'RegistersChanged' event (in case you need it) */
            server.RegistersChanged += (sender, bz) =>
            {
                var t = sender as MultiUnitRtuServer;
                (byte unitId, var registers) = bz;
                Console.WriteLine($"regiters changed for unit: {unitId}");
                // the variable 'registerAddresses' contains a list of modified register addresses
            };

            /* create Modbus RTU client */
            var client = new ModbusRtuClient();

            /* run Modbus RTU server */
            var cts = new CancellationTokenSource();

            await Task.Run(async () =>
            {
                await server.Start(serverPort);
                serverLogger.LogInformation("Server started.");

                // lock is required to synchronize buffer access between this application and the Modbus client

                DoServerWork(server);
                serverLogger.LogInformation("Server updated.");

                // update server register content once per second
            });

            /* run Modbus RTU client */
            var task_client = Task.Run(() =>
            {
                serverLogger.LogInformation("connect client.");

                client.Connect(clientPort);
                serverLogger.LogInformation("connected.");

                try
                {
                    DoClientWork(client, clientLogger);
                }
                catch (Exception ex)
                {
                    clientLogger.LogError(ex.Message);
                }

                client.Close();

                Console.WriteLine("Tests finished. Press any key to continue.");
                Console.ReadKey(true);
            });

            // wait for client task to finish
            await task_client;

            serverLogger.LogInformation("Server stopped.");
            Console.ReadKey();
        }

        private static void DoServerWork(MultiUnitRtuServer server)
        {
            // Option A: normal performance version, more flexibility

            /* get buffer in standard form (Span<short>) */
            foreach (var item in new byte[] { 1, 2, 3 })
            {
                var registers = server.GetHoldingRegisters(item);
                registers.SetLittleEndian<short>(address: 5, 77);
            }
        }

        private static void DoClientWork(ModbusRtuClient client, ILogger logger)
        {
            var sleepTime = TimeSpan.FromMilliseconds(100);

            foreach (var item in new byte[] { 1, 2, 3 })
            {
                logger.LogInformation($"unit client:{item}");

                // ReadHoldingRegisters = 0x03,        // FC03
                var data = client.ReadHoldingRegisters<short>(item, 5, 1);
                logger.LogInformation("FC03 - ReadHoldingRegisters:" + item + " Done:" + data[0]);
            }
        }
    }
}