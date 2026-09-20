# Звіт до лабораторної роботи № 1
**з дисципліни «Системи електронного підпису та управління ключами»**  
**на тему:** «Запуск, дослідження та невелике розширення готової вебсистеми»

---

## 1. Ідентифікація стану

- **Дисципліна:** Системи електронного підпису та управління ключами
- **Студент:** Марчук Микола Васильович, група КБ-41 [23]
- **Викладач:** Бабенко Юрій Михайлович [23]
- **Варіант:** 2-A «Трекер інцидентів» [23]
- **Репозиторій:** `https://github.com/Nikkola008/securelab-starter` [23]
- **Робоча гілка:** `lab/1-system` [25, 41]
- **Основна гілка:** `main` [149]
- **Фінальний тег:** `v0.1.0` [23, 80]
- **Commit hash:** `4aa18b3` [162]

---

## 2. Змінений маршрут

Маршрут проходження запиту від дії користувача у браузері до баз даних PostgreSQL і назад виглядає наступним чином:

1. **Дія у браузері:** Користувач натискає кнопку «Підсумок за severity» у браузерному клієнті (`Client/index.html`) [1, 75].
2. **Клієнтський JavaScript:** Обробник події `click` викликає асинхронну функцію `loadSeveritySummary()` у `Client/app.js` [6, 8]. Функція переводить статус у «Завантаження…» та виконує HTTP-запит `GET /api/incidents/severity-summary` через обгортку `apiFetch()` [6].
3. **Presentation Layer (Minimal API Endpoint):** Маршрутизатор ASP.NET Core спрямовує запит до маппінгу `group.MapGet("/severity-summary", GetSeveritySummaryAsync)` у файлі `src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs` [54, 74]. Ендпоінт отримує екземпляр `IncidentQueries` через Dependency Injection (DI) [21, 75].
4. **Application Layer (Query Service):** Метод `GetSeveritySummaryAsync` у `src/SecureLab.Api/Application/Incidents/IncidentQueries.cs` починає read-only запит [14, 15].
5. **Data Layer (EF Core DbContext & СУБД):** Запит виконується через `SecureLabDbContext.Incidents` із використанням `.AsNoTracking()` [15]. Entity Framework Core транслює LINQ-оператор `GroupBy(i => i.Severity)` у SQL-запит `SELECT severity, COUNT(*)::integer FROM incidents GROUP BY severity` і надсилає його до PostgreSQL [15].
6. **Матеріалізація та DTO Проєкція:** Отриманий з БД агрегат матеріалізується у пам'ять C# [15]. Для дотримання Data Minimization дані проєктуються у спеціальний контракт `IncidentSeveritySummaryResponse(string Severity, int Count)` у `src/SecureLab.Api/Presentation/Contracts/IncidentResponses.cs` [10, 15]. Відсутні в БД категорії Enum заповнюються нулями через `Enum.GetValues<IncidentSeverity>()`, після чого відбувається впорядкування за числовим доменним рангом `GetSeverityRank()` [15].
7. **HTTP Response:** Ендпоінт повертає HTTP-відповідь зі статусом `200 OK` та заголовком `Content-Type: application/json` [21, 74].
8. **Рендеринг у DOM:** Клієнтський скрипт `app.js` отримує JSON-масив і безпечно створює елементи списку `<li>` через `document.createElement("li")`, записуючи текст за допомогою властивості `textContent` [7].

### Ключові фрагменти коду:

#### **Presentation Layer (`IncidentEndpoints.cs`):**
```csharp
group.MapGet("/severity-summary", GetSeveritySummaryAsync)
    .Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>(StatusCodes.Status200OK);

private static async Task<IResult> GetSeveritySummaryAsync(
    IncidentQueries incidentQueries,
    CancellationToken cancellationToken)
{
    var summary = await incidentQueries.GetSeveritySummaryAsync(cancellationToken);
    return Results.Ok(summary);
}
```

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
```

#### **Response Contract (`IncidentResponses.cs`):**
```csharp
public sealed record IncidentSeveritySummaryResponse(
    string Severity,
    int Count);
```

#### **Client DOM Sink (`app.js`):**
```javascript
summaryStatusElement.textContent = "";
for (const item of summary) {
  const li = document.createElement("li");
  li.textContent = `${item.severity}: ${item.count}`;
  summaryListElement.append(li);
}
```

---

## 3. Виконані зміни

1. **Створення Response DTO (`IncidentResponses.cs`):** Додано типізований запис `IncidentSeveritySummaryResponse` із полями `Severity` та `Count` [10, 72]. Це забезпечує вимогу **Data Minimization** і унеможливлює витік полів сутності `Incident` (таких як `OwnerUserId`, `Description` або `email`) [57, 72].
2. **Реалізація Query-сервісу (`IncidentQueries.cs`):** Реалізовано метод `GetSeveritySummaryAsync` з асинхронним викликом `AsNoTracking()`, `GroupBy` та `Count()` [15, 72, 73]. Додано обробку політики «повного переліку рівнів» через `Enum.GetValues<IncidentSeverity>()` для відображення нульових груп (наприклад, `Critical: 0`) [15, 71]. Вирішено проблему алфавітного сортування PostgreSQL (`VARCHAR`) за допомогою функції ранжування `GetSeverityRank()` у C# [12, 15, 74].
3. **Заміна baseline 501 на робочий Endpoint (`IncidentEndpoints.cs`):** Замінено заготовку `501 Not Implemented` на асинхронний метод `GetSeveritySummaryAsync`, підключений через DI [74]. Додано метадані опису відповіді `.Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>()` [75].
4. **Розширення браузерного клієнта (`index.html` & `app.js`):** У `index.html` додано семантичні елементи UI (кнопку, статус та список) [1, 75]. У `app.js` реалізовано функцію `loadSeveritySummary()`, яка безпечно оновлює DOM через `textContent` та підтримує чотири стани UI: «Завантаження…», «Даних немає», успішний вивід та безпечну обробку помилок у блоці `catch` без витоку стек-трейсу [6, 7, 76].
5. **Структуроване журналювання:** Додано виклик `logger.LogInformation("Severity summary generated with {GroupCount} severity levels", orderedSummary.Count)` без журналювання чутливих даних або токенів [15, 77].

---

## 4. Перевірка (Матриця сценаріїв)

| ID | Передумови | Дія | Очікувано | Фактично | Доказ |
|---|---|---|---|---|---|
| **T-01** | PostgreSQL healthy, API запущено [31, 33] | `GET /health` [37] | Status 200, JSON `{"status":"ready"}` [37] | Status 200 OK, `{"status":"ready"}` | HTTP response [37] |
| **T-02** | Відновлений seed [40] | `GET /api/incidents?status=Triaged` [37] | Status 200, масив із 1 елементом ("Medium", "Triaged") [37, 199] | Status 200 OK, масив відповідає фільтру | HTTP response / DevTools [37, 199] |
| **T-03** | Відновлений seed [40] | `GET /api/incidents?status=Resolved` [37] | Status 200 з порожнім масивом `[]` [37] | Status 200 OK, тіло відповіді `[]` | HTTP response у `.http` [37] |
| **T-04** | Відновлений seed [40] | `GET /api/incidents/99999999-9999-9999-9999-999999999999` [37] | Status 404 Problem Details [37, 201] | Status 404 Not Found, `application/problem+json` з `traceId` | HTTP response / DevTools [37, 201] |
| **T-05** | Відновлений seed [40] | `GET /api/incidents?status=Unknown` [37] | Status 400 Validation Problem Details [37] | Status 400 Bad Request, `application/problem+json` | HTTP response у `.http` [37] |
| **T-06** | Реалізовано Етап 3, відновлений seed [40, 69] | `GET /api/incidents/severity-summary` [67, 70] | Status 200 OK, масив із 4 груп (Critical: 0, High: 1, Medium: 1, Low: 1) у доменному порядку [71, 80] | Status 200 OK, JSON-масив з 4 об'єктів у стабільному порядку | HTTP response у `.http` [78, 80] |
| **T-07** | Етап 3 реалізовано, API та UI запущено [75] | Натиснути кнопку «Підсумок за severity» у браузері [1, 76] | UI показує «Завантаження…», а потім безпечно виводить результат у `<li>` [6, 7, 76] | UI відображає підсумок через `textContent`, у DevTools запит `200 OK` | DevTools Network + скриншот UI [76, 79] |
| **T-08** | Після експериментів із даними [42] | Виконати `--reset-database`, повторити T-02 та T-06 [40, 42] | Стенд повертається до початкового seed-стану [40, 42] | Базу даних очищено та заповнено початковими seed-даними | Термінал `dotnet run -- --reset-database` [40] |

---

## 5. Security-сценарій (Межі довіри)

У цій лабораторній роботі досліджувалися та забезпечувалися наступні межі довіри (Trust Boundaries) і безпекові механізми [62, 196]:

1. **Межа Браузер → API:**
   - **Дані:** URL-параметри (`id`, `status`), заголовки запиту [62].
   - **Ризик:** Недовіра до клієнтського вводу (передача невалідних GUID або довільних рядків) [62, 118].
   - **Контроль:** Маршрутне обмеження `/{id:guid}` у Minimal API захищає роутинг від неформатних UUID [50, 53], а `Enum.TryParse` повертає статус `400 Validation Problem Details` для некоректних значень [118].
2. **Межа API → PostgreSQL:**
   - **Дані:** LINQ-запити та параметризовані значення [62].
   - **Ризик:** Навантаження бази даних та ризик модифікації об'єктів [62].
   - **Контроль:** Параметризація запитів у EF Core запобігає SQLi, а використання `.AsNoTracking()` вимикає відстеження змін для read-only операцій [12, 62].
3. **Межа API → Браузер:**
   - **Дані:** HTTP-відповідь (JSON DTO), заголовки [62].
   - **Ризик:** Витік чутливих даних (PII) та MIME-sniffing атаки [62, 120].
   - **Контроль:** Застосування DTO (`IncidentSeveritySummaryResponse`, `IncidentDetailsResponse`) приховує `ownerUserId`, `email` та внутрішні коментарі [10, 60, 62]. У `Program.cs` додано захисні заголовки `X-Content-Type-Options: nosniff` та `Referrer-Policy: no-referrer` [21, 22].
4. **Межа Дані response → DOM:**
   - **Дані:** Текстові поля JSON (`description`, `title` тощо) [62].
   - **Ризик:** Stored XSS-атаки при виводі даних, збережених у базі (наприклад, вміст тегів `<script>` у початковому інциденті «Перевірка журналу комп'ютерного класу») [57, 62, 121].
   - **Контроль:** Рендеринг у `app.js` виконується виключно через безпечні DOM API — `textContent` та `document.createTextNode()` [4, 61, 62]. Використання небезпечного sink `innerHTML` суворо заборонено [24, 77, 169].

---

## 6. Висновок

У ході виконання лабораторної роботи № 1 було успішно розгорнуто та досліджено локальний відтворювальний стенд вебсистеми SecureLab у складі Docker (PostgreSQL), ASP.NET Core Minimal API та Vanilla JS браузерного клієнта [24, 34]. Простежено наскрізний шлях виконання HTTP-запиту від дій користувача у браузері до виконання SQL-запитів СУБД PostgreSQL та зворотної побудови DOM-вузлів [46, 61].

У рамках Етапу 3 реалізовано наскрізне розширення системи — агрегований ендпоінт `GET /api/incidents/severity-summary` [69, 74]. Забезпечено дотримання принципу Data Minimization за допомогою окремого response DTO [10, 72], реалізовано обробку нульових груп через `Enum.GetValues` та усунуто лексикографічне SQL-сортирування за допомогою доменного ранжування у C# [12, 15, 73]. На клієнті забезпечено безпечне відображення даних через `textContent` для захисту від XSS-атак, а також додано підтримку інтерактивних станів UI та структуроване журналювання [6, 7, 76, 77]. Усі 4/4 автоматизовані інтеграційні тести проєкту успішно пройдено [170].
