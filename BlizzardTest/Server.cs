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
    /* class Server to handle the server side. It will initiate a listener to listen for any connection request and handle that request with a TCP connection
     * Each TCP connection will be handled separately by a dedicate thread
     * Assume that the loaded file are shared among the clients.
     * */
    public class Server
    {
        public const int DEFAULT_PORT = 9090;
        public const int NAME_IS_USED = 1000;
        public const int CODE_CLIENT_LIST = 5000;
        public const char SEPARATOR = '|';
        public const string COMMAND_DISCONNECT = "disconnect";
        public const string COMMAND_LIST_CLIENT = "list_client";
        public const string COMMAND_LOAD_FILE = "load";
        public const string COMMAND_COUNT_WORD= "count";
        public const string COMMAND_SEND_MSG = "message";
        public const string COMMAND_GET_CURRENT = "get_current";
        
        private AutoResetEvent connectionWaitHandle = new AutoResetEvent(false);
        //Client List is stored in a dictonary where key is client name, value is the stream writer to communication to that client
        volatile Dictionary<string,StreamWriter> ClientList = null;
        //Assume that we are using a shared file across all user
        volatile BFile LoadedFile = null;
        
        private static Server serverIns;
        
        //There should be only one instance of server, we need a singleton instance
        //A private constructor to prevent creating multiple instances.
        private Server()
        {
            ClientList = new Dictionary<string, StreamWriter>();
            LoadedFile =new BFile();
        }
        //To initiate a server instance
        public static Server Instance()
        {
            if (serverIns == null)
            {
                serverIns = new Server();
            }
            return serverIns;
        }
        //
        static void Main(string[] args)
        {
            Server server = Server.Instance();
            server.Start();
        }
        void Start()
        {
            TcpListener listener = null;
            try
            {
                //the local address of the server is used
                IPAddress localAddress = IPAddress.Parse("127.0.0.1");
                listener = new TcpListener(localAddress, DEFAULT_PORT);
                listener.Start();
                Console.WriteLine("Blizzard Test Server has initialized at port {0}", DEFAULT_PORT);
                //Listening to connection requests from clients
                //Once there is a request, a new thread should be started to handle that connection
                while (true)
                {
                    IAsyncResult result = listener.BeginAcceptTcpClient(Service, listener);
                    //the autoresetevent is used to handle
                    connectionWaitHandle.WaitOne();
                    connectionWaitHandle.Reset();
                }
            }
            finally
            {
                listener.Stop();
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
                    //should use an escape mechanism to deal with it
                    clientName = clientName.Replace(SEPARATOR,'\0');
                    if (ClientList.Keys.Contains(clientName))
                    {
                        writer.WriteLine("{0}{1}The name \"{2}\" has been used, please register with another one", NAME_IS_USED, SEPARATOR,clientName);
                        if(tcpClient.Connected)
                            tcpClient.Close();
                        return;
                    }
                    Console.WriteLine("{0} is joining", clientName);
                    writer.WriteLine("Welcome {0}", clientName);
                    //prevent other clients from accessing the Client List
                    lock (ClientList) {
                        ClientList.Add(clientName, writer);
                    }
                    //communication with the connected client
                    bool runContinue = true;
                    while (runContinue)
                    {
                        string command = reader.ReadLine();
                        //suppose that the command lines are case sensitive, so we take the command line from client input as is, except for SEPARATOR character
                        command = command.Replace(SEPARATOR,'\0');
                        if (string.IsNullOrEmpty(command))
                            continue;
                        runContinue = ProcessCommand(writer, clientName, command);
                    }
                    tcpClient.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: {0}", e.Message);
            }
        }

        bool ProcessCommand(StreamWriter writer, string clientName,string command )
        {
            /*disconnect command 
             * remove the client from the client list
             * close TCP connection
             */
             
            if (command == COMMAND_DISCONNECT)
            {
                lock (ClientList)
                {
                    ClientList.Remove(clientName);
                }
                Console.WriteLine("{0} has left", clientName);
                return false;
            }
            /* list_current command
             * prevent other thread from modifying the client list using lock
             * build a string return, separated by a special character defined in SEPARATOR
             * unlock the client list to allow others to access to it
             * send the list to the client using stream writer
             * */
            else if (command == COMMAND_LIST_CLIENT)
            {
                //list the name from the client list, we may or may not use thread safe depending on applications
                StringBuilder list = new StringBuilder();
                lock (ClientList)
                {
                    list.AppendFormat("{0}{1}", CODE_CLIENT_LIST, SEPARATOR);
                    foreach (string client in ClientList.Keys)
                    {
                        list.AppendFormat("{0}{1}", client, SEPARATOR);
                    }
                }
                writer.WriteLine(list.ToString());
            }
                /*
                 * get_current command
                 * lock LoadedFile to get the file name out to prevent other clients from loading another file at the same time
                 * send the file name to the client if any. If not, notify the client to load a file
                 * */
            else if (command == COMMAND_GET_CURRENT)
            {
                string fileName = string.Empty;
                lock (LoadedFile)
                {
                    fileName = LoadedFile.FileName;
                }
                if (string.IsNullOrEmpty(fileName))
                    writer.WriteLine("No file is loaded");
                else
                    writer.WriteLine(fileName);

            }
            else
            {
                string[] input = command.Split(' ');
                if (input.Count() < 1)
                {
                    writer.WriteLine("Unknown command!");
                    return true;
                }
                else
                {
                    string newCommand = input[0];
                    /*load <filename> command
                     * lock the LoadedFile
                     * load a new file from local folder
                     * build a dictionay for word frequency. 
                     * The dictionary is used for store words along with their appearances in the loaded file to utilize the accessing time of O(1) for the count <word> command
                     * */
                    if (newCommand == COMMAND_LOAD_FILE)
                    {
                        string fileName = command.Remove(0, newCommand.Length).Trim();
                        BFile.LOAD_STATUS status = BFile.LOAD_STATUS.LOAD_OK;
                        lock (LoadedFile)
                        {
                            status = LoadedFile.LoadFile(fileName);
                        }
                        switch (status)
                        {
                            case BFile.LOAD_STATUS.LOAD_FAILED:
                                break;
                            case BFile.LOAD_STATUS.FILE_DOES_NOT_EXIST:
                                writer.WriteLine("\"{0}\" does not exist", fileName);
                                LoadedFile.FileName = string.Empty;
                                break;
                            case BFile.LOAD_STATUS.LOAD_ALREADY_LOADED:
                                writer.WriteLine("\"{0}\" is already loaded", fileName);
                                break;
                            case BFile.LOAD_STATUS.LOAD_OK:
                                writer.WriteLine("\"{0}\" has been loaded!", fileName);
                                break;
                        }
                    }
                        /*count <word> command
                         * double check if any file is loaded, notify the client if not.
                         * if the word to count is in the dictionary, return the count. Otherwise return 0.
                         * */
                    else if (newCommand == COMMAND_COUNT_WORD)
                    {
                        string wordToCount = input[1];
                        int count = -1;
                        lock (LoadedFile)
                            count = LoadedFile.Count(wordToCount);
                        if (count == -1)
                        {
                            writer.WriteLine("Please load a file first");
                            return true;
                        }
                        else
                            writer.WriteLine(count);

                    }
                        /*Get the client to send message to from the Client List, take its stream writer out to write the message to it
                         * if the client is not found, notify the client
                         * */
                    else if (newCommand == COMMAND_SEND_MSG)
                    {
                        string otherClient = input[1];
                        StreamWriter otherWriter = null;
                        lock (ClientList)
                        {
                            if (ClientList.Keys.Contains(otherClient))
                                otherWriter = ClientList[otherClient];
                        }
                        if (otherWriter == null)
                        {
                            writer.WriteLine("Could not find user \"{0}\"", otherClient);
                            return true;
                        }
                        //extract the message from the command line
                        string message = command.Remove(0, otherClient.Length + newCommand.Length + 1).Trim();
                        otherWriter.WriteLine("{0}: {1}", clientName, message);
                        writer.WriteLine("Message sent");
                    }
                    else
                    {
                        writer.WriteLine("Unknown command!");
                    }
                }

            }
            return true;
        }
      
    }
}
