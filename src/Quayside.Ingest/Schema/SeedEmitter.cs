using System.Globalization;
using System.Text;

namespace Quayside.Ingest.Schema;

public static class SeedEmitter
{
    private const int RandomSeed = 20261001;
    private const int DefaultRows = 3;
    private static readonly DateTime Epoch = new(2026, 3, 1);

    private static readonly Dictionary<string, int> RowCounts = new()
    {
        ["Containers"] = 120, ["Bookings"] = 100, ["Customers"] = 30, ["Vessels"] = 25, ["Voyages"] = 60,
        ["Ports"] = 20, ["PortCalls"] = 150, ["SailingSchedules"] = 120, ["ContainerMovements"] = 200,
        ["ReeferReadings"] = 150, ["Invoices"] = 80, ["InvoiceLines"] = 200, ["CustomsDeclarations"] = 60,
        ["BillsOfLading"] = 100, ["Countries"] = 5,
    };

    private static readonly (string Code, string Name)[] Countries =
        [("CN", "China"), ("US", "United States"), ("DE", "Germany"), ("NL", "Netherlands"), ("IT", "Italy")];

    private static readonly (string Name, string Code, int Country, decimal Lat, decimal Lon)[] Ports =
    [
        ("Shanghai", "CNSHA", 1, 31.2304m, 121.4737m), ("Ningbo", "CNNGB", 1, 29.8683m, 121.5440m),
        ("Qingdao", "CNTAO", 1, 36.0671m, 120.3826m), ("Yantian", "CNYTN", 1, 22.5937m, 114.2786m),
        ("Xiamen", "CNXMN", 1, 24.4798m, 118.0894m), ("Tianjin", "CNTSN", 1, 39.0842m, 117.2010m),
        ("Los Angeles", "USLAX", 2, 33.7405m, -118.2711m), ("Long Beach", "USLGB", 2, 33.7701m, -118.1937m),
        ("New York", "USNYC", 2, 40.7128m, -74.0060m), ("Savannah", "USSAV", 2, 32.0809m, -81.0912m),
        ("Houston", "USHOU", 2, 29.7604m, -95.3698m), ("Hamburg", "DEHAM", 3, 53.5511m, 9.9937m),
        ("Bremerhaven", "DEBRV", 3, 53.5396m, 8.5809m), ("Rotterdam", "NLRTM", 4, 51.9244m, 4.4777m),
        ("Amsterdam", "NLAMS", 4, 52.3676m, 4.9041m), ("Genoa", "ITGOA", 5, 44.4056m, 8.9463m),
        ("La Spezia", "ITSPE", 5, 44.1025m, 9.8241m), ("Gioia Tauro", "ITGIT", 5, 38.4278m, 15.8994m),
        ("Trieste", "ITTRS", 5, 45.6495m, 13.7768m), ("Naples", "ITNAP", 5, 40.8518m, 14.2681m),
    ];

    private static readonly string[] MovementCodes = ["GATE_IN", "LOAD", "DISCHARGE", "GATE_OUT", "EMPTY_RETURN"];
    private static readonly string[] VesselAdjectives = ["Atlantic", "Pacific", "Nordic", "Meridian", "Harbour"];
    private static readonly string[] VesselNouns = ["Aurora", "Horizon", "Voyager", "Pioneer", "Sentinel"];
    private static readonly string[] CustomerPrefixes = ["Aurora", "Baltic", "Cobalt", "Delta", "Evergreen", "Fjord"];
    private static readonly string[] CustomerTrades = ["Textiles", "Foods", "Logistics", "Electronics", "Chemicals"];
    private static readonly string[] LegalForms = ["Ltd", "GmbH", "SpA"];
    private static readonly string[] Statuses = ["Open", "Confirmed", "Closed"];
    private static readonly decimal[] ReeferSetPoints = [-18m, 2m, 5m];

    private static readonly Dictionary<string, Func<int, Random, object>> Overrides = new()
    {
        ["Countries.CountryCode"] = (i, _) => Countries[i - 1].Code,
        ["Countries.CountryName"] = (i, _) => Countries[i - 1].Name,
        ["Ports.PortName"] = (i, _) => Ports[i - 1].Name,
        ["Ports.UnLocode"] = (i, _) => Ports[i - 1].Code,
        ["Ports.CountryId"] = (i, _) => Ports[i - 1].Country,
        ["Ports.Latitude"] = (i, _) => Ports[i - 1].Lat,
        ["Ports.Longitude"] = (i, _) => Ports[i - 1].Lon,
        ["Vessels.VesselName"] = (i, _) => $"{VesselAdjectives[(i - 1) % 5]} {VesselNouns[(i - 1) / 5 % 5]}",
        ["Vessels.ImoNumber"] = (i, _) => ImoNumber(i),
        ["Customers.CustomerName"] = (i, _) => $"{CustomerPrefixes[(i - 1) % 6]} {CustomerTrades[(i - 1) / 6 % 5]} {LegalForms[i % 3]}",
        ["Containers.ContainerNumber"] = (i, _) => ContainerNumber(i),
        ["Bookings.BookingNumber"] = (i, _) => $"BK{26000000 + i}",
        ["Bookings.OriginPortId"] = (i, _) => PortPair(i).Origin,
        ["Bookings.DestinationPortId"] = (i, _) => PortPair(i).Destination,
        ["Voyages.VoyageNumber"] = (i, _) => $"V{2600 + i}{(i % 2 == 0 ? "E" : "W")}",
        ["SailingSchedules.OriginPortId"] = (i, _) => PortPair(i).Origin,
        ["SailingSchedules.DestinationPortId"] = (i, _) => PortPair(i).Destination,
        ["SailingSchedules.ScheduledDeparture"] = (i, _) => Departure(i),
        ["SailingSchedules.ScheduledArrival"] = (i, _) => Departure(i).AddDays(TransitDays(i)),
        ["SailingSchedules.TransitDays"] = (i, _) => TransitDays(i),
        ["ContainerMovements.ContainerId"] = (i, r) => i <= 6 ? 1 : r.Next(1, RowCounts["Containers"] + 1),
        ["ContainerMovements.MovementCode"] = (i, _) => MovementCodes[(i - 1) % MovementCodes.Length],
        ["ReeferReadings.ReeferUnitId"] = (i, _) => ReeferUnit(i),
        ["ReeferReadings.ContainerId"] = (i, _) => ReeferUnit(i),
        ["ReeferReadings.ReadAtUtc"] = (i, _) => Epoch.AddHours(i * 4),
        ["ReeferReadings.SetPointC"] = (i, _) => ReeferSetPoints[ReeferUnit(i) - 1],
        ["ReeferReadings.SupplyAirTempC"] = (i, _) => ReeferSetPoints[ReeferUnit(i) - 1] + (i * 37 % 11 - 5) / 10m,
        ["ReeferReadings.ReturnAirTempC"] = (i, _) => ReeferSetPoints[ReeferUnit(i) - 1] + (i * 37 % 11 - 5) / 10m + 1.2m + i % 5 / 10m,
        ["Invoices.InvoiceNumber"] = (i, _) => $"INV-2026-{i:D6}",
        ["Invoices.NetAmount"] = (i, _) => NetAmount(i),
        ["Invoices.TaxAmount"] = (i, _) => decimal.Round(NetAmount(i) * 0.2m, 2),
        ["Invoices.TotalAmount"] = (i, _) => decimal.Round(NetAmount(i) * 1.2m, 2),
        ["InvoiceLines.Quantity"] = (i, _) => 1 + i % 4,
        ["InvoiceLines.UnitPrice"] = (i, _) => UnitPrice(i),
        ["InvoiceLines.LineAmount"] = (i, _) => (1 + i % 4) * UnitPrice(i),
        ["CustomsDeclarations.DeclarationNumber"] = (i, _) => $"CD-2026-{i:D6}",
        ["BillsOfLading.BlNumber"] = (i, _) => $"MEDUBL{i:D6}",
        ["BillsOfLading.LoadPortId"] = (i, _) => PortPair(i).Origin,
        ["BillsOfLading.DischargePortId"] = (i, _) => PortPair(i).Destination,
    };

    public static IReadOnlyList<TableSpec> TopologicalOrder(SchemaSpec spec)
    {
        var pending = spec.Tables.ToDictionary(
            t => t.Name,
            t => t.Columns.Where(c => c.References is { Self: false }).Select(c => c.References!.Table).ToHashSet());
        var byName = spec.Tables.ToDictionary(t => t.Name);
        var order = new List<TableSpec>();
        while (pending.Count > 0)
        {
            var ready = pending.Where(p => p.Value.Count == 0).Select(p => p.Key).Order(StringComparer.Ordinal).ToList();
            if (ready.Count == 0)
                throw new InvalidOperationException($"Foreign key cycle among: {string.Join(", ", pending.Keys.Order(StringComparer.Ordinal))}");
            foreach (var name in ready)
            {
                order.Add(byName[name]);
                pending.Remove(name);
            }
            foreach (var parents in pending.Values) parents.ExceptWith(ready);
        }
        return order;
    }

    public static string Emit(SchemaSpec spec)
    {
        var random = new Random(RandomSeed);
        var counts = new Dictionary<string, int>();
        var sql = new StringBuilder();

        foreach (var table in TopologicalOrder(spec))
        {
            var rows = RowCounts.GetValueOrDefault(table.Name, DefaultRows);
            foreach (var fk in table.Columns.Where(c => c.Unique && c.References is { Self: false }))
                rows = Math.Min(rows, counts[fk.References!.Table]);
            counts[table.Name] = rows;
            if (rows == 0) continue;

            sql.Append($"\nIF NOT EXISTS (SELECT 1 FROM ops.{table.Name})\nBEGIN\n");
            sql.Append($"SET IDENTITY_INSERT ops.{table.Name} ON;\n");
            sql.Append($"INSERT INTO ops.{table.Name} ({string.Join(", ", table.Columns.Select(c => c.Name))}) VALUES\n");
            var tuples = Enumerable.Range(1, rows).Select(row =>
                "(" + string.Join(", ", table.Columns.Select((column, ordinal) => Literal(Value(table, column, ordinal, row, random, counts), column.Type))) + ")");
            sql.Append(string.Join(",\n", tuples)).Append(";\n");
            sql.Append($"SET IDENTITY_INSERT ops.{table.Name} OFF;\nEND\n");
        }

        return sql.ToString();
    }

    private static object? Value(TableSpec table, ColumnSpec column, int ordinal, int row, Random random, Dictionary<string, int> counts)
    {
        if (column.PrimaryKey) return row;
        if (Overrides.TryGetValue($"{table.Name}.{column.Name}", out var generate)) return generate(row, random);
        if (column.References is { } target)
        {
            if (target.Self) return row > 1 && random.Next(3) > 0 ? random.Next(1, row) : null;
            if (column.Unique) return row;
            if (column.Nullable && random.Next(4) == 0) return null;
            return random.Next(1, counts[target.Table] + 1);
        }
        if (column.Nullable && !column.Unique && random.Next(4) == 0) return null;
        return Generic(table, column, ordinal, row, random);
    }

    private static object Generic(TableSpec table, ColumnSpec column, int ordinal, int row, Random random)
    {
        var type = column.Type;
        var name = column.Name;
        if (type.StartsWith("nvarchar")) return Text(table, name, row, type.EndsWith("(max)") ? int.MaxValue : int.Parse(type[9..^1]));
        if (type == "bit") return random.Next(4) != 0;
        if (type == "datetime2") return Epoch.AddDays(row % 120).AddHours(ordinal * 6);
        if (type == "date") return Epoch.AddDays(row % 120 + ordinal);
        if (type == "uniqueidentifier") return new Guid(Bytes(random, 16));
        if (type.StartsWith("varbinary")) return Bytes(random, 8);
        if (type.StartsWith("decimal")) return Decimal(name, type, random);
        if (name.EndsWith("Year")) return 2008 + row % 16;
        return name.EndsWith("Sequence") ? row : random.Next(1, 100);
    }

    private static decimal Decimal(string name, string type, Random random)
    {
        var parts = type[8..^1].Split(',');
        var scale = int.Parse(parts[1]);
        var ceiling = Math.Pow(10, int.Parse(parts[0]) - scale) - 1;
        if (name.EndsWith("TempC") || name.EndsWith("PointC") || name.EndsWith("DeviationC")) return decimal.Round((decimal)(random.NextDouble() * 30 - 18), scale);
        if (name.EndsWith("DraughtMeters")) return decimal.Round((decimal)(8 + random.NextDouble() * 8), scale);
        if (name == "Latitude") return decimal.Round((decimal)(random.NextDouble() * 140 - 70), scale);
        if (name == "Longitude") return decimal.Round((decimal)(random.NextDouble() * 340 - 170), scale);
        return decimal.Round((decimal)(random.NextDouble() * Math.Min(name.EndsWith("Pct") ? 100 : 500, ceiling)), scale);
    }

    private static string Text(TableSpec table, string name, int row, int maxLength)
    {
        var entity = Words(table.Key.Name[..^2]);
        var text = name switch
        {
            _ when name.EndsWith("Email") => $"user{row}@example.com",
            _ when name.EndsWith("Code") || name.EndsWith("Number") => $"{entity.Replace(" ", "")[..3].ToUpperInvariant()}{row:D4}",
            _ when name.EndsWith("Name") => $"{entity} {row}",
            _ when name.EndsWith("Status") => Statuses[row % Statuses.Length],
            _ => $"{Words(name)} {row}",
        };
        return text.Length > maxLength ? text[^maxLength..] : text;
    }

    private static byte[] Bytes(Random random, int length)
    {
        var bytes = new byte[length];
        random.NextBytes(bytes);
        return bytes;
    }

    private static string Words(string pascal) =>
        string.Concat(pascal.Select((ch, i) => i > 0 && char.IsUpper(ch) ? " " + ch : ch.ToString()));

    private static string Literal(object? value, string type) => value switch
    {
        null => "NULL",
        string s => $"N'{s.Replace("'", "''")}'",
        bool b => b ? "1" : "0",
        DateTime d => $"'{d.ToString(type == "date" ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}'",
        Guid g => $"'{g}'",
        byte[] bytes => "0x" + Convert.ToHexString(bytes),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException($"Unsupported seed value {value.GetType()}"),
    };

    private static (int Origin, int Destination) PortPair(int row)
    {
        var origin = (row - 1) % Ports.Length;
        var destination = (origin + 1 + (row * 7 + 3) % (Ports.Length - 1)) % Ports.Length;
        return (origin + 1, destination + 1);
    }

    private static int ReeferUnit(int row) => 1 + (row - 1) % ReeferSetPoints.Length;
    private static DateTime Departure(int row) => Epoch.AddDays(row * 2).AddHours(18);
    private static int TransitDays(int row) => 8 + row % 20;
    private static decimal NetAmount(int row) => 1000 + row * 137 % 9000;
    private static decimal UnitPrice(int row) => 250 + row * 53 % 2750;

    private static string ContainerNumber(int row)
    {
        if (row == 1) return "MSCU1234567";
        var prefix = $"MSCU{100000 + row * 7919 % 899999}";
        return prefix + CheckDigit(prefix);
    }

    private static string ImoNumber(int row)
    {
        var body = (900000 + row * 13).ToString(CultureInfo.InvariantCulture);
        return body + body.Select((ch, i) => (ch - '0') * (7 - i)).Sum() % 10;
    }

    private static int CheckDigit(string prefix) =>
        prefix.Select((ch, i) => (char.IsLetter(ch) ? 10 + ch - 'A' + (9 + ch - 'A') / 10 : ch - '0') * (1 << i)).Sum() % 11 % 10;
}
