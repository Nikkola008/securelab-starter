# Карта архітектури

Це початкова карта. Під час ЛР 1 доповніть її власним трасуванням запиту,
конкретними файлами та спостереженнями з DevTools і журналу PostgreSQL.

## Компоненти

| Компонент | Розташування | Відповідальність |
|---|---|---|
| Browser client | `src/SecureLab.Api/Client/` | Надсилає HTTP-запити, безпечно показує відповідь через DOM API |
| Presentation | `Presentation/` | Описує endpoints, читає зовнішні параметри, формує HTTP-відповідь |
| Application | `Application/` | Виконує сценарій отримання списку або деталей інциденту |
| Data | `Data/` | Відображає C#-сутності на PostgreSQL через EF Core/Npgsql |
| PostgreSQL | `infra/compose.yaml` | Зберігає навчальні дані у локальному контейнері |

## Підготовлений наскрізний маршрут

```text
submit/click у Client/app.js
  → GET /api/incidents або GET /api/incidents/{id}
  → Presentation/Endpoints/IncidentEndpoints.cs
  → Application/Incidents/IncidentQueries.cs
  → Data/SecureLabDbContext.cs
  → PostgreSQL
  → response DTO у Presentation/Contracts/
  → JSON
  → textContent/createTextNode у Client/app.js
```

## Межі довіри

Доповніть таблицю щонайменше трьома конкретними спостереженнями.

| Межа | Чому даним ще не можна довіряти | Де перевіряємо або обмежуємо |
|---|---|---|
| Користувач → Browser client | Користувач контролює введення | TODO |
| Browser client → API | Клієнт і HTTP-запит можна змінити поза UI | TODO |
| PostgreSQL → API → DOM | У БД може зберігатися раніше введений недовірений текст | DTO та безпечний DOM sink; доповнити |

## Конфігураційні входи

- `global.json` — версія .NET SDK;
- `src/SecureLab.Api/appsettings*.json` — режим міграцій і локальний connection string;
- `infra/compose.yaml` — версія PostgreSQL, порт і локальні навчальні облікові дані;
- змінна середовища `ConnectionStrings__SecureLab` — безпечний спосіб перевизначити connection string поза репозиторієм.

- # Посібник та шпаргалка для захисту Лабораторної роботи №1
**Тема:** «Запуск, дослідження та невелике розширення готової вебсистеми»  
**Проєкт:** SecureLab (Deployment and Expansion of a Signature Management Web System)  
**Дисципліна:** Прикладні технології програмування в інформаційній безпеці / Системи електронного підпису та управління ключами  

---

## 1. Команди для швидкої перевірки та діагностики стенда

Виконуйте ці команди у терміналі для демонстрації готовності системи перед викладачем:

### 1.1. Перевірка Git-гілки та стану SDK
```bash
# Перевірка активної гілки (має бути lab/1-system або main)
git branch --show-current

# Перевірка сумісності .NET SDK (потрібна смуга 10.0.3xx, наприклад 10.0.302/10.0.303)
dotnet --version
```

### 1.2. Запуск PostgreSQL у Docker Compose
```bash
# Запуск контейнера бази даних PostgreSQL у фоновому режимі та очікування healthcheck
docker compose --env-file infra/.env.example -f infra/compose.yaml up -d --wait

# Перевірка стану контейнера (має бути (healthy))
docker compose --env-file infra/.env.example -f infra/compose.yaml ps
```

### 1.3. Запуск Web API
```bash
# Відновлення залежностей та запуск Minimal API проєкту
dotnet restore SecureLab.sln
dotnet run --project src/SecureLab.Api
```

### 1.4. Основні URL-адреси для перевірки у браузері
* **Вебклієнт (UI):** `http://localhost:5080/`
* **Readiness Endpoint (Healthcheck):** `http://localhost:5080/health` *(Повертає `{"status":"ready"}` при успішному з'єднанні з БД)*
* **Інтерактивна OpenAPI-документація (Scalar):** `http://localhost:5080/scalar/v1`
* **OpenAPI Schema (JSON):** `http://localhost:5080/openapi/v1.json`

### 1.5. Автоматизоване тестування та відновлення відомого стану (Seed / Reset)
```bash
# Скидання та повторне заповнення навчальної бази даних початковими seed-даними
dotnet run --project src/SecureLab.Api -- --reset-database

# Запуск усіх інтеграційних та безпекових тестів (Release конфігурація)
dotnet test tests/SecureLab.Api.Tests/SecureLab.Api.Tests.csproj --configuration Release

# Альтернативний запуск тестів однією командою (на Linux/macOS або Git Bash)
bash scripts/test.sh
```

---

## 2. Повний наскрізний маршрут запиту (Request Route)

Маршрут перегляду деталей інциденту (`GET /api/incidents/{id}`):

| Етап | Компонент / Файл | Що відбувається на етапі |
| :--- | :--- | :--- |
| **1. UI Event** | `Client/index.html`, `Client/app.js` | Клік по картці інциденту в DOM викликає обробник `loadIncidentDetails(id)`. |
| **2. Network Request** | `Client/app.js` (`apiFetch`) | Браузер надсилає HTTP GET `/api/incidents/20000000-0000-0000-0000-000000000003`. |
| **3. API Routing** | `Presentation/Endpoints/IncidentEndpoints.cs` | Kestrel співставляє Method + URL. Маршрут `:{id:guid}` валідує формат UUID. |
| **4. App Logic** | `Application/Incidents/IncidentQueries.cs` | Виконується `GetDetailsAsync`. Використовується `.AsNoTracking()`, фільтрація за `id` та проєкція у DTO. |
| **5. ORM Mapping** | `Data/SecureLabDbContext.cs` | EF Core зчитує маппінг `.ToTable("incidents")` та транслює LINQ у параметризований SQL. |
| **6. Database** | PostgreSQL (`incidents` table) | СУБД виконує `SELECT ... FROM incidents WHERE id = ...` та повертає кортеж даних. |
| **7. Response DTO** | `Presentation/Contracts/IncidentResponses.cs` | Сервер будує `IncidentDetailsResponse` (без `ownerUserId`, `email`, `isInternal`) і повертає `200 OK` JSON. |
| **8. DOM Render** | `Client/app.js` (`renderIncidentDetails`) | Дані безпечно виводяться в DOM через `textContent` та `document.createTextNode` (захист від XSS). |

---

## 3. Найважливіші фрагменти коду проєкту

### 3.1. Presentation Layer: Контракти відповідей (Response DTOs)
📁 `src/SecureLab.Api/Presentation/Contracts/IncidentResponses.cs`
```csharp
namespace SecureLab.Api.Presentation.Contracts;

// Контракт для списку інцидентів
public sealed record IncidentListItemResponse(
    Guid Id,
    string Title,
    string Severity,
    string Status,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset CreatedAtUtc);

// Деталі інциденту (Data Minimization: без ownerUserId та email)
public sealed record IncidentDetailsResponse(
    Guid Id,
    string Title,
    string Description,
    string Severity,
    string Status,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset CreatedAtUtc,
    string OwnerDisplayName,
    IReadOnlyList<IncidentCommentResponse> Comments);

public sealed record IncidentCommentResponse(
    Guid Id,
    string AuthorDisplayName,
    string Text,
    DateTimeOffset CreatedAtUtc);

// Новий контракт точки розширення (Етап 3)
public sealed record IncidentSeveritySummaryResponse(
    string Severity,
    int Count);
```

### 3.2. Application Layer: Read-only запити та матеріалізація
📁 `src/SecureLab.Api/Application/Incidents/IncidentQueries.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Application.Incidents;

public sealed class IncidentQueries(SecureLabDbContext dbContext, ILogger<IncidentQueries> logger)
{
    // Отримання підсумку за рівнем небезпеки
    public async Task<IReadOnlyList<IncidentSeveritySummaryResponse>> GetSeveritySummaryAsync(
        CancellationToken cancellationToken)
    {
        // 1. Read-only запит до БД з вимкненим відстеженням
        var existingGroups = await dbContext.Incidents
            .AsNoTracking()
            .GroupBy(incident => incident.Severity)
            .Select(group => new IncidentSeveritySummaryResponse(
                group.Key.ToString(),
                group.Count()))
            .ToListAsync(cancellationToken);

        var countsBySeverity = existingGroups
            .ToDictionary(item => item.Severity, item => item.Count);

        // 2. Матеріалізація всіх enum-значень для доповнення порожніх груп (Critical: 0)
        var allLevels = Enum.GetValues<IncidentSeverity>();
        var completeSummary = allLevels
            .Select(level => new IncidentSeveritySummaryResponse(
                level.ToString(),
                countsBySeverity.GetValueOrDefault(level.ToString(), 0)))
            .ToList();

        // 3. Застосування доменного бізнес-порядку сортування
        var orderedSummary = completeSummary
            .OrderBy(item => GetSeverityRank(item.Severity))
            .ToList();

        // 4. Структуроване логування
        logger.LogInformation(
            "Severity summary generated with {GroupCount} severity levels",
            orderedSummary.Count);

        return orderedSummary;
    }

    // Політика ранжування: Critical -> High -> Medium -> Low
    private static int GetSeverityRank(string severity) => severity switch
    {
        nameof(IncidentSeverity.Critical) => 0,
        nameof(IncidentSeverity.High) => 1,
        nameof(IncidentSeverity.Medium) => 2,
        nameof(IncidentSeverity.Low) => 3,
        _ => int.MaxValue,
    };
}
```

### 3.3. Presentation Layer: Minimal API Endpoints
📁 `src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs`
```csharp
public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/incidents");

        // Отримання деталей із маршрутним обмеженням :guid
        group.MapGet("/{id:guid}", GetDetailsAsync)
            .Produces<IncidentDetailsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Реалізований ендпоінт підсумку
        group.MapGet("/severity-summary", GetSeveritySummaryAsync)
            .Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<IResult> GetSeveritySummaryAsync(
        IncidentQueries incidentQueries,
        CancellationToken cancellationToken)
    {
        var summary = await incidentQueries.GetSeveritySummaryAsync(cancellationToken);
        return Results.Ok(summary);
    }
}
```

### 3.4. Frontend Layer: Безпечний вивід у DOM (XSS Prevention)
📁 `src/SecureLab.Api/Client/app.js`
```javascript
// Реалізація 4 станів UI у функціоналі підсумку
async function loadSeveritySummary() {
  // 1. Loading State
  summaryStatusElement.textContent = "Завантаження…";
  summaryListElement.replaceChildren();

  try {
    // 2. Network Request
    const summary = await apiFetch("/api/incidents/severity-summary");

    // 3. Empty State
    if (summary.length === 0) {
      summaryStatusElement.textContent = "Даних немає";
      return;
    }

    summaryStatusElement.textContent = "";
    for (const item of summary) {
      const li = document.createElement("li");
      // Безпечний вивід через textContent
      li.textContent = `${item.severity}: ${item.count}`;
      summaryListElement.append(li);
    }
  } catch (error) {
    // 4. Error State (без витоку стек-трейсу)
    summaryStatusElement.textContent = "Не вдалося завантажити підсумок";
  }
}

// Захист від XSS при рендерингу тексту коментарів та опису
function renderIncidentDetails(incident) {
  // ...
  for (const comment of incident.comments) {
    const item = document.createElement("li");
    item.append(
      createTextElement("strong", `${comment.authorDisplayName}: `),
      document.createTextNode(comment.text) // Буквальний текстовий вузол
    );
    comments.append(item);
  }
}
```

---

## 4. Самостійно виявлена та усунута проблема Starter (для "Відмінного рівня")

При захисті на **«Відмінний рівень» (10 балів)** наголосіть на виявлених дефектах початкового шаблону та їхньому вирішенні у `IncidentQueries.cs`:

1. **Втрата нульових груп у SQL `GroupBy`**:
   * *Проблема starter:* Запит EF Core `.GroupBy(i => i.Severity)` формує SQL `GROUP BY severity`. СУБД повертає лише ті групи, які фактично є в базі. Оскільки в початковому seed немає інцидентів з рівнем `Critical`, ця група зникала з відповіді.
   * *Усунення:* Після матеріалізації даних з БД застосовано `Enum.GetValues<IncidentSeverity>()` для гарантованого заповнення відсутньої групи нулем (`Critical: 0`).

2. **Лексикографічне (алфавітне) сортування СУБД**:
   * *Проблема starter:* Поле `Severity` зберігається у PostgreSQL як рядок (`.HasConversion<string>()`). Замовчувальне сортування за ключем групи в SQL є алфавітним (`High` -> `Low` -> `Medium`).
   * *Усунення:* Впроваджено функцію ранжування `GetSeverityRank(...)`, яка впорядковує результат за доменним бізнес-пріоритетом (`Critical` -> `High` -> `Medium` -> `Low`).

---

## 5. Межі довіри (Trust Boundaries) у системі

| Межа / Перехід | Дані, що перетинають | Ризики / Хибні припущення | Контроль у маршруті SecureLab |
| :--- | :--- | :--- | :--- |
| **1. Браузер -> API** | URL-параметри (`id`, `status`), JSON payload | Невірна форма даних, злам UI, підроблений GUID. | Ограничение роутингу `:{id:guid}`, сервісна валідація enum, `400 Bad Request` / `404 Not Found`. |
| **2. API -> PostgreSQL** | Параметризовані LINQ-запити | SQL-ін'єкції, витік пам'яті, зміна даних при читанні. | Параметризація LINQ у EF Core, виклик `.AsNoTracking()`, явна проєкція `.Select()`. |
| **3. API -> Браузер** | HTTP-відповідь (JSON DTO), заголовки | Витік чутливих PII полів (email, password hash), MIME-sniffing. | **Data Minimization** через DTO (`IncidentDetailsResponse`), заголовки `X-Content-Type-Options: nosniff`. |
| **4. Response -> DOM** | Поля JSON (`description`, коментарі) | Stored XSS атаки (вставка шкідливого `<script>`). | Рендеринг строго через `textContent` та `document.createTextNode` замість `innerHTML`. |
| **5. Config -> API** | Connection String, секрети | Потрапляння секретів у Git, невідповідність середовищ. | Читання секретів зі змінних середовища (`$env:ConnectionStrings__SecureLab`), `.gitignore` для `.env`. |

---

## 6. Шпаргалка до «живої зміни коду» (Live Modification) на захисті

Якщо викладач дасть експрес-завдання на 2–3 хвилини:

* **Зміна сортування на протилежне:**
  У `IncidentQueries.cs` у методі `GetSeverityRank` змініть повернені значення (`Low => 0`, `Critical => 3`) або у `GetSeveritySummaryAsync` використайте `.OrderByDescending(item => GetSeverityRank(item.Severity))`.
* **Зміна тексту помилки на фронтенді:**
  У `app.js` у блоці `catch` функції `loadSeveritySummary()` змініть `summaryStatusElement.textContent = "Новий текст помилки"`.
* **Зміна формату відображення списку:**
  У `app.js` у циклі `for (const item of summary)` змініть `li.textContent = `[${item.severity}] - ${item.count} шт.``.

