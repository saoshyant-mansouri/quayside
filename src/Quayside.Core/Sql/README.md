# NL to SQL over 226 tables

At 226 tables the schema cannot go into a prompt: the generated DDL is about
280 KB. The pipeline therefore retrieves the schema first, writes SQL second,
and validates it with a real parser before anything executes. Everything in
this folder is pure (no I/O); the model call, the embedding call and the
database call live behind ports in `Ports.cs` and are wired elsewhere.

## Pipeline

1. **Embed** the question (outside this folder).
2. **`SchemaRetriever.Select`** ranks table cards (name, context, description,
   column lines) two ways and fuses them with reciprocal rank fusion:
   - dense: SIMD cosine between the question embedding and each card embedding;
   - lexical: BM25 over the card text, camel-case split and lightly stemmed,
     with the table name weighted three times so `ReeferReadings` is findable
     by "reefer readings".

   The top `k` (default 8) are the direct hits. They are then expanded `hops`
   steps (default 1) along the foreign-key adjacency so join partners are
   reachable. Expanded tables always rank below direct hits; among themselves
   they are ordered by how strongly they connect to the direct hits (a table
   that bridges two strong hits beats a hub that touches one weak hit), then
   by name, so the result is deterministic and independent of input order.
3. **`SqlPrompt.Build`** writes a fixed rule block, the DDL of the selected
   tables in the order given, and the question. Same input, same bytes.
4. The model writes T-SQL (outside this folder). If it cannot answer from the
   listed tables the prompt tells it to reply `CANNOT_ANSWER`, which the guard
   rejects as unparsable, so a refusal is just another rejection.
5. **`SqlGuard.Validate`** parses with `TSql170Parser` and walks the AST. The
   set of tables retrieved in step 2 is the allow-list.
6. Execute on a `db_datareader` login with a 5 second command timeout
   (outside this folder).
7. Return rows and the SQL.

## The table cap

`SchemaRetriever.MaxTables` is **14**. With the default `k = 8` that leaves six
slots for foreign-key expansion. A request for a larger `k` or more `hops` is
silently capped, because the cap is what keeps the prompt small: 14 tables is
about 6,000 characters of DDL (roughly 1.5k tokens) against roughly 70k tokens
for the whole schema.

Edge cases: a query that matches nothing (no lexical hit, no embedding above a
cosine of 0.1) returns an empty list; `k` larger than the table count returns
every reachable table; a table with no neighbours is returned alone; neighbour
names that are not in the card set are ignored; a query embedding of the wrong
dimension is ignored and retrieval falls back to the lexical signal alone.

## Why ScriptDom and not pattern matching

A regex sees characters; SQL Server sees a grammar. `SELECT 1; DROP TABLE x`,
a `DROP` hidden after a comment and a newline, nested block comments,
bracket-quoted names (`[ops].[Bookings]`), CTE names that shadow tables,
aliases named like other tables and `OPENROWSET` spelled with odd whitespace
all defeat a blocklist that was written by thinking of the bad cases. A parser
cannot be walked around that way: the statement either parses to a single
`SelectStatement` whose every table reference resolves to an allowed table, or
it is rejected. The traversal is a `TSqlFragmentVisitor`, so a construct nobody
thought of is still visited. The one place the guard uses the lexer rather than
the grammar is to refuse comments and absurd paren nesting, below.

## Rejection rules

A rejection is a returned `SqlValidation(false, reason, [])`, never an
exception. `Reason` is written for a human and `TablesTouched` is empty.
Several reasons can be reported together. Callers passing a null allow-list or
a non-positive `maxRows` get an exception, because that is a programming error.

| rule | detail |
|---|---|
| empty input | null, empty or whitespace |
| size | longer than 8,000 characters |
| lexer errors | for example an unterminated comment or string |
| comments | any line or block comment token. Rejected outright so the guard and SQL Server can never disagree about what is code |
| nesting | more than 32 nested parentheses, checked on tokens before the parser recurses |
| parse errors | the first ScriptDom error with line and column |
| not exactly one statement | zero, or two or more, in any number of batches |
| not a `SELECT` | the single statement must be a `SelectStatement` |
| DML | INSERT, UPDATE, DELETE, MERGE, TRUNCATE |
| DDL | any CREATE, DROP, ALTER |
| permissions | GRANT, DENY, REVOKE |
| EXEC | EXEC and EXECUTE of anything: stored procedures, `sp_`, `xp_`, `sp_executesql`, `EXEC('...')` (this is the only way to run dynamic SQL) |
| WAITFOR | |
| cursors | cursor declarations and operations, and cursor definitions inside expressions |
| variables and SET | DECLARE, SET, and `SELECT @x = ...` assignment |
| `SELECT ... INTO` | |
| table sources other than a named table, derived table, VALUES list, join, PIVOT or UNPIVOT | OPENROWSET (both forms), OPENQUERY, OPENDATASOURCE, OPENJSON, OPENXML, table-valued functions, table variables, `CROSS APPLY` of a function |
| linked-server names | `server.db.schema.table` |
| cross-database names | `db.schema.table` |
| schema other than `ops` | `dbo`, `sys`, `INFORMATION_SCHEMA`, and so on |
| unqualified names | `FROM Bookings`, `#temp`, unless the name is a CTE in scope |
| tables outside the allow-list | reported as `ops.Name`, wherever they appear: joins, subqueries, derived tables, CTE bodies |
| schema-qualified or `sp_`/`xp_` function calls | user-defined functions can read tables the allow-list never saw |
| `NEXT VALUE FOR` | it mutates sequence state |
| table hints other than `NOLOCK` and `READUNCOMMITTED` | `TABLOCKX`, `UPDLOCK` and friends can block other sessions |
| missing `TOP` | the outermost query must be one `SELECT` with its own `TOP` |
| `TOP` above `maxRows` | including values that overflow a 64-bit integer |
| `TOP ... PERCENT`, `TOP ... WITH TIES` | either can return more than n rows |
| `TOP` that is not an integer literal | a variable, subquery or expression such as `5 + 5` |
| outermost `UNION` / `INTERSECT` / `EXCEPT` | wrap in a derived table so one `TOP` bounds the result |

`TOP 5` and `TOP (5)` are both accepted. Case, whitespace and `[bracket]`
quoting never change the verdict, and a statement wrapped in one pair of
parentheses is unwrapped before the `TOP` check.

### Tables and CTEs

Tables enter a query only through table references, so aliases are irrelevant:
`FROM ops.Bookings AS Invoices` touches `Bookings`. A one-part name is a CTE
only if a CTE of that name is **in scope** at that point: CTEs are registered
in order as the `WITH` list is walked, so a CTE body that mentions a CTE
defined later (which SQL Server would resolve to a real table) is rejected, and
a CTE body is always inspected, so a CTE cannot launder a disallowed table.

### Allow-list and `TablesTouched`

The allow-list holds bare table names (`Bookings`, as in `TableCard.Table`); an
`ops.` prefix on an entry is tolerated. Matching is case-insensitive, as the
database collation is. `TablesTouched` is the distinct set of tables the query
reads, in the allow-list's spelling, sorted ordinally, without the `ops.`
prefix. CTE names are not tables and are not listed.

## Tests

`tests/Quayside.UnitTests/Sql/` covers every rule above, the end to end path
over the real 226-table `data/schema/schema.json` (cards from
`SchemaCardBuilder`, deterministic hashed bag-of-words embeddings), the
confusable sets (`ContainerMovements` / `ContainerEvents` /
`EquipmentMovements`) and retrieval stability.

## Why there is a second BM25 and tokenizer here

`Quayside.Core.Retrieval` already has `Bm25Index` and `Tokenizer`. The pair in
this folder is deliberate specialisation, not a copy.

Schema retrieval matches a natural-language question against *identifiers*, so
`Lexicon` splits CamelCase (`ContainerMovements` becomes `container` and
`movements`) and stems plurals, which is what lets "how many containers" reach
the `Containers` table. Prose retrieval needs neither, and doing identifier
splitting on post text would be wrong.

The cost model differs too. Retrieval scores up to a few thousand chunks on
every request, so its implementation is span-based and allocation-light over
rented buffers. Schema retrieval scores 226 cards once per `query_database`
call, where a plain 42-line implementation is clearer and fast enough.

Merging them would mean one component carrying both sets of compromises.
