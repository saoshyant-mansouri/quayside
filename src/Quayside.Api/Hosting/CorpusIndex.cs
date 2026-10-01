using Quayside.Core.Retrieval;

namespace Quayside.Api.Hosting;

public sealed record CorpusIndex(HybridIndex Index, DateTimeOffset? CapturedAt);
