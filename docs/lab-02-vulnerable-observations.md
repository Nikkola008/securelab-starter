# ЛР 02 — vulnerable state і спостереження до виправлення пошуку

## Ідентифікація стану

- **Supplied release:** `lab-02-start-v1` (див. `.scaffolds/lab-02.json`).
- **База гілки:** тег `v0.1.0`.
- **Vulnerable commit:** _вписати hash після commit етапу 3_.
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
| Фактичний status | _вписати_ | _вписати_ | _вписати_ |
| Фактична кількість/ідентифікатори artificial records | _вписати_ | _вписати_ | _вписати_ |
| Пояснення | _вписати_ | Символ `'` завершує SQL-рядок; `OR TRUE` інтерпретується як SQL-структура, а `--` коментує залишок запиту. | _вписати_ |

Не виконувати інші payload або SQL statements. Якщо результат контрольного сценарію відрізняється від очікуваного, зупинитися та перевірити commit, reset, seed і код.
