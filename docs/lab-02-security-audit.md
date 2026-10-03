# ЛР 02 — A-01, A-02 і CP-03

## A-01. Огляд усіх runtime data-access points

| Місце | Спосіб доступу | Висновок |
|---|---|---|
| `Application/Incidents/IncidentQueries.cs` | EF Core LINQ (`Where`, `Select`, `GroupBy`) | Безпечно: значення параметризуються EF Core; конкатенації SQL немає. |
| `Scaffolding/Lab02Endpoints.cs`, `GET /api/incidents/search` | EF Core LINQ та `EF.Functions.ILike` | Виправлено: `q` входить лише до значення `pattern`, яке EF передає параметром. `%`, `_` і `\\` екрануються для буквального LIKE-пошуку. `sortBy` допускає тільки `createdAtUtc`, `severity`, `status`. |
| `Scaffolding/Lab02Endpoints.cs`, `POST /api/incidents` | `AnyAsync`, `Add`, `SaveChangesAsync` | Безпечно: EF Core LINQ; title застосовується як параметр порівняння, а не як SQL-текст. |
| `Data/DbSeeder.cs`, `Scaffolding/Lab02Seed.cs` | `AnyAsync`, `Add`, `SaveChangesAsync` | Статичні навчальні seed-значення, без введення користувача. |
| `Data/DatabaseBootstrap.cs` | `ExecuteSqlRawAsync` | Допустиме виняткове місце: константний `TRUNCATE` для явно дозволеного Development reset; текст не збирається з input. |
| EF migrations | згенеровані EF migration-команди | У runtime коді ЛР немає raw SQL, сформованого з HTTP-параметрів. |

Висновок: після виправлення немає runtime місця, де HTTP input поєднується з SQL-текстом.

### Фактичний code search fixed commit

Виконано пошук: `FromSqlRaw`, `ExecuteSqlRaw`, `$"SELECT`, `+ query` і `ORDER BY` у `src` та `tests`.

- `FromSqlRaw`: збігів у fixed runtime-коді немає.
- `ExecuteSqlRawAsync`: один збіг у `Data/DatabaseBootstrap.cs`; це константний Development-only `TRUNCATE`, не сформований з HTTP input.
- `$"SELECT`, `+ query`, `ORDER BY`: у runtime-коді ЛР 02 немає збігів, де user-controlled value стає SQL-структурою.

## A-02. Mass assignment

Ризик: клієнт міг би спробувати надіслати `ownerUserId` або `status` та призначити собі власника/стан інциденту.

Усунення: `CreateIncidentRequest` містить лише `Title`, `Description`, `Severity`, `OccurredAtUtc`. Після валідації сервер сам встановлює `Id`, `OwnerUserId = DbSeeder.AliceId`, `Status = New`, `CreatedAtUtc` і `UpdatedAtUtc`. `CreatedIncidentResponse` не повертає owner, description або audit-поле `UpdatedAtUtc`.

Доказ: `Create_HighWith40CharactersAfterTrim_Returns201AndUsesServerFields` надсилає зайві `ownerUserId` і `status = Closed`, але перевіряє у відповіді `status = New` та відсутність `ownerUserId`.

## CP-03. Пояснення security diff

До fix пошук склеював `q` у текст SQL та передавав результат у `FromSqlRaw`. Тепер пошук виконується як LINQ-запит із `EF.Functions.ILike`; EF Core формує SQL та передає пошуковий pattern окремим параметром. Тому символи U+0027, `OR` і `--` є тільки даними pattern, а не частиною SQL-синтаксису.

`sortBy` не передається у SQL. До нього застосовано allowlist/switch: `createdAtUtc`, `severity`, `status`; будь-яке інше значення повертає `400 application/problem+json` з `errors.sortBy`.

Метасимволи LIKE `%`, `_` і `\\` екрануються до формування `pattern`, тому `q=%` є буквальним пошуком символу `%`, а не клієнтським wildcard.

Докази: `Search_WithControlledReadOnlyInput_ReturnsNoMatches` перевіряє точну порожню множину IDs; `Search_WithNormalUsbQuery_ReturnsOnlyUsbSeedIncident` і `Search_WithLegitimateApostrophe_ReturnsMatchingSeedIncident` перевіряють точні одноелементні множини; також є `Search_WithPercentSign_SearchesForLiteralPercentAndReturnsNoMatches`, `Search_WithUnsupportedSortBy_Returns400ProblemDetails` та `Search_WithAllowedSortBy_ReturnsJson`.
