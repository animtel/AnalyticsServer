using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace AnalyticsServer
{
    /// <summary>
    /// Server that processes incoming requests with game events
    /// </summary>
    class AnalyticsServer<T> where T : Serializer<Event>
    {
        private int _port;
        private TcpListener _listener;
        /// <summary>
        /// Manager of server for storing events
        /// </summary>
        public StorageManager<T> _manager;

        /// <summary>
        /// Initializes a new class that listens incoming requests with game events to the specified port
        /// </summary>
        public AnalyticsServer(int port)
        {
            _port = port;
            _listener = new TcpListener(IPAddress.Any, port);
            _manager = new StorageManager<T>("eventInfo");
            StartListening();
        }

        /// <summary>
        /// Stops the server if the listener was created
        /// </summary>
        ~AnalyticsServer()
        {
            _listener?.Stop();
            _manager.Flush();
        }

        /// <summary>
        /// Starts the server and accepts requests
        /// </summary>
        private void StartListening()
        {
            int maxThreads = Environment.ProcessorCount * 4;
            ThreadPool.SetMaxThreads(maxThreads, maxThreads);
            ThreadPool.SetMinThreads(2, 2);

            _listener.Start();
            Console.WriteLine($"The server started working at port {_port}");
            while (true)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(_manager.AcceptClient), _listener.AcceptTcpClient());
            }
        }
    }
}
