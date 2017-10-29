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
        private Thread thread;
        private readonly Semaphore dataSetted, workDone;

        public Worker(CompressionMode mode)
        {
            dataSetted = new Semaphore(0, 1);
            workDone = new Semaphore(0, 1);
            if (mode == CompressionMode.Compress)
                thread = new Thread(CompressInternal);
            else
                thread = new Thread(DecompressInternal);
            thread.Start();
        }
        public void SetData()
        {
            dataSetted.Release();
        }
        public void WaitData()
        {
            workDone.WaitOne();
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
        private void CompressInternal()
        {
            while (true)
            {
                dataSetted.WaitOne();
                if (Data == null)
                    return;
                using (MemoryStream stream = new MemoryStream())
                {
                    using (GZipStream gz = new GZipStream(stream, CompressionMode.Compress))
                    {
                        gz.Write(Data, 0, Data.Length);
                    }
                    Compressed = AddExt(stream.ToArray());
                }
                workDone.Release(); 
            }
        }
        private void DecompressInternal()
        {
            while (true)
            {
                dataSetted.WaitOne();
                if (Compressed == null)
                    return;
                Data = new byte[BitConverter.ToInt32(Compressed, Compressed.Length - 4)];
                using (MemoryStream stream = new MemoryStream(Compressed))
                {
                    using (GZipStream gz = new GZipStream(stream, CompressionMode.Decompress))
                    {
                        gz.Read(Data, 0, Data.Length);
                    }
                }
                workDone.Release();
            }
        }
    }
}
