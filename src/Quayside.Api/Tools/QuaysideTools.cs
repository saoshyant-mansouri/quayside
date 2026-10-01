using System.ComponentModel;
using Microsoft.SemanticKernel;
using Quayside.Api.Orchestration;

namespace Quayside.Api.Tools;

public sealed class QuaysideTools(
    TurnContext turn,
    ToolRunner runner,
    KnowledgeSearch knowledge,
    DatabaseQuery database,
    OperationalLookups operations)
{
    public const string PluginName = "quayside";

    [KernelFunction("search_knowledge")]
    [Description("Search MSC's public LinkedIn posts and web pages. Returns numbered sources to cite as [n]. Use it for any question about MSC the company: strategy, services, sustainability, fleet, news, positions.")]
    public Task<string> SearchKnowledge(
        [Description("A self-contained search query that names the topic, for example: MSC alternative marine fuels methanol LNG")] string query,
        CancellationToken ct) =>
        runner.RunAsync(turn, "search_knowledge", () => knowledge.SearchAsync(turn, query, ct), ct);

    [KernelFunction("query_database")]
    [Description("Answer a counting, ranking or aggregate question about the synthetic demo operations database by writing and running a read-only SQL query. Returns the SQL and the rows.")]
    public Task<string> QueryDatabase(
        [Description("The analytical question in plain English, for example: Which five ports had the most import containers last quarter?")] string question,
        CancellationToken ct) =>
        runner.RunAsync(turn, "query_database", () => database.RunAsync(turn, question, ct), ct);

    [KernelFunction("track_container")]
    [Description("Look up one container in the synthetic demo database by its ISO 6346 number and return its type, status and latest movements.")]
    public Task<string> TrackContainer(
        [Description("Container number: four letters then seven digits, for example MSCU1234567")] string containerNumber,
        CancellationToken ct) =>
        runner.RunAsync(turn, "track_container", () => operations.TrackContainerAsync(turn, containerNumber, ct), ct);

    [KernelFunction("find_schedules")]
    [Description("Search published sailings in the synthetic demo database between an origin and a destination port.")]
    public Task<string> FindSchedules(
        [Description("Origin port name or five letter UN/LOCODE, for example Rotterdam or NLRTM")] string origin,
        [Description("Destination port name or five letter UN/LOCODE")] string destination,
        [Description("Optional earliest departure date as yyyy-MM-dd")] string? earliestDeparture,
        CancellationToken ct) =>
        runner.RunAsync(turn, "find_schedules", () => operations.FindSchedulesAsync(turn, origin, destination, earliestDeparture, ct), ct);

    [KernelFunction("get_vessel")]
    [Description("Look up a vessel in the synthetic demo fleet by name or seven digit IMO number and return its particulars.")]
    public Task<string> GetVessel(
        [Description("Vessel name or IMO number")] string nameOrImo,
        CancellationToken ct) =>
        runner.RunAsync(turn, "get_vessel", () => operations.GetVesselAsync(turn, nameOrImo, ct), ct);

    [KernelFunction("get_port")]
    [Description("Look up a port in the synthetic demo network by name or five letter UN/LOCODE and return its particulars.")]
    public Task<string> GetPort(
        [Description("Port name or UN/LOCODE")] string nameOrLocode,
        CancellationToken ct) =>
        runner.RunAsync(turn, "get_port", () => operations.GetPortAsync(turn, nameOrLocode, ct), ct);
}
