using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Tests;

public sealed class IncidentCreationContractTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_WithMissingRequiredFields_Returns400ProblemDetails()
    {
        using var response = await PostCreateAsync(new { });

        await AssertValidationProblemAsync(response, "title", "description", "severity", "occurredAtUtc");
    }

    [Fact]
    public async Task Create_WithTitleLongerThan160Characters_Returns400()
    {
        using var response = await PostCreateAsync(new
        {
            title = new string('t', 161),
            description = "Коректний опис.",
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow
        });

        await AssertValidationProblemAsync(response, "title");
    }

    [Fact]
    public async Task Create_WithDescriptionLongerThan4000Characters_Returns400()
    {
        using var response = await PostCreateAsync(new
        {
            title = "Перевірка максимальної довжини опису",
            description = new string('d', 4001),
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow
        });

        await AssertValidationProblemAsync(response, "description");
    }

    [Fact]
    public async Task Create_WithNumericSeverity_Returns400ProblemDetails()
    {
        using var response = await PostCreateAsync(new
        {
            title = "Перевірка числового severity",
            description = "Коректний опис для перевірки severity.",
            severity = "7",
            occurredAtUtc = DateTimeOffset.UtcNow
        });

        await AssertValidationProblemAsync(response, "severity");
    }

    [Fact]
    public async Task Create_WithoutOccurredAtUtc_Returns400ProblemDetails()
    {
        using var response = await PostCreateAsync(new
        {
            title = "Перевірка обов’язкової дати",
            description = "Коректний опис для перевірки дати.",
            severity = "Low"
        });

        await AssertValidationProblemAsync(response, "occurredAtUtc");
    }

    [Fact]
    public async Task Create_WithDateMoreThanFiveMinutesInFuture_Returns400()
    {
        using var response = await PostCreateAsync(new
        {
            title = "Перевірка майбутньої дати",
            description = "Коректний опис для перевірки дати.",
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(10)
        });

        await AssertValidationProblemAsync(response, "occurredAtUtc");
    }

    [Fact]
    public async Task Create_HighWith39CharactersAfterTrim_Returns400()
    {
        using var response = await PostCreateAsync(new
        {
            title = $"High short {Guid.NewGuid():N}",
            description = $"  {new string('x', 39)}  ",
            severity = "High",
            occurredAtUtc = DateTimeOffset.UtcNow
        });

        await AssertValidationProblemAsync(response, "description");
    }

    [Fact]
    public async Task Create_HighWith40CharactersAfterTrim_Returns201AndUsesServerFields()
    {
        var normalizedTitle = $"High valid {Guid.NewGuid():N}";
        using var response = await PostCreateAsync(new
        {
            title = $"  {normalizedTitle}  ",
            description = $"  {new string('x', 40)}  ",
            severity = "High",
            occurredAtUtc = DateTimeOffset.UtcNow,
            ownerUserId = Guid.NewGuid(),
            status = "Closed"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(normalizedTitle, root.GetProperty("title").GetString());
        Assert.Equal("High", root.GetProperty("severity").GetString());
        Assert.Equal("New", root.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("occurredAtUtc", out _));
        Assert.True(root.TryGetProperty("createdAtUtc", out _));
        Assert.False(root.TryGetProperty("description", out _));
        Assert.False(root.TryGetProperty("ownerUserId", out _));
        Assert.False(root.TryGetProperty("updatedAtUtc", out _));

        var id = root.GetProperty("id").GetGuid();
        using var detailsResponse = await _client.GetAsync($"/api/incidents/{id}");
        using var detailsDocument = JsonDocument.Parse(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Equal("New", detailsDocument.RootElement.GetProperty("status").GetString());
        Assert.False(detailsDocument.RootElement.TryGetProperty("ownerUserId", out _));
    }

    [Fact]
    public async Task Create_WithTrimmedTitleMatchingActiveSeedIncident_Returns409ProblemDetails()
    {
        using var response = await PostCreateAsync(new
        {
            title = "  Підозрілий лист із вкладенням  ",
            description = "Повторна спроба створити інцидент із наявною активною назвою.",
            severity = "Medium",
            occurredAtUtc = DateTimeOffset.UtcNow
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Активний інцидент із такою назвою вже зареєстровано.", body);
        Assert.DoesNotContain("Npgsql", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecureLab.Api.Data", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WithTitleMatchingClosedIncident_Returns201()
    {
        var title = $"Closed incident {Guid.NewGuid():N}";
        var closedIncidentId = Guid.NewGuid();
        Guid? createdIncidentId = null;

        using (var setupScope = factory.Services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
            var now = DateTimeOffset.UtcNow;

            db.Incidents.Add(new Incident
            {
                Id = closedIncidentId,
                OwnerUserId = DbSeeder.AliceId,
                Title = title,
                Description = "Закритий інцидент для перевірки предметного правила.",
                Severity = IncidentSeverity.Low,
                Status = IncidentStatus.Closed,
                OccurredAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            await db.SaveChangesAsync();
        }

        try
        {
            using var response = await PostCreateAsync(new
            {
                title = $"  {title}  ",
                description = "Новий інцидент з title, який має збіг лише з Closed записом.",
                severity = "Medium",
                occurredAtUtc = DateTimeOffset.UtcNow
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            createdIncidentId = document.RootElement.GetProperty("id").GetGuid();
        }
        finally
        {
            using var cleanupScope = factory.Services.CreateScope();
            var db = cleanupScope.ServiceProvider.GetRequiredService<SecureLabDbContext>();

            if (createdIncidentId is Guid id)
            {
                await db.Incidents.Where(item => item.Id == id).ExecuteDeleteAsync();
            }

            await db.Incidents.Where(item => item.Id == closedIncidentId).ExecuteDeleteAsync();
        }
    }

    private Task<HttpResponseMessage> PostCreateAsync(object request) =>
        _client.PostAsJsonAsync("/api/incidents", request);

    private static async Task AssertValidationProblemAsync(
        HttpResponseMessage response,
        params string[] expectedErrorFields)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var errors = document.RootElement.GetProperty("errors");

        foreach (var field in expectedErrorFields)
        {
            Assert.True(errors.TryGetProperty(field, out _), $"Відсутня помилка поля '{field}'.");
        }

        Assert.DoesNotContain("Npgsql", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecureLab.Api.Data", body, StringComparison.OrdinalIgnoreCase);
    }
}
