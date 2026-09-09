using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Presentation.Endpoints;

public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/incidents");

        group.MapGet("/", GetListAsync)
            .Produces<IReadOnlyList<IncidentListItemResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetDetailsAsync)
            .Produces<IncidentDetailsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/severity-summary", GetSeveritySummaryAsync)
            .Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<IResult> GetListAsync(
        IncidentStatus? status,
        IncidentQueries incidentQueries,
        CancellationToken cancellationToken)
    {
        var incidents = await incidentQueries.GetListAsync(status, cancellationToken);
        return Results.Ok(incidents);
    }

    private static async Task<IResult> GetDetailsAsync(
        Guid id,
        IncidentQueries incidentQueries,
        CancellationToken cancellationToken)
    {
        var incident = await incidentQueries.GetDetailsAsync(id, cancellationToken);
        return incident is not null ? Results.Ok(incident) : Results.NotFound();
    }

    private static async Task<IResult> GetSeveritySummaryAsync(
        IncidentQueries incidentQueries,
        CancellationToken cancellationToken)
    {
        var summary = await incidentQueries.GetSeveritySummaryAsync(cancellationToken);
        return Results.Ok(summary);
    }
}