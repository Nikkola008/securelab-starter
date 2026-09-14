using Microsoft.AspNetCore.Mvc;
using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Presentation.Endpoints;

public static class IncidentEndpoints
{
    public static RouteGroupBuilder MapIncidentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/incidents");

        // 1. Отримання списку інцидентів з можливістю фільтрації за status
        group.MapGet("/", GetListAsync)
            .Produces<IReadOnlyList<IncidentListItemResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        // 2. Отримання деталей інциденту за GUID
        group.MapGet("/{id:guid}", GetDetailsAsync)
            .Produces<IncidentDetailsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // 3. Отримання підсумку за severity з підтримкою фільтру status (Добрий рівень)
        group.MapGet("/severity-summary", GetSeveritySummaryAsync)
            .Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    private static async Task<IResult> GetListAsync(
        [FromQuery] IncidentStatus? status,
        IncidentQueries incidentQueries,
        CancellationToken cancellationToken)
    {
        var incidents = await incidentQueries.GetListAsync(status, cancellationToken);
        return Results.Ok(incidents);
    }

    private static async Task<IResult> GetDetailsAsync(
        [FromRoute] Guid id,
        IncidentQueries incidentQueries,
        CancellationToken cancellationToken)
    {
        var incident = await incidentQueries.GetDetailsAsync(id, cancellationToken);
        return incident is not null
            ? Results.Ok(incident)
            : Results.NotFound();
    }

    private static async Task<IResult> GetSeveritySummaryAsync(
        [FromQuery] IncidentStatus? status,
        IncidentQueries incidentQueries,
        CancellationToken cancellationToken)
    {
        var summary = await incidentQueries.GetSeveritySummaryAsync(status, cancellationToken);
        return Results.Ok(summary);
    }
}
