# ЛР 02 — зовнішній контракт `POST /api/incidents` (baseline 2-A)

## Таблиця контракту

| Частина запиту/відповіді | Правило |
|---|---|
| `title` | Обов’язкове поле; максимум 160 символів. Після базової перевірки сервер один раз застосовує `Trim()`. |
| `description` | Обов’язкове поле; максимум 4000 символів. Після базової перевірки сервер один раз застосовує `Trim()`. |
| `severity` | Обов’язкове одне зі значень `Low`, `Medium`, `High`, `Critical`. Рядок `"7"` не допускається. |
| `occurredAtUtc` | Обов’язкове. Не може бути пізніше за UTC-час сервера більш ніж на п’ять хвилин. |
| Cross-field правило | Для `High` або `Critical` нормалізований `description` містить щонайменше 40 символів. |
| Предметний конфлікт | `title.Trim()` не може повторювати наявний title з урахуванням регістру, якщо його статус `New`, `Triaged`, `InProgress` або `Resolved`. Збіг тільки з `Closed` не блокує створення. |
| Серверні поля | Клієнт не передає `id`, `ownerUserId`, `status`, `createdAtUtc` або `updatedAtUtc`. Їх установлює сервер. |
| Успіх | `201 Created`, `application/json`, тіло `CreatedIncidentResponse`: `id`, `title`, `severity`, `status`, `occurredAtUtc`, `createdAtUtc`. |
| Некоректний запит | `400 Bad Request`, `application/problem+json`, поле `errors` з ключами невалідних полів; без деталей БД або stack trace. |
| Предметний конфлікт | `409 Conflict`, `application/problem+json`, безпечне пояснення без внутрішніх деталей. |

## CP-01: маршрут і обґрунтування

1. Браузер або інший HTTP-клієнт надсилає JSON до `POST /api/incidents`.
2. ASP.NET Core зв’язує лише чотири дозволені поля JSON із `CreateIncidentRequest`.
3. Endpoint перевіряє required, довжини, UTC-дату й severity до створення entity та доступу до БД.
4. Якщо в `errors` є хоча б одна помилка, `Results.ValidationProblem` повертає `400` з `application/problem+json`.

`Enum.TryParse` перевіряє, чи можливо прочитати рядок як enum. Самого цього недостатньо: числовий рядок `"7"` може бути перетворений на enum-значення. `Enum.IsDefined` окремо підтверджує, що це одне з визначених значень домену, тому для `"7"` endpoint повертає 400.

Зміна `<select>` у браузері не є захистом: клієнтський код можна обійти через DevTools, curl або інший HTTP-клієнт. Тому перевірка виконується на сервері. `OwnerUserId` і `status` не є властивостями `CreateIncidentRequest`, тож binding не встановлює їх у модель створення; сервер задає owner і `New` самостійно.

Автоматизований доказ: `IncidentCreationContractTests` перевіряє числовий severity, 400 Problem Details, межі 39/40 символів, 409 для активного seed-дубліката, ігнорування надісланих `ownerUserId`/`status` і дозволене повторення title після `Closed`.
