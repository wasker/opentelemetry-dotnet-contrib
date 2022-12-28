// <copyright file="TcpSocketDataTransport.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Net;
using System.Net.Sockets;

namespace OpenTelemetry.Exporter.Geneva;

internal class TcpSocketDataTransport : IDataTransport, IDisposable
{
    public const int DefaultTimeoutMilliseconds = 15000;
    private readonly EndPoint networkEndpoint;
    private Socket socket;
    private int timeoutMilliseconds;

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpSocketDataTransport"/> class.
    /// The class for transporting data over Unix domain socket.
    /// </summary>
    /// <param name="host">Host name of the TCP endpoint.</param>
    /// <param name="port">Port of the TCP endpoint.</param>
    /// <param name="timeoutMilliseconds">
    /// The time-out value, in milliseconds.
    /// If you set the property with a value between 1 and 499, the value will be changed to 500.
    /// The default value is 15,000 milliseconds.
    /// </param>
    public TcpSocketDataTransport(
        string host,
        int port,
        int timeoutMilliseconds = DefaultTimeoutMilliseconds)
    {
        this.networkEndpoint = new DnsEndPoint(host, port);
        this.timeoutMilliseconds = timeoutMilliseconds;
        this.Connect();
    }

    public bool SupportsBatching => true;

    public bool IsEnabled()
    {
        return true;
    }

    public void Send(byte[] data, int size)
    {
        try
        {
            if (!this.socket.Connected)
            {
                // Socket connection is off! Server might have stopped. Trying to reconnect.
                this.Reconnect();
            }

            this.socket.Send(data, size, SocketFlags.None);
        }
        catch (Exception)
        {
            // Re-throw the exception so that Export method catches it and sets the ExportResult correctly.
            throw;
        }
    }

    public void Dispose()
    {
        this.socket.Dispose();
    }

    private void Connect()
    {
        try
        {
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                SendTimeout = this.timeoutMilliseconds,
            };
            this.socket.Connect(this.networkEndpoint);
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException("UDS Connect failed.", ex);

            // Re-throw the exception to
            // 1. fail fast in Geneva exporter contructor, or
            // 2. fail in the Reconnect attempt.
            throw;
        }
    }

    private void Reconnect()
    {
        this.socket.Close();
        this.Connect();
    }
}
