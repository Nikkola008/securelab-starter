дентифікація стану
Дисципліна: «Системи електронного підпису та управління ключами» / «Прикладні технології програмування в інформаційній безпеці»
.
Тема: «Запуск, дослідження та невелике розширення готової вебсистеми»
.
Виконав: студент групи КБ-41 Марчук Микола Васильович
.
Варіант: 2-A «Трекер інцидентів»
.
Робоча гілка: lab/1-system
.
Основна гілка: main
.
Фінальний тег: v0.1.0
.
Commit hash: 4aa18b3
.
2. Змінений маршрут
Маршрут проходження запиту від дії користувача у браузері до баз даних PostgreSQL і назад виглядає наступним чином
:
Дія у браузері: Користувач натискає кнопку завантаження підсумку інцидентів у файлі Client/index.html
.
Клієнтський JavaScript: Обробник події викликає асинхронну функцію loadSeveritySummary() у Client/app.js
. Функція переводить статус у стан «Завантаження…»
 та виконує HTTP-запит GET /api/incidents/severity-summary через обгортку apiFetch()
.
Presentation Layer (Minimal API Endpoint): Маршрутизатор ASP.NET Core спрямовує запит до маппінгу group.MapGet("/severity-summary", GetSeveritySummaryAsync) у файлі src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs
. Ендпоінт отримує екземпляр IncidentQueries через Dependency Injection (DI)
.
Application Layer (Query Service): Метод GetSeveritySummaryAsync у src/SecureLab.Api/Application/Incidents/IncidentQueries.cs починає read-only запит
.
Data Layer (EF Core DbContext & СУБД): Запит виконується через SecureLabDbContext.Incidents із використанням AsNoTracking()
. Entity Framework Core транслює LINQ-оператор GroupBy(i => i.Severity) у SQL-запит SELECT severity, COUNT(*)::integer FROM incidents GROUP BY severity і надсилає його до PostgreSQL
.
Матеріалізація та DTO Проєкція: Отриманий з БД агрегат матеріалізується у пам'ять C#
. Для дотримання Data Minimization дані проєктуються у спеціальний контракт IncidentSeveritySummaryResponse(string Severity, int Count) у src/SecureLab.Api/Presentation/Contracts/IncidentResponses.cs
. Відсутні в БД категорії Enum заповнюються нулями через Enum.GetValues<IncidentSeverity>(), після чого відбувається впорядкування за числовим доменним рангом GetSeverityRank()
.
HTTP Response: Ендпоінт повертає HTTP-відповідь зі статусом 200 OK та заголовоком Content-Type: application/json
.
Рендеринг у DOM: Клієнтський скрипт app.js отримує JSON-масив і безпечно створює елементи списку <li> через document.createElement("li"), записуючи текст за допомогою властивості textContent
.
Ключові фрагменти коду:
Presentation Layer (IncidentEndpoints.cs):
group.MapGet("/severity-summary", GetSeveritySummaryAsync)
    .Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>(StatusCodes.Status200OK);

private static async Task<IResult> GetSeveritySummaryAsync(
    IncidentQueries incidentQueries,
    CancellationToken cancellationToken)
{
    var summary = await incidentQueries.GetSeveritySummaryAsync(cancellationToken);
    return Results.Ok(summary);
}
``` [19, 21, 37]

#### **Application Layer (`IncidentQueries.cs`):**
```csharp
public async Task<IReadOnlyList<IncidentSeveritySummaryResponse>> GetSeveritySummaryAsync(
    CancellationToken cancellationToken)
{
    var existingGroups = await dbContext.Incidents
        .AsNoTracking()
        .GroupBy(incident => incident.Severity)
        .Select(group => new IncidentSeveritySummaryResponse(
            group.Key.ToString(),
            group.Count()))
        .ToListAsync(cancellationToken);

    var countsBySeverity = existingGroups
        .ToDictionary(item => item.Severity, item => item.Count);

    var allLevels = Enum.GetValues<IncidentSeverity>();

    var completeSummary = allLevels
        .Select(level => new IncidentSeveritySummaryResponse(
            level.ToString(),
            countsBySeverity.GetValueOrDefault(level.ToString(), 0)))
        .ToList();

    var orderedSummary = completeSummary
        .OrderBy(item => GetSeverityRank(item.Severity))
        .ToList();

    logger.LogInformation(
        "Severity summary generated with {GroupCount} severity levels",
        orderedSummary.Count);

    return orderedSummary;
}

private static int GetSeverityRank(string severity) => severity switch
{
    nameof(IncidentSeverity.Critical) => 0,
    nameof(IncidentSeverity.High) => 1,
    nameof(IncidentSeverity.Medium) => 2,
    nameof(IncidentSeverity.Low) => 3,
    _ => int.MaxValue,
};
``` [19, 24, 31]

#### **Response Contract (`IncidentResponses.cs`):**
```csharp
public sealed record IncidentSeveritySummaryResponse(
    string Severity,
    int Count);
``` [30, 31]

#### **Client DOM Sink (`app.js`):**
```csharp
summaryStatusElement.textContent = "";
for (const item of summary) {
  const li = document.createElement("li");
  li.textContent = `${item.severity}: ${item.count}`;
  summaryListElement.append(li);
}
``` [34, 35]

---

## 3. Виконані зміни

1. **Створення Response DTO (`IncidentResponses.cs`):** Додано типізований запис `IncidentSeveritySummaryResponse` із полями `Severity` та `Count` [30, 31]. Це забезпечує вимогу **Data Minimization** і унеможливлює витік полів сутності `Incident` (таких як `OwnerUserId`, `Description` або `email`) [30, 32, 38].
2. **Реалізація Query-сервісу (`IncidentQueries.cs`):** Реалізовано метод `GetSeveritySummaryAsync` з асинхронним викликом `AsNoTracking()`, `GroupBy` та `Count()` [19, 24, 25]. Додано обробку політики «повного переліку рівнів» через `Enum.GetValues<IncidentSeverity>()` для відображення нульових груп (наприклад, `Critical: 0`) [19, 24, 28]. Вирішено проблему алфавітного сортування PostgreSQL (`VARCHAR`) за допомогою функції ранжування `GetSeverityRank()` у C# [19, 24, 28, 29].
3. **Заміна baseline 501 на робочий Endpoint (`IncidentEndpoints.cs`):** Замінено заготовку `501 Not Implemented` на асинхронний метод `GetSeveritySummaryAsync`, підключений через DI [18, 19, 21, 37]. Додано метадані опису відповіді `.Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>()` [19, 21, 37].
4. **Розширення браузерного клієнта (`index.html` & `app.js`):** У `index.html` додано семантичні елементи UI (кнопку, статус та список) [14, 23]. У `app.js` реалізовано функцію `loadSeveritySummary()`, яка безпечно оновлює DOM через `textContent` та підтримує чотири стани UI: «Завантаження…», «Даних немає», успішний вивід та безпечну обробку помилок у блоці `catch` без витоку стек-трейсу [15-17, 34, 35].
5. **Структуроване журналювання:** Додано виклик `logger.LogInformation("Severity summary generated with {GroupCount} severity levels", orderedSummary.Count)` без журналювання чутливих даних або токенів [19, 39-41].

---

## 4. Перевірка

| ID | Передумови | Дія | Очікувано | Фактично | Доказ |
|---|---|---|---|---|---|
| **T-01** | PostgreSQL healthy, API запущено | `GET /health` | Status 200, JSON `{"status":"ready"}` | Status 200 OK, `{"status":"ready"}` | HTTP response [42, 43] |
| **T-02** | Відновлений seed | `GET /api/incidents?status=Triaged` | Status 200, масив із 1 елементом ("Medium", "Triaged") | Status 200 OK, масив відповідає фільтру | HTTP response / DevTools [43, 44] |
| **T-03** | Відновлений seed | `GET /api/incidents?status=Resolved` | Status 200 з порожнім масивом `[]` | Status 200 OK, тіло відповіді `[]` | HTTP response у `.http` [43, 45] |
| **T-04** | Відновлений seed | `GET /api/incidents/99999999-9999-9999-9999-999999999999` | Status 404 Problem Details | Status 404 Not Found, `application/problem+json` з `traceId` | HTTP response / DevTools [43, 46, 47] |
| **T-05** | Відновлений seed | `GET /api/incidents?status=Unknown` | Status 400 Validation Problem Details | Status 400 Bad Request, `application/problem+json` | HTTP response у `.http` [43, 45, 48] |
| **T-06** | Реалізовано Етап 3, відновлений seed | `GET /api/incidents/severity-summary` | Status 200 OK, масив із 4 груп (Critical: 0, High: 1, Medium: 1, Low: 1) у доменному порядку | Status 200 OK, JSON-масив з 4 об'єктів у стабільному порядку | HTTP response у `.http` [33, 45, 49, 50] |
| **T-07** | Етап 3 реалізовано, API та UI запущено | Натиснути кнопку «Підсумок за severity» у браузері | UI показує «Завантаження…», а потім безпечно виводить результат у `<li>` | UI відображає підсумок через `textContent`, у DevTools запит `200 OK` | DevTools Network + скриншот UI [15, 34, 35, 45, 51] |
| **T-08** | Після експериментів із даними | Виконати `--reset-database`, повторити T-02 та T-06 | Стенд повертається до початкового seed-стану | Базу даних очищено та заповнено початковими seed-даними | Термінал `dotnet run -- --reset-database` [45, 48, 52] |

---

## 5. Security-сценарій

У цій лабораторній роботі досліджувалися та забезпечувалися наступні межі довіри (Trust Boundaries) і безпекові мехінізми [53, 54]:

1. **Межа Браузер \\(\rightarrow\\) API:**
   - **Дані:** URL-параметри (`id`, `status`), заголовки запиту [53, 54].
   - **Ризик:** Недовіра до клієнтського вводу (передача невалідних GUID або довільних рядків) [53, 54].
   - **Контроль:** Маршрутне обмеження `/{id:guid}` у Minimal API захищає роутинг від неформатних UUID [20, 53, 55], а `Enum.TryParse` повертає статус `400 Validation Problem Details` для некоректних значень [43, 53, 56].
2. **Межа API \\(\rightarrow\\) PostgreSQL:**
   - **Дані:** LINQ-запити та параметризовані значення [53, 54].
   - **Ризик:** Навантаження бази даних та ризик модифікації об'єктів [53, 54].
   - **Контроль:** Параметризація запитів у EF Core запобігає SQLi [53, 54], а використання `.AsNoTracking()` вимикає відстеження змін для read-only операцій [19, 32, 53, 57].
3. **Межа API \\(\rightarrow\\) Браузер:**
   - **Дані:** HTTP-відповідь (JSON DTO), заголовки [53, 54].
   - **Ризик:** Витік чутливих даних (PII) та MIME-sniffing атаки [22, 32, 53, 54].
   - **Контроль:** Застосування DTO (`IncidentSeveritySummaryResponse`, `IncidentDetailsResponse`) приховує `ownerUserId`, `email` та внутрішні коментарі [30, 32, 38, 58]. У `Program.cs` додано захисні заголовки `X-Content-Type-Options: nosniff` та `Referrer-Policy: no-referrer` [22, 53, 59].
4. **Межа Дані response \\(\rightarrow\\) DOM:**
   - **Дані:** Текстові поля JSON (`description`, `title` тощо) [53, 54].
   - **Ризик:** Stored XSS-атаки при виводі даних, збережених у базі (наприклад, вміст тегів `<script>` у початковому інциденті «Перевірка журналу комп'ютерного класу») [36, 53, 60-62].
   - **Контроль:** Рендеринг у `app.js` виконується виключно через безпечні DOM API — `textContent` та `document.createTextNode()` [34-36, 53, 60, 62]. Використання небезпечного sink `innerHTML` суворо заборонено [62-64].

---

## 6. Висновок

У ході виконання лабораторної роботи № 1 було успішно розгорнуто та досліджено локальний відтворювальний стенд вебсистеми SecureLab у складі Docker (PostgreSQL), ASP.NET Core Minimal API та Vanilla JS браузерного клієнта [65-68]. Простежено наскрізний шлях виконання HTTP-запиту від дій користувача у браузері до виконання SQL-запитів СУБД PostgreSQL та зворотної побудови DOM-вузлів [11, 12, 68].

У рамках Етапу 3 реалізовано наскрізне розширення системи — агрегований ендпоінт `GET /api/
