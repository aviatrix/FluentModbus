using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using FluentModbus.ServerMultiUnit;

namespace FluentModbus.Tests
{
    public class MultiModbusRtuServerTests : IClassFixture<XUnitFixture>
    {
        private ITestOutputHelper _logger;

        public MultiModbusRtuServerTests(ITestOutputHelper logger)
        {
            _logger = logger;
        }

        [Fact]
        public async void ServerHandlesRequestFire()
        {
            // Arrange
            var serialPort = new FakeSerialPort();

            var server = new MultiUnitRtuServer(new byte[] { 1, 2, 3 }, true);

            server.Start(serialPort);

            var client = new ModbusRtuClient();
            client.Connect(serialPort);

            await Task.Run(() =>
            {
                var data = Enumerable.Range(0, 20).Select(i => (float)i).ToArray();
                var sw = Stopwatch.StartNew();
                var iterations = 10000;

                for (int i = 0; i < iterations; i++)
                {
                    client.WriteMultipleRegisters(0, 0, data);
                }

                var timePerRequest = sw.Elapsed.TotalMilliseconds / iterations;
                _logger.WriteLine($"Time per request: {timePerRequest * 1000:F0} us. Frequency: {1 / timePerRequest * 1000:F0} requests per second.");

                client.Close();
            });

            // Assert
        }

        [Fact]
        public async Task UpdateServerRegisters()
        {
            var serialPort = new FakeSerialPort();

            var server = new MultiUnitRtuServer(new byte[] { 1, 2, 3 }, true);

            await server.Start(serialPort);

            server.GetHoldingRegisters(1).SetLittleEndian<short>(address: 5, 1);
            server.GetHoldingRegisters(2).SetLittleEndian<short>(address: 5, 2);
            server.GetHoldingRegisters(3).SetLittleEndian<short>(address: 5, 3);

            var client = new ModbusRtuClient();
            client.Connect(serialPort);

            var results = new short[4];
            await Task.Run(() =>
           {
               results[1] = client.ReadHoldingRegisters<short>(1, 5, 2).ToArray()[0];

               results[2] = client.ReadHoldingRegisters<short>(2, 5, 2).ToArray()[0];

               results[3] = client.ReadHoldingRegisters<short>(3, 5, 2).ToArray()[0];
               client.Close();
           });

            Assert.Equal(1, results[1]);
            Assert.Equal(2, results[2]);
            Assert.Equal(3, results[3]);
        }
    }
}