using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

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

            _manualResetEvent = new ManualResetEventSlim(false);
        }

        #region Properties

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

        private ModbusMultiUnitRtuRequestHandler RequestHandler { get; set; }
        public CancellationTokenSource CTS { get; private set; } = new CancellationTokenSource();

        public object Lock = new object();
        private Task _task_process_requests;
        private ManualResetEventSlim _manualResetEvent;

        #endregion Properties

        /// <summary>
        /// Starts the server. It will listen on the provided <paramref name="port"/>.
        /// </summary>
        /// <param name="port">The COM port to be used, e.g. COM1.</param>
        public void Start(string port, bool async)
        {
            Console.WriteLine("public void Start");
            IModbusRtuSerialPort serialPort = new ModbusRtuSerialPort(new SerialPort(port)
            {
                BaudRate = this.BaudRate,
                Handshake = this.Handshake,
                Parity = this.Parity,
                StopBits = this.StopBits,
                ReadTimeout = this.ReadTimeout,
                WriteTimeout = this.WriteTimeout
            });

            Lock = this;

            Start(serialPort, async);
        }

        /// <summary>
        /// Starts the server. It will listen on the provided <paramref name="port"/>.
        /// </summary>
        /// <param name="serialPort">The COM port to be used, e.g. COM1.</param>
        /// <param name="async"></param>
        internal void Start(IModbusRtuSerialPort serialPort, bool async)
        {
            _serialPort = serialPort;
            RequestHandler = new ModbusMultiUnitRtuRequestHandler(serialPort, serversDict, async);

            foreach (var unit in units)
            {
                var server = new ModbusRtuServer(unit, async)
                {
                    // those should be removed or something, they are not used in the internal start
                    BaudRate = this.BaudRate,
                    Handshake = this.Handshake,
                    Parity = this.Parity,
                    StopBits = this.StopBits,
                    ReadTimeout = this.ReadTimeout,
                    WriteTimeout = this.WriteTimeout,
                };

                //this calls the internal start

                server.Start(serialPort);
                serversDict.Add(unit, server);
            }

            StartProcessing(async);
        }

        /// <summary>
        /// Starts the server operation.
        /// </summary>

        public void StartProcessing(bool isAsync)
        {
            Console.WriteLine("public void StartProcessing(bool isAsync)");
            this.CTS = new CancellationTokenSource();

            if (!isAsync)
            {
                // only process requests when it is explicitly triggered
                _task_process_requests = Task.Run(() =>
                {
                    Console.WriteLine("_task_process_requests = Task.Run(()");

                    Console.WriteLine("_manualResetEvent.Wait(this.CTS.Token);");
                    _manualResetEvent.Wait(this.CTS.Token);

                    while (!this.CTS.IsCancellationRequested)
                    {
                        Console.WriteLine("Processing Request");
                        this.ProcessRequests();
                        Console.WriteLine("Done Processing Request");

                        _manualResetEvent.Reset();
                        _manualResetEvent.Wait(this.CTS.Token);
                    }
                }, this.CTS.Token);
            }
        }

        ///<inheritdoc/>
        private void ProcessRequests()
        {
            lock (this.Lock)
            {
                if (this.RequestHandler.IsReady)
                {
                    var unitId = this.RequestHandler.ReceiveRequestAsync().Result;

                    if (this.RequestHandler.Length > 0)
                        this.RequestHandler.WriteResponse(unitId);
                }
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
        /// Stops the server and removes unit from dict.
        /// </summary>
        public void Stop(byte unit)
        {
            // TODO: should we even have single unit stop?
            // what are the edge cases?
            serversDict[unit].Stop();
            serversDict.Remove(unit);
        }
    }
}