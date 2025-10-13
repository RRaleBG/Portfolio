using Portfolio.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace Portfolio.Services
{
    public class AIService
    {
        private readonly ILocalModelClient? _localClient;
        private readonly IRagService? _rag;
        private readonly ILogger<AIService> _logger;

        // Stronger system prompt to guide accuracy and source-citation
        private const string SystemPrompt =
            "You are Radoš's friendly portfolio assistant. Speak directly to the visitor using 'you' and 'I'. " +
            "Answer concisely and politely in a professional, warm tone. If the question is ambiguous, ask one short clarifying question. " +
            "Do not hallucinate: if you cannot determine the answer from the provided context or facts you know, say \"I don't know\" and offer next steps. " +
            "When the request includes 'Source:' lines in the context, ground your answer in those passages and, if you used them, append a short 'Sources:' line listing the source labels (comma-separated). " +
            "Prefer short answers (1-4 sentences); offer to expand on request.";

        public AIService(IConfiguration config, ILogger<AIService> logger, IRagService? rag = null, ILocalModelClient? localClient = null)
        {
            _logger = logger;
            _localClient = localClient;
            _rag = rag;

            if (_localClient == null)
            {
                _logger.LogWarning("No local model client configured. AI features will be disabled until configured.");
            }
        }

        public async Task<string> AskAsync(string userMessage)
        {
            return await AskAsync(userMessage, memory: null);
        }

        public async Task<string> AskAsync(string userMessage, IList<Chat>? memory)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                return "Please provide a question.";
            }

            // If a local model client is available, prefer it
            if (_localClient != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine(SystemPrompt);

                if (_rag != null)
                {
                    try
                    {
                        var ctx = await _rag.BuildContextAsync(userMessage);
                        if (!string.IsNullOrWhiteSpace(ctx))
                        {
                            sb.AppendLine("When relevant, ground your answer in this context about Radoš. If unknown, say you don't know.");
                            sb.AppendLine("---");
                            sb.AppendLine(ctx);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "RAG context failed; continuing without context");
                    }
                }

                if (memory != null)
                {
                    foreach (var m in memory)
                    {
                        sb.AppendLine($"[{m.Role}] {m.Content}");
                    }
                }

                sb.AppendLine(userMessage);

                try
                {
                    var prompt = sb.ToString();
                    var reply = await _localClient.GenerateAsync(prompt, memory);
                    return string.IsNullOrWhiteSpace(reply) ? "I'm not sure how to answer that." : reply.Trim();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Local model generation failed");
                    return "Sorry, I couldn't reach the AI service right now. Please try again later.";
                }
            }

            // Fallback: no local model client available. Use RAG (embeddings + index) to build context and synthesize a higher-quality extractive answer.
            try
            {
                if (_rag == null)
                {
                    _logger.LogWarning("AI requested but no local model client or RAG service available.");
                    return "AI is not configured on this server. Please contact the site owner to enable AI features.";
                }

                var ctx = await _rag.BuildContextAsync(userMessage, maxChars: 4000);
                if (string.IsNullOrWhiteSpace(ctx))
                {
                    return "I don't have enough information in the CV or site content to answer that. Please ask a different question or contact the site owner.";
                }

                // Parse RAG context blocks. Each block starts with 'Source: {label}\n{text}' and blocks are separated by '\n---\n'
                var blocks = ctx.Split(new[] { "\n---\n" }, StringSplitOptions.RemoveEmptyEntries);

                // Tokenize query into keywords
                var keywords = Tokenize(userMessage);

                var scoredSentences = new List<(string Sentence, string Source, double Score)>();

                foreach (var block in blocks)
                {
                    var lines = block.Split(new[] { '\n' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    string source = "unknown";
                    string text = block;
                    if (lines.Length == 2 && lines[0].StartsWith("Source:", StringComparison.OrdinalIgnoreCase))
                    {
                        source = lines[0].Substring("Source:".Length).Trim();
                        text = lines[1];
                    }

                    // Split text into sentences
                    var sentences = SplitIntoSentences(text);
                    foreach (var s in sentences)
                    {
                        var score = ScoreSentence(s, keywords);
                        if (score > 0)
                        {
                            scoredSentences.Add((s.Trim(), source, score));
                        }
                    }
                }

                // If no sentence has keyword matches, fall back to returning first chunk(s)
                string answerBody;
                var citations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (scoredSentences.Count == 0)
                {
                    // return up to two blocks verbatim (shortened)
                    var take = blocks.Take(2).Select(b => b.Trim()).ToList();
                    answerBody = string.Join("\n\n", take).Trim();
                    foreach (var b in take)
                    {
                        var m = Regex.Match(b, @"^Source:\s*(.+)$", RegexOptions.Multiline);
                        if (m.Success) citations.Add(m.Groups[1].Value.Trim());
                    }
                }
                else
                {
                    // take top N sentences by score, prefer diversity of sources
                    var ordered = scoredSentences.OrderByDescending(x => x.Score).ToList();
                    var selected = new List<(string Sentence, string Source)>();
                    foreach (var s in ordered)
                    {
                        if (selected.Count >= 3) break;
                        // prefer sentences from different sources when possible
                        if (selected.Any(x => x.Source.Equals(s.Source, StringComparison.OrdinalIgnoreCase)))
                        {
                            // allow at most 2 sentences from same source
                            var countFromSource = selected.Count(x => x.Source.Equals(s.Source, StringComparison.OrdinalIgnoreCase));
                            if (countFromSource >= 2) continue;
                        }
                        selected.Add((s.Sentence, s.Source));
                        citations.Add(s.Source);
                    }

                    answerBody = string.Join("\n\n", selected.Select(s => s.Sentence.Trim()));
                }

                // Build concise answer
                var sbAnswer = new StringBuilder();
                sbAnswer.AppendLine(GenerateLeadIn(userMessage));
                sbAnswer.AppendLine(answerBody.Length > 1200 ? answerBody.Substring(0, 1200) + "..." : answerBody);
                if (citations.Count > 0)
                {
                    sbAnswer.AppendLine();
                    sbAnswer.AppendLine("Sources: " + string.Join(", ", citations));
                }
                else
                {
                    sbAnswer.AppendLine();
                    sbAnswer.AppendLine("(No explicit sources available)");
                }

                sbAnswer.AppendLine();
                sbAnswer.AppendLine("If you'd like more details, ask a follow-up question or contact the site owner.");

                return sbAnswer.ToString().Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallback RAG answer generation failed");
                return "Sorry, I couldn't generate an answer right now. Please try again later.";
            }
        }

        private static string GenerateLeadIn(string question)
        {
            // Short lead-in encouraging concise answer
            return "Based on content from my CV and site:";
        }

        private static List<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();
            var words = Regex.Matches(text.ToLowerInvariant(), "[a-z0-9']+")
                             .Select(m => m.Value)
                             .Where(w => w.Length > 1)
                             .Where(w => !StopWords.Contains(w))
                             .Distinct()
                             .ToList();
            return words;
        }

        private static double ScoreSentence(string sentence, List<string> keywords)
        {
            if (string.IsNullOrWhiteSpace(sentence) || keywords == null || keywords.Count == 0) return 0;
            var s = sentence.ToLowerInvariant();
            double score = 0;
            foreach (var k in keywords)
            {
                if (s.Contains(k)) score += 1.0;
            }
            // boost shorter, denser sentences
            var lenFactor = Math.Max(1.0, 200.0 / Math.Max(20.0, sentence.Length));
            return score * lenFactor;
        }

        private static IEnumerable<string> SplitIntoSentences(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;
            // crude sentence splitter
            var pieces = Regex.Split(text, @"(?<=[\.\!\?])\s+");
            foreach (var p in pieces)
            {
                var t = p.Trim();
                if (t.Length > 0) yield return t;
            }
        }

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "the","and","for","with","that","this","from","your","you","are","was","were","have","has","had","not","but","about","their","they","them","his","her","she","he","its","what","which","when","where","how","why","will","can","could","should","would","may","might","also","into","over","under","more","other","some","such","these","those","each","any","all","it's","i","me","my","we","our","ours"
        };
    }
}