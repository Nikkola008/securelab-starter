# ЛР 02 — vulnerable state і спостереження до виправлення пошуку

## Ідентифікація стану

- **Supplied release:** `lab-02-start-v1` (див. `.scaffolds/lab-02.json`).
- **База гілки:** тег `v0.1.0`.
- **Vulnerable commit:** `e4201b016067bb112ffd889ac82ee61e14af71fe`.
- **Security diff:** server-side validation `POST /api/incidents`, контракт `CreatedIncidentResponse`, сценарії створення та дослідження пошуку. Виправлення search у цей commit не входить.

## Root cause

| Файл | Метод | Точна точка |
|---|---|---|
| `src/SecureLab.Api/Scaffolding/Lab02Endpoints.cs` | `MapLab02Endpoints` | Рядок 20: значення query parameter `q` приєднується до рядка `sql` у виразі `var sql = ...`; рядок 22: сформований рядок викликається через `db.Incidents.FromSqlRaw(sql)`. |

Маршрут: query string `q` → binding параметра `string? q` у Minimal API → конкатенація в `sql` → `FromSqlRaw` → PostgreSQL.

## План fix перед редагуванням

«Замість передавати в PostgreSQL SQL-текст, до якого вже приєднано `q`, я використаю LINQ і `EF.Functions.ILike`, щоб EF Core передав значення параметром. Значення `sortBy` я обмежу `switch`/allowlist допустимих полів сортування».

## Таблиця спостережень

Заповнити лише фактичними результатами локального запуску на vulnerable commit після reset бази.

| Поле | Звичайний пошук | Контрольний read-only сценарій | Апостроф-регресія |
|---|---|---|---|
| Передумови | local Development, PostgreSQL container, reset, vulnerable commit | local Development, PostgreSQL container, reset, vulnerable commit | local Development, PostgreSQL container, reset, vulnerable commit |
| Дія | `GET /api/incidents/search?q=USB` | `GET /api/incidents/search?q=zz-no-match%27%20OR%20TRUE%20--%20` | `GET /api/incidents/search?q=комп%27ютерного` |
| Назва сценарію | `NormalSearch_Usb` | `ReadOnlyControl_ZzNoMatchOrTrue` | `ApostropheRegression_Kompiuternoho` |
| Очікування без defect | Повертаються тільки записи, які містять USB | Не повертаються записи, що не відповідають `zz-no-match` | Повертаються лише записи зі збігом для слова з U+0027 |
| Фактичний status | `200 OK` | `200 OK` | _ще не зафіксовано вручну на vulnerable commit_ |
| Фактична кількість/ідентифікатори artificial records | `1`: `20000000-0000-0000-0000-000000000005` — «Перевірка USB навчальної мережі» | `5`: `20000000-0000-0000-0000-000000000001` … `20000000-0000-0000-0000-000000000005` | _ще не зафіксовано вручну на vulnerable commit_ |
| Пояснення | Нормальний збіг повернув лише USB seed-запис. | Символ `'` завершує SQL-рядок; `OR TRUE` інтерпретується як SQL-структура, а `--` коментує залишок запиту. | Потрібно виконати один ручний запит до fix та записати фактичну відповідь. |

Не виконувати інші payload або SQL statements. Якщо результат контрольного сценарію відрізняється від очікуваного, зупинитися та перевірити commit, reset, seed і код.

## Етап 4 — реалізоване виправлення

- Пошук більше не формує SQL-текст через конкатенацію і не використовує `FromSqlRaw`.
- LINQ з `EF.Functions.ILike` передає search pattern як параметр EF Core.
- Метасимволи LIKE `%`, `_` та `\\` екрануються: `q=%25` шукає буквальний символ `%`, а не wildcard.
- `sortBy` має allowlist: `createdAtUtc`, `severity`, `status`; інше значення повертає `400 application/problem+json` з ключем `errors.sortBy`.
- Ручні сценарії та `SearchMechanicsTests` перевіряють normal search, контрольний input, легітимний U+0027 апостроф і невалідний `sortBy`.

Після запуску regression tests та `lab-02-search.http` вписати фактичні результати після fix окремо у звіт; не перезаписувати спостереження vulnerable commit.
