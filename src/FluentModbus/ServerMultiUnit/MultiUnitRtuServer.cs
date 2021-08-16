﻿using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FluentModbus.ServerMultiUnit
{
    /// <summary>
    ///
    /// </summary>
    public class MultiUnitRtuServer
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="units"></param>
        /// <param name="isAsynchronous"></param>
        /// <param name="baudRate"></param>
        /// <param name="handshake"></param>
        /// <param name="parity"></param>
        /// <param name="stopBits"></param>
        /// <param name="readTimeout"></param>
        /// <param name="writeTimeout"></param>
        public MultiUnitRtuServer(byte[] units, bool isAsynchronous, int baudRate = 9600, Handshake handshake = Handshake.None, Parity parity = Parity.Even, StopBits stopBits = StopBits.One, int readTimeout = 1000, int writeTimeout = 1000)
        {
            this.Units = units;

            this.IsAsynchronous = isAsynchronous;

            this.MaxInputRegisterAddress = ushort.MaxValue;
            this.MaxHoldingRegisterAddress = ushort.MaxValue;
            this.MaxCoilAddress = ushort.MaxValue;
            this.MaxDiscreteInputAddress = ushort.MaxValue;

            _inputRegisterSize = (this.MaxInputRegisterAddress + 1) * 2;
            _holdingRegisterSize = (this.MaxHoldingRegisterAddress + 1) * 2;
            _coilSize = (this.MaxCoilAddress + 1 + 7) / 8;
            _discreteInputSize = (this.MaxDiscreteInputAddress + 1 + 7) / 8;

            foreach (var unit in units)
            {
                _inputRegisterBuffer.Add(unit, new byte[_inputRegisterSize]);

                _holdingRegisterBuffer.Add(unit, new byte[_holdingRegisterSize]);

                _coilBuffer.Add(unit, new byte[_coilSize]);

                _discreteInputBuffer.Add(unit, new byte[_discreteInputSize]);
            }

            BaudRate = baudRate;
            Handshake = handshake;
            Parity = parity;
            StopBits = stopBits;
            ReadTimeout = readTimeout;
            WriteTimeout = writeTimeout;
        }

        #region Events

        /// <summary>
        /// Occurs after one or more registers changed.
        /// </summary>
        public event EventHandler<(byte, List<int>)> RegistersChanged;

        /// <summary>
        /// Occurs after one or more coils changed.
        /// </summary>
        public event EventHandler<(byte, List<int>)> CoilsChanged;

        #endregion Events

        #region Fields

        private Dictionary<byte, byte[]> _inputRegisterBuffer = new();
        private Dictionary<byte, byte[]> _holdingRegisterBuffer = new();
        private Dictionary<byte, byte[]> _coilBuffer = new();
        private Dictionary<byte, byte[]> _discreteInputBuffer = new();

        private int _inputRegisterSize;
        private int _holdingRegisterSize;
        private int _coilSize;
        private int _discreteInputSize;
        public readonly byte[] Units;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets the operation mode.
        /// </summary>
        public bool IsAsynchronous { get; }

        /// <summary>
        /// Gets the maximum input register address.
        /// </summary>
        public UInt16 MaxInputRegisterAddress { get; }

        /// <summary>
        /// Gets the maximum holding register address.
        /// </summary>
        public UInt16 MaxHoldingRegisterAddress { get; }

        /// <summary>
        /// Gets the maximum coil address.
        /// </summary>
        public UInt16 MaxCoilAddress { get; }

        /// <summary>
        /// Gets the maximum discrete input address.
        /// </summary>
        public UInt16 MaxDiscreteInputAddress { get; }

        /// <summary>
        /// Gets or sets a method that validates each client request.
        /// </summary>
        public Func<ModbusFunctionCode, ushort, ushort, ModbusExceptionCode> RequestValidator { get; set; }

        /// <summary>
        /// Gets or sets whether the events should be raised when register or coil data changes. Default: false.
        /// </summary>
        public bool EnableRaisingEvents { get; set; }

        //private protected bool IsReady
        //{
        //    get
        //    {
        //        return !_manualResetEvent.Wait(TimeSpan.Zero);
        //    }
        //}

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

        public MultiModbusRtuRequestHandler RequestHandler { get; private set; }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Starts the server. It will listen on the provided <paramref name="port"/>.
        /// </summary>
        /// <param name="port">The COM port to be used, e.g. COM1.</param>
        public async Task Start(string port)
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

            await Start(serialPort);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="serialPort"></param>
        public async Task Start(IModbusRtuSerialPort serialPort)
        {
            if (this.Parity == Parity.None && this.StopBits != StopBits.Two)
                throw new InvalidOperationException(ErrorMessage.Modbus_NoParityRequiresTwoStopBits);

            // "base..." is important!
            this.RequestHandler = new MultiModbusRtuRequestHandler(serialPort, this);

            //this.RequestHandler.Stop();

            await this.RequestHandler.BaseStart();
        }

        /// <summary>
        /// Gets the input register as <see cref="UInt16"/> array.
        /// </summary>
        public Span<short> GetInputRegisters(byte unitId)
        {
            return MemoryMarshal.Cast<byte, short>(this.GetInputRegisterBuffer(unitId));
        }

        /// <summary>
        /// Gets the input register buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        public Span<T> GetInputRegisterBuffer<T>(byte unitId) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetInputRegisterBuffer(unitId));
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the input register buffer as byte array.
        /// </summary>
        public Span<byte> GetInputRegisterBuffer(byte unitId)
        {
            return _inputRegisterBuffer[unitId];
        }

        /// <summary>
        /// Gets the holding register as <see cref="UInt16"/> array.
        /// </summary>
        public Span<short> GetHoldingRegisters(byte unitId)
        {
            return MemoryMarshal.Cast<byte, short>(this.GetHoldingRegisterBuffer(unitId));
        }

        /// <summary>
        /// Gets the holding register buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        public Span<T> GetHoldingRegisterBuffer<T>(byte unitId) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetHoldingRegisterBuffer(unitId));
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the holding register buffer as byte array.
        /// </summary>
        public Span<byte> GetHoldingRegisterBuffer(byte unitId)
        {
            return _holdingRegisterBuffer[unitId];
        }

        /// <summary>
        /// Gets the coils as <see cref="byte"/> array.
        /// </summary>
        public Span<byte> GetCoils(byte unitId)
        {
            return this.GetCoilBuffer(unitId);
        }

        /// <summary>
        /// Gets the coil buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        public Span<T> GetCoilBuffer<T>(byte unitId) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetCoilBuffer(unitId));
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the coil buffer as byte array.
        /// </summary>
        public Span<byte> GetCoilBuffer(byte unitId)
        {
            return _coilBuffer[unitId];
        }

        /// <summary>
        /// Gets the discrete inputs as <see cref="byte"/> array.
        /// </summary>
        public Span<byte> GetDiscreteInputs(byte unitId)
        {
            return this.GetDiscreteInputBuffer(unitId);
        }

        /// <summary>
        /// Gets the discrete input buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        public Span<T> GetDiscreteInputBuffer<T>(byte unitId) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetDiscreteInputBuffer(unitId));
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the discrete input buffer as byte array.
        /// </summary>
        public Span<byte> GetDiscreteInputBuffer(byte unitId)
        {
            return _discreteInputBuffer[unitId];
        }

        /// <summary>
        /// Clears all buffer contents.
        /// </summary>
        public void ClearBuffers(byte unitId)
        {
            this.GetInputRegisterBuffer(unitId).Clear();
            this.GetHoldingRegisterBuffer(unitId).Clear();
            this.GetCoilBuffer(unitId).Clear();
            this.GetDiscreteInputBuffer(unitId).Clear();
        }

        internal void OnRegistersChanged(byte unitId, List<int> registers)
        {
            this.RegistersChanged?.Invoke(this, (unitId, registers));
        }

        internal void OnCoilsChanged(byte unitId, List<int> coils)
        {
            this.CoilsChanged?.Invoke(this, (unitId, coils));
        }

        #endregion Methods

        #region IDisposable Support

        private bool disposedValue = false;

        /// <summary>
        /// Disposes the <see cref="ModbusServer"/> and frees all managed and unmanaged resources.
        /// </summary>
        /// <param name="disposing">A value indicating if the finalizer or the dispose method triggered the dispose process.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing) this.RequestHandler.Stop();

                disposedValue = true;
            }
        }

        /// <summary>
        /// Disposes the <see cref="ModbusServer"/> and frees all managed and unmanaged resources.
        /// </summary>
        ~MultiUnitRtuServer()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes the buffers.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion IDisposable Support
    }
}