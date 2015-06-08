using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace BlizzardTest
{
    /*BFile class to handle the loaded file from the local folder. It will have a fileName and a dictionary of word frequency.
     * 
     * */
    class BFile
    {
        public enum LOAD_STATUS
        {
            FILE_DOES_NOT_EXIST = 0,
            LOAD_ALREADY_LOADED = 1,
            LOAD_OK = 2,
            LOAD_FAILED = 3
        }
        public string FileName { get; set; }
        Dictionary<string, int> WordFrequencyList;
        public BFile()
        {
        }
        /*LoadFile to load the content of the given file to build a word frequency dictionary
         * 
         * */
        public LOAD_STATUS LoadFile(string fileName)
        {
            if (fileName.Equals(FileName, StringComparison.InvariantCultureIgnoreCase))
                return LOAD_STATUS.LOAD_ALREADY_LOADED;
            //reset the dictionary
            if (WordFrequencyList != null)
                WordFrequencyList.Clear();
            //double check the physical file
            if (!File.Exists(fileName))
            {
                return LOAD_STATUS.FILE_DOES_NOT_EXIST;
            }
            FileName = fileName;
            char[] separator = { ' ', '.', ',', ':', ';', '!', '(', ')', '?' };
            try
            {
                if (WordFrequencyList == null)
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
                return LOAD_STATUS.LOAD_OK;
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: {0}", e.Message);
                return LOAD_STATUS.LOAD_FAILED;
            }

        }
        public int Count(string word)
        {
            if (WordFrequencyList == null)
                return -1;
            if (WordFrequencyList.ContainsKey(word))
                return WordFrequencyList[word];
            else
                return 0;
        }
    }
}
