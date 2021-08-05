using System;
using System.Collections.Generic;
using System.IO.Ports;

namespace FluentModbus
{
    /// <summary>
    /// A Modbus RTU server supporting multiple units.
    /// </summary>
    public class ModbusMultiUnitRtuServer
    {
        /// <summary>
        /// stores unit numbers to be active
        /// </summary>
        private readonly byte[] units;

        private IModbusRtuSerialPort _serialPort;
        public Dictionary<byte, ModbusRtuServer> serversDict = new Dictionary<byte, ModbusRtuServer>();

        /// <summary>
        ///
        /// </summary>
        /// <param name="units"></param>
        /// <param name="baudRate"></param>
        /// <param name="handshake"></param>
        /// <param name="parity"></param>
        /// <param name="stopBits"></param>
        /// <param name="readTimeout"></param>
        /// <param name="writeTimeout"></param>
        public ModbusMultiUnitRtuServer(byte[] units, int baudRate = 9600, Handshake handshake = Handshake.None, Parity parity = Parity.None, StopBits stopBits = StopBits.Two, int readTimeout = 1000, int writeTimeout = 1000)
        {
            if (units is null) throw new ArgumentNullException(nameof(units));
            this.units = units;

            BaudRate = baudRate;
            Handshake = handshake;
            Parity = parity;
            StopBits = stopBits;
            ReadTimeout = readTimeout;
            WriteTimeout = writeTimeout;
        }

        /// <summary>
        /// Gets the connection status of the underlying serial port.
        /// </summary>
        public bool IsConnected => _serialPort != null ? _serialPort.IsOpen : false;

        /// <summary>
        /// Gets or sets the serial baud rate. Default is 9600.
        /// </summary>
        public int BaudRate { get; }

        /// <summary>
        /// Gets or sets the handshaking protocol for serial port transmission of data. Default is Handshake.None.
        /// </summary>
        public Handshake Handshake { get; }

        /// <summary>
        /// Gets or sets the parity-checking protocol. Default is Parity.Even.
        /// </summary>
        public Parity Parity { get; }

        /// <summary>
        /// Gets or sets the standard number of stopbits per byte. Default is StopBits.One.
        /// </summary>
        public StopBits StopBits { get; }

        /// <summary>
        /// Gets or sets the read timeout in milliseconds. Default is 1000 ms.
        /// </summary>
        public int ReadTimeout { get; }

        /// <summary>
        /// Gets or sets the write timeout in milliseconds. Default is 1000 ms.
        /// </summary>
        public int WriteTimeout { get; }

        /// <summary>
        /// Starts the server. It will listen on the provided <paramref name="port"/>.
        /// </summary>
        /// <param name="port">The COM port to be used, e.g. COM1.</param>
        public void Start(string port)
        {
            IModbusRtuSerialPort serialPort = new ModbusRtuSerialPort(new SerialPort(port)
            {
                BaudRate = this.BaudRate,
                Handshake = this.Handshake,
                Parity = this.Parity,
                StopBits = this.StopBits,
                ReadTimeout = this.ReadTimeout,
                WriteTimeout = this.WriteTimeout
            });

            _serialPort = serialPort;

            foreach (var unit in units)
            {
                var server = new ModbusRtuServer(unit, false)
                {
                    // those should be removed or something, they are not used in the internal start
                    BaudRate = this.BaudRate,
                    Handshake = this.Handshake,
                    Parity = this.Parity,
                    StopBits = this.StopBits,
                    ReadTimeout = this.ReadTimeout,
                    WriteTimeout = this.WriteTimeout
                };

                //this calls the internal start

                server.Start(serialPort);
                serversDict.Add(unit, server);
            }
        }

        /// <summary>
        /// Starts the server. It will listen on the provided <paramref name="port"/>.
        /// </summary>
        /// <param name="port">The COM port to be used, e.g. COM1.</param>
        internal void Start(IModbusRtuSerialPort serialPort)
        {
            _serialPort = serialPort;

            foreach (var unit in units)
            {
                var server = new ModbusRtuServer(unit)
                {
                    // those should be removed or something, they are not used in the internal start
                    BaudRate = this.BaudRate,
                    Handshake = this.Handshake,
                    Parity = this.Parity,
                    StopBits = this.StopBits,
                    ReadTimeout = this.ReadTimeout,
                    WriteTimeout = this.WriteTimeout
                };

                //this calls the internal start

                server.Start(serialPort);
                serversDict.Add(unit, server);
            }
        }

        /// <summary>
        /// Stops All servesr and closes the underlying serial port.
        /// </summary>
        public void StopAll()
        {
            foreach (var server in serversDict)
            {
                server.Value.Stop();
            }

            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }

        /// <summary>
        /// Stops the server and closes the underlying serial port.
        /// </summary>
        public void Stop(byte unit)
        {
            serversDict[unit].Stop();
        }
    }
}