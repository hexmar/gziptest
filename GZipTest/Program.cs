using System;
using System.IO;

namespace GZipTest
{
    static class Program
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
                return;
            }
            try
            {
                if (args[0].Equals("compress"))
                    controller.Compress();
                else
                    controller.Decompress();
            }
            catch (Exception e)
            {
                Console.Write("Error! " + e.Message);
                File.Delete(args[2]);
                return;
            }
            Console.WriteLine("Done");
        }

        private static bool ValidArgs(string[] args)
        {
            if (args.Length != 3)
                return false;
            if (!args[0].Equals("compress") && !args[0].Equals("decompress"))
                return false;
            return true;
        }
    }
}
