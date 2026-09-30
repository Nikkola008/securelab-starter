using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Scaffolding;

// Навчальний старт ЛР 02. Запускати лише з локальними штучними даними.
public static class Lab02Endpoints
{
    public static void MapLab02Endpoints(this WebApplication app)
    {
        app.MapGet("/api/incidents/search", async (string? q, string? sortBy, SecureLabDbContext db, CancellationToken ct) =>
        {
            var order = sortBy switch
            {
                null or "" or "createdAtUtc" => "created_at_utc DESC",
                "severity" => "severity", "status" => "status", _ => sortBy
            };
            var sql = "SELECT * FROM incidents WHERE title ILIKE '%" + (q ?? "")
                + "%' OR description ILIKE '%" + (q ?? "") + "%' ORDER BY " + order + " LIMIT 50";
            var rows = await db.Incidents.FromSqlRaw(sql).AsNoTracking().ToListAsync(ct);
            return Results.Ok(rows.Select(row => new
            {
                row.Id, row.Title, row.Description,
                Severity = row.Severity.ToString(), Status = row.Status.ToString(), row.CreatedAtUtc
            }));
        });
        app.MapPost("/api/incidents", async (CreateIncidentRequest request, SecureLabDbContext db, CancellationToken ct) =>
        {
            // ЛР 02: доповніть server validation, business rules та response contract.
            var now = DateTimeOffset.UtcNow;
            var errors = new Dictionary<string, string[]>();
            IncidentSeverity severity = default;

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                errors["title"] = ["Title є обов’язковим."];
            }
            else if (request.Title.Length > 160)
            {
                errors["title"] = ["Title не може бути довшим за 160 символів."];
            }

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                errors["description"] = ["Description є обов’язковим."];
            }
            else if (request.Description.Length > 4000)
            {
                errors["description"] = ["Description не може бути довшим за 4000 символів."];
            }

            if (string.IsNullOrWhiteSpace(request.Severity)
                || !Enum.TryParse(request.Severity, ignoreCase: true, out severity)
                || !Enum.IsDefined(severity))
            {
                errors["severity"] = ["Допустимі значення: Low, Medium, High, Critical."];
            }

            if (request.OccurredAtUtc is null)
            {
                errors["occurredAtUtc"] = ["OccurredAtUtc є обов’язковим."];
            }
            else if (request.OccurredAtUtc.Value > now.AddMinutes(5))
            {
                errors["occurredAtUtc"] = ["OccurredAtUtc не може бути більш ніж на 5 хвилин у майбутньому."];
            }

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var title = request.Title!.Trim();
            var description = request.Description!.Trim();

            if (severity is IncidentSeverity.High or IncidentSeverity.Critical
                && description.Length < 40)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["description"] = ["Для рівнів High і Critical опис має містити щонайменше 40 символів."]
                });
            }

            var blockingStatuses = new[]
            {
                IncidentStatus.New,
                IncidentStatus.Triaged,
                IncidentStatus.InProgress,
                IncidentStatus.Resolved
            };

            var hasDuplicateTitle = await db.Incidents.AnyAsync(
                item => blockingStatuses.Contains(item.Status) && item.Title.Trim() == title,
                ct);

            if (hasDuplicateTitle)
            {
                return Results.Problem(
                    title: "Інцидент уже існує",
                    detail: "Активний інцидент із такою назвою вже зареєстровано.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var incident = new Incident
            {
                Id = Guid.NewGuid(), OwnerUserId = DbSeeder.AliceId,
                Title = title, Description = description,
                Severity = severity,
                Status = IncidentStatus.New, OccurredAtUtc = request.OccurredAtUtc!.Value,
                CreatedAtUtc = now, UpdatedAtUtc = now
            };
            db.Incidents.Add(incident);
            await db.SaveChangesAsync(ct);
            var response = new CreatedIncidentResponse(
                incident.Id,
                incident.Title,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.OccurredAtUtc,
                incident.CreatedAtUtc);

            return Results.Created($"/api/incidents/{incident.Id}", response);
        })
        .Produces<CreatedIncidentResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

public sealed record CreateIncidentRequest(
    string? Title, string? Description, string? Severity, DateTimeOffset? OccurredAtUtc);
