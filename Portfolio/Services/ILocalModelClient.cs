namespace Portfolio.Services
{
    public interface ILocalModelClient
    {
        /// <summary>
        /// Generate a response for the given prompt. Implementations may call GPT4All, Olama, or other local models.
        /// </summary>
        /// <param name="prompt">Complete prompt text (system + context + user)</param>
        /// <param name="memory">Optional chat history for the session</param>
        Task<string> GenerateAsync(string prompt, IList<Models.Chat>? memory = null);
    }
}