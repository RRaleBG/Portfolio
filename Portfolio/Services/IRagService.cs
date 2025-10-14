using System.Collections.Generic;
using System.Threading.Tasks;

namespace Portfolio.Services
{
    public interface IRagService
    {
        Task<string> BuildContextAsync(string userQuestion, int maxChars = 1500);
        // Diagnostics: get number of indexed chunks (if supported) and force rebuild
        Task<int> GetIndexCountAsync();
        Task RebuildIndexAsync();

        // Return indexed snippets (Source, Text) for UI display
        Task<List<RagSnippetDto>> GetIndexedSnippetsAsync();
    }
}
