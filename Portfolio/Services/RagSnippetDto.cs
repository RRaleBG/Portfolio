namespace Portfolio.Services
{
    public class RagSnippetDto
    {
        public string Id { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;

        public RagSnippetDto() { }
        public RagSnippetDto(string id, string source, string text)
        {
            Id = id; Source = source; Text = text;
        }
    }
}
