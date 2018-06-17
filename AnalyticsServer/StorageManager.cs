using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AnalyticsServer
{
    /// <summary>
    /// The class that stores and transfers to the server information about the game events
    /// </summary>
    /// <typeparam name="T">Type of serializer</typeparam>
    public class StorageManager<T> where T : Serializer<Event>
    {
        /// <summary>
        /// Common filestream for all threads
        /// </summary>
        private string _fullPath;

        private Serializer<Event> serializer = SerializerFactory<T, Event>.GetSerializer();

        /// <summary>
        /// Temporary storage of events
        /// </summary>
        private List<Event> _buffer = new List<Event>();

        private const int MIN_FLUSH_NUMBER = 4;

        /// <summary>
        /// Initializes a manager of event, that will store the events in file with specified name 
        /// and transmit them to server with specified Uri
        /// </summary>
        /// <param name="filename">Name of file</param>
        public StorageManager(string filename)
        {
            string extension = null;

            Type tType = typeof(T);

            if (tType == typeof(JSONSerializer<Event>))
            {
                extension = "json";
            }
            else if (tType == typeof(XMLSerializer<Event>))
            {
                extension = "xml";
            }
            else if (tType == typeof(CSVSerializer<Event>))
            {
                extension = "csv";
            }

            _fullPath = $"./{filename}.{extension}";

            if (!File.Exists(_fullPath))
                CreateNew();
        }

        /// <summary>
        /// Gets client's request info, gives him response and then stores data in the file
        /// </summary>
        /// <param name="currEvent">Current TCP client</param>
        public void AcceptClient(object client)
        {
            TcpClient tcpClient = (TcpClient)client;
            string requestData = GetClientInfo(tcpClient);

            string htmlResponse = "<html><body><h1>Server is working</h1></body></html>";
            string fullResponse = "HTTP/1.1 200 OK\r\nConnection:close\r\nContent-type: text/html\r\nContent-Length:" + htmlResponse.Length.ToString() + "\r\n\r\n" + htmlResponse;

            byte[] buffer = Encoding.ASCII.GetBytes(fullResponse);
            tcpClient.GetStream().Write(buffer, 0, buffer.Length);
            tcpClient.Close();

            if (requestData != null)
            {
                Console.WriteLine(WebUtility.HtmlDecode(requestData));
                string serializedData = GetPostData(requestData);

                // Incorrect data
                if (serializedData == null)
                    return;

                List<Event> receivedEvents = serializer.Deserialize(serializedData);

                if (receivedEvents == null)
                    return;

                lock (_buffer)
                {
                    _buffer.AddRange(receivedEvents);
                    if (_buffer.Count >= MIN_FLUSH_NUMBER)
                    {
                        Flush();
                    }
                }
            }
        }

        /// <summary>
        /// Clears the buffer and stores the events in the file
        /// </summary>
        public void Flush()
        {
            try
            {
                List<Event> eventList = null;
                string newFileText = null;

                lock (_fullPath)
                {
                    using (FileStream ReadingStream = new FileStream(_fullPath, FileMode.Open))
                    {
                        using (StreamReader reader = new StreamReader(ReadingStream))
                        {
                            string fileText = reader.ReadToEnd();

                            eventList = serializer.Deserialize(fileText);
                            eventList.AddRange(_buffer);
                            _buffer.Clear();

                            // Manager removes old events to store new ones

                            newFileText = serializer.Serialize(eventList);
                        }
                    }

                    using (FileStream WritingStream = new FileStream(_fullPath, FileMode.Open))
                    {
                        using (StreamWriter writer = new StreamWriter(WritingStream))
                        {
                            writer.AutoFlush = true;
                            writer.Write(newFileText);
                        }
                    }
                }
            }
            catch (System.Security.SecurityException)
            {
                Console.Out.WriteLine("File is already in use by another process");
            }
        }

        /// <summary>
        /// Fetches post data from HTTP request
        /// </summary>
        /// <param name="httpString">Full HTTP request text</param>
        /// <returns>Post data from request</returns>
        private string GetPostData(string httpString)
        {
            Regex reg = new Regex(@"(POST / HTTP/1.1)([\s\S]+)\n?\n([\s\S]+)");
            Match match = reg.Match(httpString);

            if (match == Match.Empty)
                return null;

            return match.Groups[3].Value;
        }

        private string GetClientInfo(TcpClient client)
        {
            try
            {
                string serializedString = "";
                byte[] buffer = new byte[1024];

                NetworkStream stream = client.GetStream();
                stream.ReadTimeout = 250;

                while (stream.DataAvailable)
                {
                    stream.Read(buffer, 0, buffer.Length);
                    serializedString += Encoding.ASCII.GetString(buffer);
                }
                return serializedString;
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Creates a new file for storing events
        /// </summary>
        private void CreateNew()
        {
            List<Event> emptyList = new List<Event>();
            string structure = serializer.Serialize(emptyList);

            using (FileStream stream = new FileStream(_fullPath, FileMode.Create))
            using (StreamWriter writer = new StreamWriter(stream))
            {
                writer.Write(structure);
            }
        }
    }
}
