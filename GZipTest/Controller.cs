using System;
using System.IO;
using System.IO.Compression;

namespace GZipTest
{
    public class Controller
    {
        static void Main(string[] args)
        {
            if (!ValidArgs(args))
            {
                Console.WriteLine("Invalid arguments. Use: GZipTest.exe <compress/decompress> <Input file> <Output file>");
                return;
            }
            Controller controller;
            try
            {
                controller = new Controller(args[1], args[2]);
            }
            catch (Exception)
            {
                Console.ReadKey();
                return;
            }
            try
            {
                if (args[0].Equals("compress"))
                    controller.Compress();
                else
                    controller.Decompress();
            }
            catch (Exception) //TODO Not enought space
            {
                return;
            }

        }

        private static bool ValidArgs(string[] args)
        {
            if (args.Length != 3)
                return false;
            if (!args[0].Equals("compress") && !args[0].Equals("decompress"))
                return false;
            return true;
        }

        readonly string inFile, outFile;

        public Controller(string inFilePath, string outFilePath)
        {
            inFile = inFilePath;
            outFile = outFilePath;
            try
            {
                using (FileStream stream = new FileStream(inFile, FileMode.Open)) ;
                using (FileStream stream = new FileStream(outFile, FileMode.CreateNew)) ;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error! " + e.Message);
                throw;
            }
        }

        public void Compress()
        {

        }

        public void Decompress()
        {

        }
    }
}
