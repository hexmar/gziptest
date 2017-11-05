using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipTest
{
    class Worker
    {
        public byte[] Data { get; set; }
        public byte[] Compressed { get; set; }
        public bool Abort { get; set; }
        public bool EndOfFile { get { return endOfFile; } }
        private bool endOfFile;
        private string path;
        private int threadNumber;
        private long position;
        private Thread thread;
        private readonly Semaphore dataGetted, workDone, positionSetted;
        private readonly Controller parent;

        private static int threadCount = Environment.ProcessorCount;
        private const int dataBlockSize = 0x100000;

        public Worker(string path, int startIndex, CompressionMode mode, int threadNumber, Controller parent)
        {
            Data = null;
            Compressed = null;
            Abort = false;
            endOfFile = false;
            position = startIndex;
            this.path = path;
            this.threadNumber = threadNumber;
            this.parent = parent;
            dataGetted = new Semaphore(1, 1);
            workDone = new Semaphore(0, 1);
            positionSetted = new Semaphore(0, 1);
            if (mode == CompressionMode.Compress)
                thread = new Thread(CompressInternal);
            else
                thread = new Thread(DecompressInternal);
            thread.Start();
        }
        public void GetData()
        {
            dataGetted.Release();
        }
        public void WaitData()
        {
            workDone.WaitOne();
        }
        public void SetPosition(long position)
        {
            this.position = position;
            positionSetted.Release();
        }
        private byte[] AddExt(byte[] data)
        {
            byte[] ext = new byte[8];
            ext[0] = (byte)'G';
            ext[1] = (byte)'T';
            ext[2] = 4;
            ext[3] = 0;
            BitConverter.GetBytes(data.Length + 8).CopyTo(ext, 4);

            byte[] result = new byte[data.Length + 8];
            for (int i = 0; i < 10; i++)
                result[i] = data[i];
            result[3] = 4;
            ext.CopyTo(result, 10);
            for (int i = 10; i < data.Length; i++)
                result[i + 8] = data[i];

            return result;
        }
        private byte[] RemoveExt(byte[] data)
        {
            data[3] = 0;
            var result = new byte[data.Length - 8];
            for (int i = 0; i < 10; i++)
                result[i] = data[i];
            for (int i = 18; i < data.Length; i++)
                result[i - 8] = data[i];
            return result;
        }
        private void CompressInternal() //TODO: Check exception
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Data = new byte[dataBlockSize];
                while (position < file.Length) // Check position after moving
                {
                    dataGetted.WaitOne();
                    if (Abort)
                        return;
                    file.Position = position;
                    long dif = file.Length - file.Position;
                    if (dif < dataBlockSize)
                        Data = new byte[dif];
                    file.Read(Data, 0, Data.Length);
                    using (var stream = new MemoryStream())
                    {
                        using (var gz = new GZipStream(stream, CompressionMode.Compress))
                        {
                            gz.Write(Data, 0, Data.Length);
                        }
                        Compressed = AddExt(stream.ToArray());
                    }
                    workDone.Release();
                    position += threadCount * dataBlockSize; // Moving to next sector
                }
            }
            dataGetted.WaitOne();
            endOfFile = true;
        }
        private void DecompressInternal()
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] info = new byte[18];
                Data = new byte[dataBlockSize];
                positionSetted.WaitOne();
                while (position < file.Length) // Check position after moving
                {
                    dataGetted.WaitOne();
                    if (Abort)
                        return;
                    file.Position = position;
                    file.Read(info, 0, info.Length);
                    if (!ValidExt(info))
                    {
                        //TODO: Send exception to main thread
                        return;
                    }
                    int blockSize = BitConverter.ToInt32(info, info.Length - 4);
                    parent.SetNextPosition(threadNumber, position + blockSize);
                    Compressed = new byte[blockSize];
                    info.CopyTo(Compressed, 0);
                    file.Read(Compressed, info.Length, Compressed.Length - info.Length);
                    Compressed = RemoveExt(Compressed);
                    blockSize = BitConverter.ToInt32(Compressed, Compressed.Length - 4);
                    if (blockSize != dataBlockSize)
                        Data = new byte[blockSize];
                    using (MemoryStream stream = new MemoryStream(Compressed))
                    {
                        using (GZipStream gz = new GZipStream(stream, CompressionMode.Decompress))
                        {
                            gz.Read(Data, 0, Data.Length);
                        }
                    }
                    workDone.Release();
                    positionSetted.WaitOne(); // Moving to next sector
                }
                parent.SetNextPosition(threadNumber, file.Length);
            }
            dataGetted.WaitOne();
            endOfFile = true;
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
