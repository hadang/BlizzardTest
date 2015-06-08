using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
namespace BlizzardTest
{
    public class Server
    {
        public const int DEFAULT_PORT = 9090;
        public const int NAME_IS_USED = 1000;
        public const int CODE_CLIENT_LIST = 5000;
        public const string COMMAND_DISCONNECT = "disconnect";
        public const string COMMAND_LIST_CLIENT = "list_client";
        public const string COMMAND_LOAD_FILE = "load";
        public const string COMMAND_COUNT_WORD= "count";
        public const string COMMAND_SEND_MSG = "message";
        
        private AutoResetEvent connectionWaitHandle = new AutoResetEvent(false);
        Dictionary<string,StreamWriter> ClientList = null;
        BFile LoadedFile = null;
        private TcpListener Listener = null;
        private static Server serverIns;
        

        //There should be only one instance of server, we need a singleton instance
        //A private constructor to prevent creating multiple instances.
        private Server()
        {
            ClientList = new Dictionary<string, StreamWriter>();
            LoadedFile =new BFile();
        }
        void Start()
        {
            try
            {
                IPAddress localAddress = IPAddress.Parse("127.0.0.1");
                Listener = new TcpListener(localAddress, DEFAULT_PORT);
                Listener.Start();
                Console.WriteLine("Blizzard Test Server has initialized at port {0}", DEFAULT_PORT);
                //Listening to connection requests from clients
                //Once there is a request, a new thread should be started to handle that connection
                while (true)
                {
                    IAsyncResult result = Listener.BeginAcceptTcpClient(Service, Listener);
                    //the autoresetevent is used to handle
                    connectionWaitHandle.WaitOne();
                    connectionWaitHandle.Reset();
                }
            }
            finally
            {
                Listener.Stop();
            }
        }
        //implemetation of the async function to handle the connection to each client
        void Service(IAsyncResult result)
        {
            TcpListener listener = (TcpListener)result.AsyncState;
            TcpClient tcpClient = listener.EndAcceptTcpClient(result);
            connectionWaitHandle.Set();
            try
            {
                using (Stream stream = tcpClient.GetStream())
                {
                    StreamReader reader = new StreamReader(stream);
                    StreamWriter writer = new StreamWriter(stream);
                    writer.AutoFlush = true;
                    //read the the first text from client, it is the registered name 
                    string clientName = reader.ReadLine();
                    if (ClientList.Keys.Contains(clientName))
                    {
                        writer.WriteLine("{0}~The name \"{1}\" has been used, please register another one", NAME_IS_USED,clientName);
                        if(tcpClient.Connected)
                            tcpClient.Close();
                        return;
                    }
                    Console.WriteLine("{0} is joining", clientName);
                    writer.WriteLine("Welcome {0}", clientName);
                    //thread safe
                    lock (ClientList) {
                        ClientList.Add(clientName, writer);
                    }
                    //communication with the connected client
                    while (true)
                    {
                        string command = reader.ReadLine();
                        //suppose that the command lines are case sensitive, so we take the command line from client input as is
                        if (string.IsNullOrEmpty(command))
                            continue;
                        if (command == COMMAND_DISCONNECT)
                        {
                            lock (ClientList)
                            {
                                ClientList.Remove(clientName);
                            }
                            Console.WriteLine("{0} has left", clientName);
                            break;
                        }
                        else if (command == COMMAND_LIST_CLIENT)
                        {
                            //list the name from the client list, we may or may not use thread safe depending on applications
                            StringBuilder list = new StringBuilder();
                            lock (ClientList)
                            {
                                list.AppendFormat("{0}|",CODE_CLIENT_LIST);
                                foreach (string client in ClientList.Keys)
                                {
                                    list.AppendFormat("{0}|", client);
                                }
                            }
                            writer.WriteLine(list.ToString());
                        }
                        else
                        {
                            string[] input = command.Split(' ');
                            if (input.Count() < 1)
                            {
                                writer.WriteLine("Unknown command!");
                                continue;
                            }
                            else
                            {
                                string newCommand = input[0];
                                if (newCommand == COMMAND_LOAD_FILE)
                                {
                                    string fileName = command.Remove(0, newCommand.Length).Trim();
                                    int code = BFile.LOAD_OK;
                                    lock (LoadedFile)
                                    {
                                        code = LoadedFile.LoadFile(fileName);
                                    }
                                    switch(code){
                                        case BFile.LOAD_FAILED:
                                            break;
                                        case BFile.FILE_DOES_NOT_EXIST:
                                            writer.WriteLine("\"{0}\" does not exist", fileName);
                                            break;
                                        case BFile.LOAD_ALREADY_LOADED:
                                            writer.WriteLine("\"{0}\" already loaded", fileName);
                                            break;
                                        case BFile.LOAD_OK:
                                            writer.WriteLine("\"{0}\" has been loaded!", fileName);
                                            break;
                                    }
                                }
                                else if (newCommand == COMMAND_COUNT_WORD)
                                {
                                    string wordToCount = input[1];
                                    int count = -1;
                                    lock(LoadedFile)
                                         count = LoadedFile.Count(wordToCount);
                                    if(count == -1){
                                        writer.WriteLine("Please load a file first");
                                        continue;
                                    }
                                    else
                                        writer.WriteLine(count);

                                }
                                else if (newCommand == COMMAND_SEND_MSG)
                                {
                                    string otherClient = input[1];
                                    StreamWriter otherWriter = null;
                                    lock (ClientList)
                                    {
                                        if(ClientList.Keys.Contains(otherClient))
                                            otherWriter = ClientList[otherClient];
                                    }
                                    if (otherWriter == null)
                                    {
                                        writer.WriteLine("Could not find user \"{0}\"", otherClient);
                                        continue;
                                    }
                                    //extract the message from the command line
                                    string message = command.Remove(0, otherClient.Length + newCommand.Length + 1).Trim();
                                    otherWriter.WriteLine("{0}: {1}", clientName, message);
                                    writer.WriteLine("Message sent");
                                }
                                else
                                {
                                    writer.WriteLine("Unknown command!");
                                    continue;
                                }
                            }

                        }
                    }
                    tcpClient.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: {0}", e.Message);
            }
        }
        
        public static Server Instance()
        {
            if (serverIns == null)
            {
                serverIns = new Server();
            }
            return serverIns;
        }

        static void Main(string[] args)
        {
            Server server = Server.Instance();
            server.Start();
        }
    }

    class BFile
    {
        public const int FILE_DOES_NOT_EXIST =2000;
        public const int LOAD_OK = 2001;
        public const int LOAD_ALREADY_LOADED = 2002;
        public const int LOAD_FAILED = 2003;
        string FileName; 
        Dictionary<string, int> WordFrequencyList;
        public BFile()
        {
            
        }
        public int LoadFile(string fileName)
        {
            if (fileName.Equals(FileName, StringComparison.InvariantCultureIgnoreCase))
                return LOAD_ALREADY_LOADED;
            //reset the dictionary
            if (WordFrequencyList != null)
                WordFrequencyList.Clear();
            //double check the physical file
            if (!File.Exists(fileName))
            {
                return FILE_DOES_NOT_EXIST;
            }
            FileName = fileName;
            char[] separator = { ' ', '.', ',', ':', ';', '!', '(', ')', '?' };
            try
            {
                if(WordFrequencyList == null)
                    WordFrequencyList = new Dictionary<string, int>();
                using (StreamReader reader = new StreamReader(fileName))
                {
                    string line = null;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] words = line.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string word in words)
                        {
                            if (string.IsNullOrWhiteSpace(word))
                                continue;
                            string nw = word.ToLowerInvariant();
                            if (WordFrequencyList.ContainsKey(nw))
                                WordFrequencyList[nw]++;
                            else
                                WordFrequencyList.Add(nw, 1);
                        }
                    }
                }
                return LOAD_OK;
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: {0}", e.Message);
                return LOAD_FAILED;
            }

        }
        public int Count(string word)
        {
            if(WordFrequencyList == null)
                return -1;
            if(WordFrequencyList.ContainsKey(word))    
                return WordFrequencyList[word];
            else 
                return 0;
        }
    }
}
