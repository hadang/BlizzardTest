using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace BlizzardTest
{
    /*
     * The client class to work on the client side
     * It has two threads, the main thread is to etablish connection with the server and take commands from the Console (user input)
     * The second thread is to handle incoming messages from server
     */
    class Client
    {
        string ServerName { get; set; }
        int Port { get; set; }
        string ClientName { get; set; }
        private volatile static bool threadRunning = true;
        public Client(string server, int port, string client)
        {
            ServerName = server;
            Port = port;
            ClientName = client;
        }
        //the second thread, handling the incoming message from server
        private static void ListenerService(object reader)
        {
            try
            {
                while (threadRunning)
                {
                    string serverMsg = ((StreamReader)reader).ReadLine();
                    if (string.IsNullOrEmpty(serverMsg))
                        break;
                    string[] list = serverMsg.Split(Server.SEPARATOR);
                    if (list.Count() > 1)
                    {
                        int code = -1;
                        if (!int.TryParse(list[0], out code))
                        {
                            Console.WriteLine("ERROR: unknown return code \"{0}\"", list[0]);
                            continue;
                        }
                        if (code == Server.CODE_CLIENT_LIST)
                        {
                            for (int i = 1; i < list.Count(); i++)
                            {
                                if (string.IsNullOrEmpty(list[i])) continue;
                                Console.WriteLine(list[i]);
                            }
                        }
                    }
                    else
                        Console.WriteLine(serverMsg);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("LISTNER ERROR: {0}", e.Message);
            }
        }
        //The main thread, to establish connection to the provided server and take commands from Console
        public void Start()
        {
            try
            {
                //Establish a TCP connection with the server, using name and port
                TcpClient tcpClient = new TcpClient(ServerName, Port);
                using (Stream stream = tcpClient.GetStream())
                {
                    //reader stream and writer stream over the network stream
                    StreamReader reader = new StreamReader(stream);
                    StreamWriter writer = new StreamWriter(stream);
                    writer.AutoFlush = true;
                    //Register the client name to the server
                    writer.WriteLine(ClientName);
                    string firstMessage = reader.ReadLine();
                    string[] codes = firstMessage.Split(Server.SEPARATOR);
                    if (codes.Count() > 1)
                    {
                        if (codes[0] == string.Format("{0}", Server.NAME_IS_USED))
                        {
                            Console.WriteLine(codes[1]);
                            tcpClient.Close();
                            return;
                        }
                    }
                    //Display the welcome message
                    Console.WriteLine(firstMessage);
                    //start a dedicated thread to handle message coming from server
                    Thread listenerThread = new Thread(new ParameterizedThreadStart(ListenerService));
                    listenerThread.Start(reader);
                    while (true)
                    {
                        string command = Console.ReadLine();
                        writer.WriteLine(command);
                        if (command == Server.COMMAND_DISCONNECT)
                            break;

                    }
                    //stop the listener thread
                    if (listenerThread.IsAlive)
                    {
                        threadRunning = false;
                        Thread.Sleep(1);
                        listenerThread.Join();
                    }
                    tcpClient.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: {0}", e.Message);
                Environment.Exit(0);
            }
           
        }
        static void Main(string[] args)
        {
            if (args.Count() != 2)
            {
                Usage();
                return;
            }
            int port = -1;
            string[] serverArgs = args[0].Split(':');
            if (serverArgs.Count() != 2)
            {
                Usage();
                return;
            }
            if (!int.TryParse(serverArgs[1], out port))
            {
                Console.WriteLine("<port> should be an integer");
                return;
            }
            Client client = new Client(serverArgs[0], port, args[1]);
            client.Start();
        }
        static void Usage()
        {
            Console.WriteLine("Usage: Client <server>:<port> username");
        }
    }
}
