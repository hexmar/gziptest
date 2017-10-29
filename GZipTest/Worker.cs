using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipTest
{
    class Worker
    {
        private byte[] data;
        private byte[] compressed;
        private Thread thread;
        private readonly Semaphore dataSetted, workDone;

        public Worker(ref byte[] data, ref byte[] compressed, CompressionMode mode)
        {
            this.data = data;
            this.compressed = compressed;
            dataSetted = new Semaphore(0, 1);
            workDone = new Semaphore(1, 1);
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
                if (data == null)
                    return;
                using (MemoryStream stream = new MemoryStream())
                {
                    using (GZipStream gz = new GZipStream(stream, CompressionMode.Compress))
                    {
                        gz.Write(data, 0, data.Length);
                    }
                    compressed = AddExt(stream.ToArray());
                }
                workDone.Release(); 
            }
        }
        private void DecompressInternal()
        {
            while (true)
            {
                dataSetted.WaitOne();
                if (compressed == null)
                    return;
                data = new byte[BitConverter.ToInt32(compressed, compressed.Length - 4)];
                using (MemoryStream stream = new MemoryStream(compressed))
                {
                    using (GZipStream gz = new GZipStream(stream, CompressionMode.Decompress))
                    {
                        gz.Read(data, 0, data.Length);
                    }
                }
                workDone.Release();
            }
        }
    }
}
