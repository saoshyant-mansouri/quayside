# Retrieval and grounding

This folder, together with `Documents` and `Grounding`, is the pure, in-memory
retrieval engine. Nothing here performs I/O. The index is built once from
documents and embedded chunks, then only read, so any number of requests may
call `HybridIndex.Search` concurrently without locks.

## Pipeline

1. `Chunker.Split` turns a `Document` into `Chunk` records.
2. `HybridIndex.Build` copies every embedding into one contiguous `float[]`
   (row i starts at `i * dimensions`), normalises each row to unit length once,
   and builds a BM25 inverted index over `title + chunk text`.
3. `HybridIndex.Search`:
   1. normalise the query vector;
   2. score every chunk with `TensorPrimitives.Dot` over spans of the buffer
      (cosine similarity, because both sides are unit length);
   3. score the query text with BM25;
   4. keep the top `max(50, 5k)` of each ranking;
   5. fuse the two rankings with reciprocal rank fusion;
   6. take the top `max(20, 3k)` fused candidates as the MMR pool;
   7. select `k` hits with MMR.
4. `CitationBuilder.From` numbers the hits `[1]`, `[2]`, ... deduplicated by
   document, and `CitationEnforcer.Check` audits an answer against them.

Scratch arrays for a search come from `ArrayPool`, so the only per-search
allocations are the returned hit array, the `ScoredChunk` records and the
`RetrievalResult`.

## Chunking

Sentences are found by `SentenceSplitter`. A boundary is a run of `. ! ?` (or
the CJK full stops), plus any closing quotes, brackets and `[n]` markers,
followed by whitespace. A full stop is not a boundary after a known
abbreviation or a single capital initial, after a bare list number such as
`1.`, or when the next word starts in lower case. URLs and decimals contain no
whitespace after the dot, so they never split. Every newline is also a
boundary, which keeps the paragraph and line structure of LinkedIn posts and
scraped web pages; a chunk keeps the original line breaks of the text it covers.

Packing is greedy: sentences are added until the next one would push the chunk
past the target. The next chunk then starts a few whole sentences back so that
the shared tail is at most the overlap budget (and never so long that it blocks
progress). A document that fits in one chunk yields exactly one chunk. Empty or
whitespace-only text yields no chunks.

A single sentence longer than the target cannot respect both limits, so it is
split between words, and if it has no whitespace at all, between grapheme
clusters (`StringInfo`), so surrogate pairs and joined emoji are never cut.
This is the only case in which a chunk boundary falls inside a sentence.

Chunk ids are `{documentId}#{ordinal:D4}`, so the same document always yields
the same ids.

### Token estimate

`TokenEstimator` uses `ceil(characters / 4)`, where characters are UTF-16 code
units. It is deterministic, needs no tokenizer package and is close enough to
English BPE (about four characters per token) for sizing chunks. It
underestimates tokens for text dense in emoji or CJK, where one or two
characters can cost a token each, so chunks of such text are somewhat larger in
real tokens than the estimate says. The embedding model accepts 8191 tokens, so
the 700 token target leaves a wide margin even at 3x error.

## Constants

| constant | value | why |
|---|---|---|
| BM25 k1 | 1.2 | standard term-frequency saturation |
| BM25 b | 0.75 | standard length normalisation |
| BM25 idf | `ln(1 + (N - df + 0.5) / (df + 0.5))` | never negative, so a very common term cannot subtract score |
| RRF k | 60 | the constant from the original RRF paper; flattens the head so that agreement between rankings matters more than a single first place |
| candidate depth | `max(50, 5k)` per ranking | RRF only needs the head of each list; contributions past rank 50 are below 1/110 |
| MMR lambda | 0.7 | leans to relevance but a near duplicate (similarity close to 1) loses 0.3 and so is displaced by any distinct candidate within about 40 percent of its relevance |
| MMR pool | `max(20, 3k)` | enough alternatives to diversify from, small enough that selection costs microseconds |

MMR relevance is the fused RRF score min-max scaled over the pool (best = 1,
worst = 0) so it is on the same 0..1 scale as cosine similarity. The
similarity term is the cosine between candidates, a plain dot product of the
stored unit vectors. The `Score` on each returned `ScoredChunk` is the raw RRF
score; hits come back in MMR selection order, which can differ slightly from
descending RRF order. RRF scores are relative: the maximum possible is
2 / 61, reached by a chunk ranked first in both lists, and they are not a
calibrated confidence for refusing to answer.

BM25 terms: lowercased, split on anything that is not a letter or digit,
tokens over 64 characters dropped, and a small English stopword list removed.
Query terms are de-duplicated and at most 64 distinct terms are used.

## Grounding

`CitationEnforcer.Check` splits the answer with the same sentence splitter. A
sentence is supported when it contains at least one `[n]` that resolves to a
citation in the list. Rules worth knowing:

- Only `[n]` with a single positive integer counts. `[1, 2]`, `[1-3]` and a
  markdown link label such as `[1](...)` are not markers. Adjacent `[1][2]` is.
- Fenced code blocks are ignored entirely and inline code spans are masked, so
  neither contributes markers nor sentences.
- Sentences that assert nothing are neither cited nor uncited: questions,
  headings, lines ending in a colon, greetings and thanks, offers of further
  help, and refusals such as "I could not find ...". A refusal that continues
  with "but ..." or similar is judged as a claim, and a greeting followed by a
  statement ("Sure, MSC operates ...") is judged on the statement.
- Unsupported sentences are returned verbatim, trimmed, including any invalid
  marker they carry.

## Measured timings

Apple silicon laptop, .NET 10, Release build, 1536-dimension random vectors,
k = 8, a text query on every search, 5000 timed searches after warm-up,
`ElapsedMs` as reported by `HybridIndex.Search`:

| index | chunks | p50 | p99 |
|---|---|---|---|
| real LinkedIn posts only | 115 | 0.09 ms | 0.13 ms |
| real LinkedIn posts plus website pages | 440 | 0.16 ms | 0.21 ms |
| same corpus tiled to the 4000 chunk upper bound | 4000 | 1.05 ms | 1.29 ms |

The 4000 chunk index is also exercised by
`HybridIndexTests.Corpus_scale_search_latency_is_reported`, which printed p50
between 1.07 and 1.14 ms and p99 between 1.3 and 3.8 ms over several runs while
other tests were running. Building the 4000 chunk index takes about 110 ms.
Allocation per search is about 540 bytes. The architecture budget of under
5 ms p50 for hybrid retrieval holds with a wide margin; the cost is dominated
by streaming the 24 MB embedding buffer once.

## The refusal signal

`ScoredChunk.Score` is the fused reciprocal-rank-fusion score and hits come
back in MMR order, so neither is comparable across queries: RRF scores are
relative to a ranking, not to the question. They cannot answer "is there any
evidence for this at all?", which is exactly what the grounding contract needs
before it lets the model speak.

So `ScoredChunk.Cosine` carries the raw cosine similarity of that chunk to the
query, and `RetrievalResult.TopCosine` carries the best cosine among the
returned hits. Because embeddings are normalised once at build time, this is a
plain dot product already computed during the dense pass — it costs nothing
extra.

`TopCosine` is an absolute, query-independent number in [-1, 1] and is the
value the generation layer thresholds on to refuse. A zero query vector yields
a `TopCosine` of 0, as does an empty result.
