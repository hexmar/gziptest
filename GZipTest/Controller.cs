using System;
using System.IO;
using System.IO.Compression;

namespace GZipTest
{
    public class Controller
    {
        static int threadCount = Environment.ProcessorCount;

        const int dataBlockSize = 0x100000;

        readonly string inFile, outFile;
        private Worker[] workers;
        volatile private bool abort;
        volatile private Exception exception;

        public Controller(string inFilePath, string outFilePath)
        {
            inFile = inFilePath;
            outFile = outFilePath;
            abort = false;
            try
            {
                using (var stream = new FileStream(inFile, FileMode.Open));
                using (var stream = new FileStream(outFile, FileMode.CreateNew));
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
                    if (abort) throw exception;
                    workers[thread].WaitData();
                    compressed = new byte[workers[thread].Compressed.Length];
                    workers[thread].Compressed.CopyTo(compressed, 0);
                    workers[thread].GetData();
                    try
                    {
                        outStream.Write(compressed, 0, compressed.Length);
                    }
                    catch (Exception e)
                    {
                        StopWork(e);
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
                    if (abort) throw exception;
                    workers[thread].WaitData();
                    decompressed = new byte[workers[thread].Data.Length];
                    workers[thread].Data.CopyTo(decompressed, 0);
                    workers[thread].GetData();
                    try
                    {
                        outStream.Write(decompressed, 0, decompressed.Length);
                    }
                    catch (Exception e)
                    {
                        StopWork(e);
                        throw;
                    }
                }
            }
        }

        public void SetNextPosition(int thread, long position)
        {
            workers[(thread + 1) == threadCount ? 0 : (thread + 1)].SetPosition(position);
        }

        public void StopWork(Exception e)
        {
            exception = e;
            abort = true;
            for (int i = 0; i < threadCount; i++)
            {
                workers[i].Abort = true;
                try
                {
                    workers[i].SetPosition(Int64.MaxValue);
                }
                catch (Exception) { }
                try
                {
                    workers[i].GetData();
                }
                catch (Exception) { }
            }
        }
    }
}
