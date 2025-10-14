using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace Portfolio.Services
{
    public sealed class LocalEmbeddingClient : IEmbeddingClient
    {
        private readonly ILogger<LocalEmbeddingClient> _logger;
        private readonly int _dimension;
        private readonly Encoding _utf8 = Encoding.UTF8;

        public LocalEmbeddingClient(IConfiguration config, ILogger<LocalEmbeddingClient> logger)
        {
            _logger = logger;
            if (!int.TryParse(config["LocalEmbedding:Dimension"], out _dimension))
                _dimension = 128; // reasonable default
        }

        public Task<float[]> CreateEmbeddingAsync(string input)
        {
            var v = GenerateDeterministicVector(input ?? string.Empty, _dimension);
            return Task.FromResult(v);
        }

        public Task<List<float[]>> CreateEmbeddingsAsync(List<string> inputs)
        {
            var list = new List<float[]>(inputs.Count);
            for (int i = 0; i < inputs.Count; i++)
            {
                list.Add(GenerateDeterministicVector(inputs[i] ?? string.Empty, _dimension));
            }
            return Task.FromResult(list);
        }

        private float[] GenerateDeterministicVector(string input, int dim)
        {
            var vec = new float[dim];

            // Pre-encode input once
            var baseBytes = _utf8.GetBytes(input ?? string.Empty);

            // Rent buffer for baseBytes + 4 bytes counter
            int bufSize = baseBytes.Length + 4;
            var pool = ArrayPool<byte>.Shared;
            var buffer = pool.Rent(bufSize);
            try
            {
                for (int i = 0; i < dim; i++)
                {                   
                    Array.Copy(baseBytes, 0, buffer, 0, baseBytes.Length);
                    
                    int offset = baseBytes.Length;
                    buffer[offset] = (byte)(i & 0xFF);
                    buffer[offset + 1] = (byte)((i >> 8) & 0xFF);
                    buffer[offset + 2] = (byte)((i >> 16) & 0xFF);
                    buffer[offset + 3] = (byte)((i >> 24) & 0xFF);

                    // compute SHA256
                    Span<byte> hash = stackalloc byte[32];
                    using (var sha = SHA256.Create())
                    {
                        sha.TryComputeHash(new ReadOnlySpan<byte>(buffer, 0, baseBytes.Length + 4), hash, out _);
                    }

                    // take first 4 bytes as uint
                    uint val = (uint)(hash[0] | (hash[1] << 8) | (hash[2] << 16) | (hash[3] << 24));
                    vec[i] = (float)((val / (double)uint.MaxValue) * 2.0 - 1.0);
                }
            }
            finally
            {
                pool.Return(buffer);
            }

            // normalize vector
            double norm = 0;
            for (int i = 0; i < dim; i++) norm += vec[i] * vec[i];
            norm = Math.Sqrt(norm) + 1e-8;
            for (int i = 0; i < dim; i++) vec[i] = (float)(vec[i] / norm);
            return vec;
        }
    }
}
