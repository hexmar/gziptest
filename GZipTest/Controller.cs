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

        static int threadCount = Environment.ProcessorCount;

        const int dataBlockSize = 0x100000;

        readonly string inFile, outFile;
        Worker[] workers;

        public Controller(string inFilePath, string outFilePath)
        {
            inFile = inFilePath;
            outFile = outFilePath;
            try
            {
                using (FileStream stream = new FileStream(inFile, FileMode.Open));
                using (FileStream stream = new FileStream(outFile, FileMode.CreateNew));
            }
            catch (Exception e)
            {
                Console.Write("Error! " + e.Message);
                throw;
            }
        }

        public void Compress()
        {
            using (var outStream = new FileStream(outFile, FileMode.Append))
            {
                byte[] compressed;
                workers = new Worker[threadCount];
                for (int i = 0, position = 0; i < threadCount; i++, position += dataBlockSize)
                    workers[i] = new Worker(inFile, position, CompressionMode.Compress, i, this);
                for (int thread = 0; !workers[thread].EndOfFile; thread = (thread + 1) == threadCount ? 0 : thread + 1)
                {
                    workers[thread].WaitData();
                    compressed = new byte[workers[thread].Compressed.Length];
                    workers[thread].Compressed.CopyTo(compressed, 0);
                    workers[thread].GetData();
                    try
                    {
                        outStream.Write(compressed, 0, compressed.Length);
                    }
                    catch (Exception)
                    {
                        for (int i = 0; i < threadCount; i++)
                        {
                            workers[i].Abort = true;
                            workers[i].GetData();
                        }
                        throw;
                    }
                }
            }
        }

        public void Decompress()
        {
            using (var outStream = new FileStream(outFile, FileMode.Append))
            {
                byte[] decompressed;
                workers = new Worker[threadCount];
                for (int i = 0; i < threadCount; i++)
                    workers[i] = new Worker(inFile, 0, CompressionMode.Decompress, i, this);
                workers[0].SetPosition(0);
                for (int thread = 0; !workers[thread].EndOfFile; thread = (thread + 1) == threadCount ? 0 : thread + 1)
                {
                    workers[thread].WaitData();
                    decompressed = new byte[workers[thread].Data.Length];
                    workers[thread].Data.CopyTo(decompressed, 0);
                    workers[thread].GetData();
                    try
                    {
                        outStream.Write(decompressed, 0, decompressed.Length);
                    }
                    catch (Exception)
                    {
                        for (int i = 0; i < threadCount; i++)
                        {
                            workers[i].Abort = true;
                            workers[i].GetData();
                        }
                        throw;
                    }
                }
            }
        }

        public void SetNextPosition(int thread, long position)
        {
            workers[(thread + 1) == threadCount ? 0 : (thread + 1)].SetPosition(position);
        }
    }
}
