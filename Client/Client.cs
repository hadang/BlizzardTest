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
    class Client
    {
        string ServerName { get; set; }
        int Port { get; set; }
        string ClientName { get; set; }
        static Thread listener = null;
        static TcpClient tcpClient = null;
        public Client(string server, int port, string client)
        {
            ServerName = server;
            Port = port;
            ClientName = client;
        }
        private static void ListenerService(object reader)
        {
            try
            {
                while (true)
                {
                    string serverMsg = ((StreamReader)reader).ReadLine();
                    string[] list = serverMsg.Split('|');
                    if (list.Count() > 1)
                    {
                        int code = -1;
                        if (!int.TryParse(list[0], out code))
                        {
                            Console.WriteLine("ERROR: unknown return code \"{0}\"", list[0]);
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
                Console.WriteLine("ERROR 1: {0}", e.Message);
                Environment.Exit(0);
            }
        }
        public void Start()
        {
           
            try
            {
                tcpClient = new TcpClient(ServerName, Port);
                using (Stream stream = tcpClient.GetStream())
                {
                    StreamReader reader = new StreamReader(stream);
                    StreamWriter writer = new StreamWriter(stream);
                    writer.AutoFlush = true;
                    //Register the client name to the server
                    writer.WriteLine(ClientName);
                    string firstMessage = reader.ReadLine();
                    string[] codes = firstMessage.Split('~');
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
                    listener = new Thread(new ParameterizedThreadStart(ListenerService));
                    listener.Start(reader);
                    while (true)
                    {
                        string command = Console.ReadLine();
                        writer.WriteLine(command);
                        if (command == Server.COMMAND_DISCONNECT)
                            break;

                    }
                    listener.Abort();
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
            if (args.Count() != 3)
            {
                Console.WriteLine("Usage: Client <server>:<port> username");
                return;
            }
            int port = -1;
            if (!int.TryParse(args[1], out port))
            {
                Console.WriteLine("<port> should be an integer");
                return;
            }
            Client client = new Client(args[0], port, args[2]);
            client.Start();
        }
    }
}
