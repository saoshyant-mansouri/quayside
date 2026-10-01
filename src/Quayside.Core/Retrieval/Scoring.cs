using Quayside.Core.Documents;

namespace Quayside.Core.Retrieval;

public sealed record ScoredChunk(Chunk Chunk, Document Document, double Score);

public sealed record RetrievalResult(IReadOnlyList<ScoredChunk> Hits, int Considered, double ElapsedMs);
