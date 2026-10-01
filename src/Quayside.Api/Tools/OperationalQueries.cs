namespace Quayside.Api.Tools;

public static class OperationalQueries
{
    public static OperationalQuery ContainerSummary(string containerNumber) => new(
        "SELECT TOP (1) c.ContainerNumber, t.TypeCode, t.TypeName, s.StatusName, c.ManufactureYear "
        + "FROM ops.Containers c "
        + "JOIN ops.ContainerTypes t ON t.ContainerTypeId = c.ContainerTypeId "
        + "JOIN ops.ContainerStatuses s ON s.ContainerStatusId = c.ContainerStatusId "
        + $"WHERE c.ContainerNumber = {SqlLiteral.ContainerNumber(containerNumber)}",
        Set("Containers", "ContainerTypes", "ContainerStatuses"),
        1);

    public static OperationalQuery ContainerMovements(string containerNumber) => new(
        "SELECT TOP (10) m.MovementCode, p.PortName, p.UnLocode, m.OccurredAtUtc, m.IsLaden "
        + "FROM ops.ContainerMovements m "
        + "JOIN ops.Containers c ON c.ContainerId = m.ContainerId "
        + "JOIN ops.Ports p ON p.PortId = m.PortId "
        + $"WHERE c.ContainerNumber = {SqlLiteral.ContainerNumber(containerNumber)} "
        + "ORDER BY m.OccurredAtUtc DESC",
        Set("ContainerMovements", "Containers", "Ports"),
        10);

    public static OperationalQuery Schedules(string origin, string destination, DateOnly? earliestDeparture)
    {
        var departure = earliestDeparture is { } date ? $" AND s.ScheduledDeparture >= {SqlLiteral.Date(date)}" : string.Empty;
        return new OperationalQuery(
            "SELECT TOP (10) v.VoyageNumber, ves.VesselName, op.PortName AS OriginPort, op.UnLocode AS OriginLocode, "
            + "dp.PortName AS DestinationPort, dp.UnLocode AS DestinationLocode, "
            + "s.ScheduledDeparture, s.ScheduledArrival, s.TransitDays, s.IsDirect "
            + "FROM ops.SailingSchedules s "
            + "JOIN ops.Voyages v ON v.VoyageId = s.VoyageId "
            + "JOIN ops.Vessels ves ON ves.VesselId = v.VesselId "
            + "JOIN ops.Ports op ON op.PortId = s.OriginPortId "
            + "JOIN ops.Ports dp ON dp.PortId = s.DestinationPortId "
            + $"WHERE {PortFilter("op", origin)} AND {PortFilter("dp", destination)}{departure} "
            + "ORDER BY s.ScheduledDeparture",
            Set("SailingSchedules", "Voyages", "Vessels", "Ports"),
            10);
    }

    public static OperationalQuery Vessel(string nameOrImo)
    {
        var filter = SqlLiteral.LooksLikeImo(nameOrImo)
            ? $"v.ImoNumber = {SqlLiteral.Imo(nameOrImo)}"
            : $"v.VesselName LIKE {SqlLiteral.NameContains(nameOrImo)}";
        return new OperationalQuery(
            "SELECT TOP (5) v.VesselName, v.ImoNumber, vc.ClassName, c.CountryName AS Flag, v.BuiltYear, v.TeuCapacity, "
            + "v.GrossTonnage, v.DeadweightTonnes, v.LengthOverallMeters, v.BeamMeters, v.ServiceSpeedKnots, v.IsActive "
            + "FROM ops.Vessels v "
            + "JOIN ops.VesselClasses vc ON vc.VesselClassId = v.VesselClassId "
            + "JOIN ops.Countries c ON c.CountryId = v.FlagCountryId "
            + $"WHERE {filter} "
            + "ORDER BY v.VesselName",
            Set("Vessels", "VesselClasses", "Countries"),
            5);
    }

    public static OperationalQuery Port(string nameOrLocode) => new(
        "SELECT TOP (5) p.PortName, p.UnLocode, c.CountryName, p.Latitude, p.Longitude, p.MaxDraughtMeters, p.IsHubPort "
        + "FROM ops.Ports p "
        + "JOIN ops.Countries c ON c.CountryId = p.CountryId "
        + $"WHERE {PortFilter("p", nameOrLocode)} "
        + "ORDER BY p.PortName",
        Set("Ports", "Countries"),
        5);

    private static string PortFilter(string alias, string nameOrLocode) =>
        SqlLiteral.LooksLikeLocode(nameOrLocode)
            ? $"{alias}.UnLocode = {SqlLiteral.Locode(nameOrLocode)}"
            : $"{alias}.PortName LIKE {SqlLiteral.NameContains(nameOrLocode)}";

    private static HashSet<string> Set(params string[] tables) => new(tables, StringComparer.OrdinalIgnoreCase);
}
