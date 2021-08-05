using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluentModbus.Tests
{
    public class ModbusMultiUnitRtuServerTests : IClassFixture<XUnitFixture>
    {
        private ITestOutputHelper _logger;

        public ModbusMultiUnitRtuServerTests(ITestOutputHelper logger)
        {
            _logger = logger;
        }

        [Fact]
        public async void ServerHandlesRequestFire()
        {
            // Arrange
            var serialPort = new FakeSerialPort();
            var units = new byte[] { 1, 2, 3 };
            var server = new ModbusMultiUnitRtuServer(units);
            server.Start(serialPort);

            var client = new ModbusRtuClient();
            client.Connect(serialPort);

            var data = Enumerable.Range(0, 20).Select(i => (float)i).ToArray();
            var sw = Stopwatch.StartNew();
            var iterations = 100;

            for (int i = 0; i < iterations; i++)
            {
                foreach (var u in units)
                {
                    client.WriteMultipleRegisters(u, 0, data);
                }
            }

            var timePerRequest = sw.Elapsed.TotalMilliseconds / iterations;
            _logger.WriteLine($"Time per request: {timePerRequest * 1000:F0} us. Frequency: {1 / timePerRequest * 1000:F0} requests per second.");

            // Assert
            foreach (var unit in units)
            {
                var resdata = await client.ReadHoldingRegistersAsync(unit, 1, 20);
                _logger.WriteLine($"unit {unit}: {string.Join(",", resdata)}");
            }
            client.Close();
        }
    }
}