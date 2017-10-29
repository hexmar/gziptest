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
            using (FileStream inStream = new FileStream(inFile, FileMode.Open, FileAccess.Read,
                                                        FileShare.Read, 0x200000,
                                                        FileOptions.SequentialScan),
                              outStream = new FileStream(outFile, FileMode.Append))
            {
                int blockSize;
                byte[] data, compressed;
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
                        data = new byte[blockSize];
                        inStream.Read(data, 0, blockSize);
                        workers[block].Data = data;
                        workers[block].SetData();
                    }
                    for (int block = 0; (block < threadCount) && (workers[block].Data != null); block++)
                    {
                        workers[block].WaitData();
                        compressed = workers[block].Compressed;
                        try
                        {
                            outStream.Write(compressed, 0, compressed.Length);
                        }
                        catch (Exception)
                        {
                            for (int i = 0; i < threadCount; i++)
                            {
                                workers[i].Data = null;
                                workers[i].SetData();
                            }
                            throw;
                        }
                        workers[block].Data = null;
                    }
                }

                for (int i = 0; i < threadCount; i++)
                    workers[i].SetData();
            }
        }

        public void Decompress()
        {
            using (FileStream inStream = new FileStream(inFile, FileMode.Open, FileAccess.Read,
                                                        FileShare.Read, 0x200000,
                                                        FileOptions.SequentialScan),
                              outStream = new FileStream(outFile, FileMode.Append))
            {
                int blockSize;
                byte[] data, compressed;
                Worker[] workers = new Worker[threadCount];
                for (int i = 0; i < threadCount; i++)
                    workers[i] = new Worker(CompressionMode.Decompress);

                Console.WriteLine("Decompressing...");

                while (inStream.Position < inStream.Length)
                {
                    for (int block = 0;
                         (block < threadCount) && (inStream.Position < inStream.Length);
                         block++)
                    {
                        byte[] info = new byte[18];
                        inStream.Read(info, 0, info.Length);
                        if (!ValidExt(info))
                            throw new Exception("File has incompatible format.");
                        blockSize = BitConverter.ToInt32(info, info.Length - 4);
                        compressed = new byte[blockSize];
                        info.CopyTo(compressed, 0);
                        inStream.Read(compressed, 18, blockSize - 18);
                        workers[block].Compressed = compressed;
                        workers[block].SetData();
                    }
                    for (int block = 0;
                         (block < threadCount) && (workers[block].Compressed != null);
                         block++)
                    {
                        workers[block].WaitData();
                        data = workers[block].Data;
                        try
                        {
                            outStream.Write(data, 0, data.Length);
                        }
                        catch (Exception)
                        {
                            for (int i = 0; i < threadCount; i++)
                            {
                                workers[i].Compressed = null;
                                workers[i].SetData();
                            }
                            throw;
                        }
                        workers[block].Compressed = null;
                    }
                }

                for (int i = 0; i < threadCount; i++)
                    workers[i].SetData();
            }
        }

        private bool ValidExt(byte[] info)
        {
            if ((info[3] & 4) == 0)
                return false;
            if ((info[10] != 'G') || (info[11] != 'T'))
                return false;
            if ((info[12] != 4) || (info[13] != 0))
                return false;
            return true;
        }
    }
}
