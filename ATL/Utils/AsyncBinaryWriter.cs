using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ATL
{
    // Inspired by https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync

    public sealed class AsyncBinaryWriter : IDisposable, IAsyncDisposable
    {

        private readonly Stream stream;
        private readonly Encoding stringEncoding;

        public AsyncBinaryWriter(Stream stream)
        {
            this.stream = stream;
        }

        public AsyncBinaryWriter(Stream stream, Encoding stringEncoding)
        {
            this.stream = stream;
            this.stringEncoding = stringEncoding;
        }

        public Stream BaseStream
        {
            get => stream;
        }


        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            Dispose(disposing: false);
#pragma warning disable S3971 // "GC.SuppressFinalize" should not be called
            GC.SuppressFinalize(this);
#pragma warning restore S3971 // "GC.SuppressFinalize" should not be called
        }

        protected void Dispose(bool disposing)
        {
            if (disposing)
            {
                stream.Dispose();
            }
        }
    }
}
