# Quayside operational schema

`schema.json` is a declarative spec of a synthetic container-liner operations
database: 14 bounded contexts, 226 tables, 1,291 columns and 451 foreign keys
(3 of them self-references). It exists to make the NL to SQL centrepiece
honest. At this size the whole DDL cannot go into a prompt, so the pipeline has
to retrieve table description cards first, expand one hop along the foreign-key
graph, and only then build a small DDL prompt. See
`roadmap/01-architecture.md`, "The centrepiece".

It is **data, not code**. Nothing in this directory is compiled. The generator
that reads it lives in `src/Quayside.Ingest/Schema/`.

All of it is synthetic. No real MSC data, system, schema or customer is
involved; table and column names follow general container shipping vocabulary,
and every seeded row is generated from a fixed random seed.

## Counts

| thing | count |
|---|---|
| contexts | 14 |
| tables | 226 |
| columns | 1,291 |
| foreign keys | 451 (3 self-references, all nullable) |
| FK indexes emitted | 446 (unique FK columns already have a unique index) |
| generated DDL | 281,931 bytes |
| generated seed | 206,972 bytes, 2,053 rows |

Tables per context: reference 18, customer 16, booking 16, equipment 16,
vessel 15, voyage 16, portcall 15, cargo 15, reefer 15, tariff 16, invoicing 18,
customs 17, documentation 17, terminal 16.

## Shape

Every table has a surrogate `int IDENTITY` primary key, a one-sentence business
description, and a description on every column. Foreign keys always point at the
target's primary key and form a DAG apart from the three marked self-references,
so seeding order is always resolvable. No table is an orphan.

Confusable names are deliberate, because retrieval that still picks the right
one is the interesting result:

- `ContainerMovements` (physical terminal handling) vs `ContainerEvents`
  (customer-facing tracking milestones) vs `EquipmentMovements` (chassis and
  gensets) vs `YardMoves` and `GateTransactions`.
- `Tariffs` (published tariff book header) vs `TariffRates` (published rate
  lines) vs `RateSheets` (negotiated customer offers) vs `ContractRates` vs
  `TariffClassifications` (customs duty rates, not freight).
- `SailingSchedules` (published ETD and ETA) vs `PortCalls` (the vessel's
  actual visits) vs `PortCallEvents`.
- `Ports` (operated seaports) vs `UnLocodes` (the wider location registry) vs
  `Terminals`.
- `VesselInspections` vs `PortCallInspections` vs `ContainerInspections`.

The demo tables the assistant's tools read are `Containers`, `Bookings`,
`Customers`, `Vessels`, `Voyages`, `Ports`, `PortCalls`, `SailingSchedules`,
`ContainerMovements`, `ReeferReadings`, `Invoices`, `InvoiceLines`,
`CustomsDeclarations` and `BillsOfLading`.

## Regenerating SQL

The emitters are static and deterministic: the same `schema.json` always
yields byte-identical output.

```csharp
var spec = SchemaSpec.Load("data/schema/schema.json");
File.WriteAllText("ddl.sql", DdlEmitter.Emit(spec));
File.WriteAllText("seed.sql", SeedEmitter.Emit(spec));
var cards = new SchemaCardBuilder(spec);
```

- `DdlEmitter.Emit` creates schema `ops`, then every table, then every foreign
  key as a separate `ALTER TABLE`, then an index per foreign key column. Every
  statement is guarded with an existence check, so it can be re-run. It uses no
  `GO`, so it runs as a single batch through `SqlClient`.
- `SeedEmitter.Emit` writes rows in topological order of the foreign-key
  graph. The 14 demo tables get 20 to 200 rows, every other table 3 rows
  (`Countries` has 5). Each insert is guarded by `IF NOT EXISTS`.
- `SchemaCardBuilder.Cards` maps table name to the text that is embedded;
  `SchemaCardBuilder.Adjacency` maps table name to its directly related tables
  in both directions, for one-hop expansion.

Container numbers are ISO 6346 shaped (four letters, six digits, check digit).
`MSCU1234567` is seeded literally because the demo question uses it; the other
119 carry computed check digits, and that literal one does not.

## Editing the spec

Edit `schema.json` directly. After any change, regenerate and check that every
`references.table` exists, every `references.column` is that table's primary
key, names are unique, and the foreign-key graph is still acyclic (apart from
`"self": true` references, which must be nullable). `SeedEmitter` throws if it
finds a cycle.
