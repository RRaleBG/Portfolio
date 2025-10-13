namespace Portfolio.Services
{
    public interface IEmbeddingClient
    {
        Task<float[]> CreateEmbeddingAsync(string input);
        Task<List<float[]>> CreateEmbeddingsAsync(List<string> inputs);
    }
}