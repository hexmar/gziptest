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
                return;
            }
            if (args[0].Equals("compress"))
                controller.Compress();
            else
                controller.Decompress();
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

        static int threadCount = Environment.ProcessorCount;

        const int dataBlockSize = 0x100000;

        readonly string inFile, outFile;

        byte[][] data;
        byte[][] compressed;

        public Controller(string inFilePath, string outFilePath)
        {
            inFile = inFilePath;
            outFile = outFilePath;
            try
            {
                using (FileStream stream = new FileStream(inFile, FileMode.Open));
                using (FileStream stream = new FileStream(outFile, FileMode.Truncate));
            }
            catch (Exception e)
            {
                Console.WriteLine("Error! " + e.Message);
                throw;
            }
            data = new byte[threadCount][];
            compressed = new byte[threadCount][];
        }

        public void Compress()
        {
            using (FileStream inStream = new FileStream(inFile, FileMode.Open, FileAccess.Read,
                                                        FileShare.Read, 0x200000,
                                                        FileOptions.SequentialScan),
                              outStream = new FileStream(outFile, FileMode.Append))
            {
                int blockSize;
                Worker[] workers = new Worker[threadCount];
                for (int i = 0; i < threadCount; i++)
                    workers[i] = new Worker(CompressionMode.Compress);

                Console.WriteLine("Compressing...");

                while (inStream.Position < inStream.Length)
                {
                    for (int block = 0;
                         (block < threadCount) && (inStream.Position < inStream.Length);
                         block++)
                    {
                        if (inStream.Length - inStream.Position < dataBlockSize)
                            blockSize = (int)(inStream.Length - inStream.Position);
                        else
                            blockSize = dataBlockSize;
                        data[block] = new byte[blockSize];
                        inStream.Read(data[block], 0, blockSize);
                        workers[block].Data = data[block];
                        workers[block].SetData();
                    }
                    for (int block = 0; (block < threadCount) && (data[block] != null); block++)
                    {
                        workers[block].WaitData();
                        compressed[block] = workers[block].Compressed;
                        outStream.Write(compressed[block], 0, compressed[block].Length);
                        data[block] = null;
                    }
                }

                for (int i = 0; i < threadCount; i++)
                {
                    workers[i].Data = null;
                    workers[i].SetData();
                }
            }
        }

        public void Decompress()
        {

        }
    }
}
