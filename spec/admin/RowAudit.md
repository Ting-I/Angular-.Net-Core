# Build Spec for RowAudit (異動紀錄 history)

- database schema: `.\database\admin.sql`
- derived from the implementation on `feature/row-audit-writer`; the write side is specified in the
  異動紀錄 section of `spec/conventions/backend.md`, and this file covers **reading it back**.

---

## Summary

| Item | Detail |
|------|--------|
| Primary Key | `pkid` int IDENTITY — never sent to the client, and never filtered on |
| Foreign Keys | **N/A** — `TableName` + `PrimaryKeyValues` is a soft reference to any business row, with no constraint behind it |
| Required Fields | `TableName`, `UserName`, `PrimaryKeyValues`, `ActionType`, `[DateTime]` |
| N-N Relationships | **N/A** |
| Primary-Foreign Links | **N/A** — nothing references RowAudit |
| Query Filters | `tableName` + `pkid`, both required; there is no other way to ask |
| Default Sort | `ORDER BY a.[DateTime] DESC, a.pkid DESC` |

**What this feature is.** The trail had a writer and no reader. Every repository wrote a `RowAudit`
row on every insert, update and delete, and then nothing in the API selected from the table and
nothing in the client asked — a change was recorded and immediately invisible. This adds the one
read endpoint and the one component that renders it, on every page where a record is looked at.

**What it is not.** Not a CRUD entity. There is no list page, no form, no route, no sidebar entry,
and no create / update / delete of any kind. The standard six endpoints are deliberately one.

---

## Localization

### Chinese Table Name

- RowAudit: 異動紀錄
- Description: the cross-cutting change log — one row per insert, update or delete against any
  business table, written by the repository that made the change.

### Chinese Column Names

- pkid: 主代碼
- TableName: 資料表名稱
- UserName: 使用者名稱
- PrimaryKeyValues: 主鍵值
- ActionType: 異動類型
- ActionDesc: 異動說明
- DateTime: 異動時間

The badge and its dialog use 異動時間 / 使用者 / 異動類型 / 異動說明 as column headings, and
`異動紀錄 History` as the button label.

---

## Required Fields

Required (NOT NULL): `TableName`, `UserName`, `PrimaryKeyValues`, `ActionType`, `[DateTime]`.

Optional (nullable): `ActionDesc` — the one nullable column, so it is `string?` in both models and
renders as `—` in the dialog.

Nothing here is written by this feature. The columns are listed because the read model mirrors
them, and because `ActionDesc` being nullable is the reason the dialog has a placeholder at all.

---

## Foreign Keys / Foreign-Primary Links / Primary-Foreign Links / N-N

**N/A**, and the absence is the design:

- `TableName` + `PrimaryKeyValues` identify a business row without a constraint, on purpose. An FK
  would make the trail undeletable alongside the row it describes — and a delete's audit row exists
  precisely because the row no longer does.
- `PrimaryKeyValues` is `nvarchar(100)`, holding the pkid **rendered as text**. Matching it means
  rendering the caller's key the same way rather than leaving a widening conversion to the server.
- Nothing references RowAudit, so there are no child counts and no guarded delete.

---

## Query Filters

There is one query and it takes both halves of the key. There is no keyword search, no date range
and no "everything for this table" listing.

| Parameter | Type | Required | Notes |
|-----------|------|----------|-------|
| `tableName` | `string?` | yes | The **database** table name (`Course`, `FeaturedPromoItem`). Trimmed before use; blank is `400` |
| `pkid` | `int?` | yes | The record's pkid. Nullable on purpose — see below |

- **Both halves, always.** `PrimaryKeyValues` is only unique within a `TableName`, so pkid 7 alone
  mixes 原廠 7 with 課程 7.
- **`pkid` is `int?`, not `int`.** A plain `int` binds a missing parameter to 0 and answers the
  malformed request with a straight face — and 0 is a real key for a table whose pkid is not
  IDENTITY. A missing key is a `400`.
- **No unfiltered listing.** An endpoint that returns the whole table is an endpoint that grows
  without bound and shows every operator every other operator's activity. If a 稽核報表 is wanted
  later it is a different feature with its own filters and paging.

---

## Lookup Endpoints Required

**N/A.** The badge is handed its `tableName` and `pkid` by the page rendering it; it resolves
nothing.

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/rowaudit?tableName={table}&pkid={pkid}` | One record's trail, newest first |

- **`200`** with `[]` for an unknown table or key. "This record has no history yet" is what the
  badge renders and it is not an error — there is no `404` arm.
- **`400`** with `ProblemDetails` for a missing/blank `tableName` (title 缺少資料表名稱) or a
  missing `pkid` (title 缺少主代碼).
- Auth: nothing special. The global `FallbackPolicy` covers it like every other controller, and
  `AuthorizationTests.ProtectedEndpoints` carries `/api/rowaudit?tableName=Course&pkid=1` so the
  rule is proved for this shape too.
- **No write endpoint of any kind.** An audit row that can be edited is not an audit row; the trail
  is written only by the repository that made the change, through `IRowAuditWriter`.

---

## Backend Notes

### Models

`Models/RowAuditHistoryEntry.cs` — a read model, and deliberately not `RowAuditEntry` reversed. The
write model carries the column widths and the key columns the writer fills in; a reader has already
said which table and which key it is asking about, so repeating them per row would be noise.

```csharp
public sealed class RowAuditHistoryEntry
{
    [JsonPropertyName("dateTime")]
    public DateTime LoggedAt { get; init; }   // the [DateTime] column
    public string UserName { get; init; } = string.Empty;
    public string ActionType { get; init; } = string.Empty;
    public string? ActionDesc { get; init; }
}
```

The property is `LoggedAt` for the same reason `RowAuditEntry.LoggedAt` is: a property called
`DateTime` shadows the type of its own value. The **wire** name stays `dateTime`, via
`[JsonPropertyName]` — the API is the one place the two names have to meet, and the client should
not have to know about a C# keyword problem.

There is no `RowAuditRequest` and no `RowAuditQuery`. Two query-string parameters do not earn a
DTO, and a request model would imply a write.

### Repository

`IRowAuditRepository` / `RowAuditRepository`, registered `AddScoped` in `Program.cs` beside the
writer.

**It is a different object from `IRowAuditWriter`, on purpose.** The writer is called by
repositories in the middle of somebody else's transaction and is write-only by design; this is an
ordinary read-only repository a controller injects. Keeping them apart means nothing that writes
can acquire a way to read, and the reader can never be handed a transaction.

```csharp
Task<IEnumerable<RowAuditHistoryEntry>> GetForRecordAsync(
    string tableName, int pkid, CancellationToken cancellationToken = default);
```

One connection, no transaction: nothing here writes, and the trail is read outside whatever change
produced it. The key is bound as `pkid.ToString(CultureInfo.InvariantCulture)` — what went into
`PrimaryKeyValues` was digits, so what matches it has to be too.

### SQL — SELECT

`RowAuditSql.SelectForRecord`:

```sql
SELECT a.[DateTime] AS LoggedAt,
       a.UserName,
       a.ActionType,
       a.ActionDesc
FROM RowAudit a
WHERE a.TableName = @TableName
  AND a.PrimaryKeyValues = @PrimaryKeyValues
ORDER BY a.[DateTime] DESC, a.pkid DESC
```

- `[DateTime]` is bracketed — it is a type name as well as a column name — and aliased to
  `LoggedAt` to land on the model property.
- `pkid` and `TableName` are not projected: the caller supplied one and does not need the other.
- **The tie-break is load-bearing.** A change that writes more than one audit row —
  `FeaturedPromoItemRepository.MoveToSlotAsync` writes one per row the swap moved — stamps them
  from the same clock reading, so ordering on `[DateTime]` alone leaves their order to the server.
  IDENTITY is the only thing here that still increases inside one transaction.

### SQL — INSERT / UPDATE / DELETE

**N/A for this feature.** `RowAuditSql.Insert` exists and belongs to the writer; there is no
`Update` and no `Delete` statement in the file, and adding one would defeat the table.

### Special Column Notes

- **`[DateTime]` is local, not UTC.** `RowAuditWriter` stamps it from
  `TimeProvider.GetLocalNow().DateTime`. This is the opposite of `AppUser.PasswordUpdatedTime`,
  which is written with `GETUTCDATE()` — and it is why the client must **not** append `'Z'` here.
  See **Date Handling** below.
- **`PrimaryKeyValues` is the pkid even where the key is not.** `AuditHelper.PrimaryKeyValue`
  records the surrogate key every table carries, including 角色 AppRole (keyed on `RoleId`) and
  使用者 AppUser (keyed on `UserId`). The client has to pass the pkid to find anything.
- Every column is cut to its width by the writer, so a value read back is never truncated further.

---

## Frontend Notes

### Route

**N/A.** This is a component, not a page: no route, no lazy `loadComponent`, no sidebar entry.

### Angular Model — `core/models/row-audit.model.ts`

```ts
export interface RowAuditEntry {
  dateTime: string;      // yyyy-MM-ddTHH:mm:ss, local, no offset
  userName: string;
  actionType: string;    // Insert | Update | Delete
  actionDesc: string | null;
}
```

Read-only in every sense — there is no request model to pair with it, because the API exposes no
way to write one.

### Service — `core/services/row-audit.service.ts`

`RowAuditService.getForRecord(tableName: string, pkid: number)`, `GET` to
`${environment.apiUrl}/rowaudit` with `HttpParams`. No `create` / `update` / `delete` to match the
other services, and their absence is the point.

### Component — `core/components/row-audit-badge/`

`RowAuditBadge`, selector `app-row-audit-badge`, standalone, importing `ButtonModule` and
`DialogModule`.

| Input | Type | Notes |
|-------|------|-------|
| `tableName` | `input.required<string>()` | The **database** table name |
| `pkid` | `input<number \| null>(null)` | The record's pkid; null while unknown |

- **It is the only component outside `features/`.** Every detail and form page renders it and it
  belongs to no entity, so it lives in `core/` beside the other cross-cutting pieces rather than
  under whichever feature happened to need it first. There is no `shared/` directory and this did
  not earn one.
- **The fetch is an `effect` over the inputs, not `ngOnInit`.** A detail page knows its pkid only
  once the record has loaded, and a form in 新增 mode never has one — so a key that arrives late
  still fetches, and a key that never arrives never does. A null pkid makes **no request at all**.
- A `sequence` counter drops an out-of-order answer: an input that changes twice leaves two
  requests on the wire, and the last to arrive is not necessarily the last asked for.
- The API answers newest first, so `latest = entries()[0]`. Nothing re-sorts on the client, and
  opening the dialog does not re-fetch — the badge already holds the trail.

**Three states, and they are not the same state:**

| State | Rendered |
|-------|----------|
| Loading | `載入中…` |
| Has history | `{ActionType} by {UserName} · {yyyy-MM-dd HH:mm}` — e.g. `Update by alice · 2026-06-04 14:30` |
| No history | `尚無異動紀錄 No history` (`NO_HISTORY_TEXT`) |
| Fetch failed | `異動紀錄無法載入 Unavailable` (`HISTORY_UNAVAILABLE_TEXT`) |

The last two must not read alike: "no history" is a claim about the record, "unavailable" is a
claim about the request, and showing the first when the second is true says the record was never
touched.

**Dialog.** `p-dialog`, header `異動紀錄 History`, a plain table of 異動時間 / 使用者 / 異動類型 /
異動說明, newest first, `—` for a null `ActionDesc`, and the same empty / failed states as the
badge. Not a `p-table`: there is nothing to sort, page or filter, and the trail is already ordered.

**ActionType is rendered raw** (`Insert` / `Update` / `Delete`) rather than translated. It is data
the writer stored, not a UI label, and inventing a 新增/修改/刪除 mapping in the client would put a
second vocabulary between the operator and the row.

### Placement — the `#start` slot that does not exist

The `/crud` skill asks for the badge in a `p-toolbar` `#start` slot. **There is no `p-toolbar` in
this codebase** (CLAUDE.md is explicit that this file wins on that point). The house equivalent is
`.page-header`, so the badge goes immediately after the `<h1>`, ahead of `.page-actions`.

`.page-header` is `display: flex; justify-content: space-between`, so a third child would park
itself in the middle of the bar. The badge's own stylesheet claims the free space instead:

```scss
:host { display: inline-flex; margin-right: auto; }
```

That rule lives in the component, not in each page's stylesheet — which is what makes it drop-in,
and what keeps thirteen pages from each needing a layout fix.

### Where it is rendered

Every detail page and every form page. `featured-promo-item-form` is the one exception to the
placement rule: it is an inline editor in a week-grid cell with no page header, so its badge sits
in `.form-actions`.

| Page | `tableName` | `pkid` binding |
|------|-------------|----------------|
| `app-role-detail` | `AppRole` | `role()?.pkid ?? null` |
| `app-role-form` | `AppRole` | `auditPkid()` |
| `app-user-detail` | `AppUser` | `user()?.pkid ?? null` |
| `app-user-form` | `AppUser` | `auditPkid()` |
| `course-group-detail` | `CourseGroup` | `courseGroup()?.pkid ?? null` |
| `course-group-form` | `CourseGroup` | `pkid()` |
| `course-detail` | `Course` | `course()?.pkid ?? null` |
| `course-form` | `Course` | `pkid()` |
| `partner-detail` | `Partner` | `partner()?.pkid ?? null` |
| `partner-form` | `Partner` | `pkid()` |
| `publish-status-detail` | `PublishStatus` | `status()?.pkid ?? null` |
| `publish-status-form` | `PublishStatus` | `auditPkid()` |
| `featured-promo-item-form` | `FeaturedPromoItem` | `item()?.pkid ?? null` |

**`auditPkid` is a new signal on three forms.** `app-role-form` and `app-user-form` are keyed on a
string and never held a pkid, so it is set from the loaded record in `patchFromRole` /
`patchFromUser`. `publish-status-form` had the key in a plain `private pkid` field, which a
template cannot read. The other three forms already exposed `pkid()`.

**個人資料 My Profile has no badge.** It edits the operator's own AppUser row, but the session holds
`userId` / `userName` and no pkid, so the badge would need an extra `GET /api/app-users/{id}` for
the sole purpose of finding one. Left out deliberately; revisit if the profile page ever loads the
full record for another reason.

### Date Handling — the one place `+ 'Z'` is wrong

`core/utils/date.util.ts` gained `formatDateTime(value)` → `yyyy-MM-dd HH:mm` from **local**
components, null for anything unparseable so the caller renders its own placeholder instead of
`Invalid Date`.

`spec/feature-spec.template.md` says to append `'Z'` before parsing an API datetime, because Dapper
returns `datetime` with `Kind = Unspecified`. **That rule does not apply to `RowAudit.[DateTime]`,
and following it here would be a bug.** The rule exists for columns written in UTC —
`AppUser.PasswordUpdatedTime` is `GETUTCDATE()`, and `app-user-detail` correctly renders
`passwordUpdatedTime + 'Z'`. `RowAudit.[DateTime]` is written from `GetLocalNow().DateTime`, so it
is already local: appending `'Z'` would shift every entry by the operator's offset — eight hours in
UTC+8, which is enough to move a change to the previous day.

The wire form has no offset, `new Date()` parses that as local, and the components come back out
exactly as the server wrote them. Nothing is stripped and nothing is added.

### Session Storage Keys

**N/A.** The badge holds no filter, sort or page state.

---

## Tests

### Backend (`src/CMS.API.Tests`) — 16 new, 629 total

`Controllers/RowAuditControllerTests.cs` (10 cases) against `Fakes/FakeRowAuditRepository`, which
mirrors what the real query gets from the database — the filter on both columns and the
newest-first ordering with IDENTITY breaking a tie — rather than returning whatever it was seeded
with. Seeding rows out of order across two tables is then a real test of the contract.

- filters to one table **and** one key (原廠 7 and 課程 8 both excluded when asking for 課程 7)
- newest first; a tie on `[DateTime]` broken by the audit row's own key
- carries all four columns the client renders
- no history → empty list, not a `404`
- missing / blank / whitespace `tableName` → `400`, and the repository is never called
- missing `pkid` → `400` rather than a query for pkid 0
- `tableName` is trimmed

`Repositories/RowAuditSqlTests.cs` (4 cases) pins the statement itself — both filter clauses, the
`ORDER BY [DateTime] DESC, pkid DESC`, the four projected columns with `[DateTime]` bracketed and
aliased, and that the statement contains no `UPDATE` or `DELETE`. No fake and no database, like
every other `{Table}Sql` test.

`Security/AuthorizationTests.ProtectedEndpoints` gains the route (2 more cases: without a token it
is `401`, with one it is not), and `TestApiFactory` gains
`Replace<IRowAuditRepository>(services, new FakeRowAuditRepository())` so an authenticated request
reaches the action instead of the throwing connection factory.

### Frontend (`src/CMS.NG`) — 16 new, 562 total

`core/components/row-audit-badge/row-audit-badge.spec.ts` (10):

- fetches by `tableName` + `pkid`, and shows the latest entry inline without being opened
- renders the bilingual label while the request is still on the wire
- opens the dialog with the full trail, newest first, all four columns
- opening it does not re-fetch
- empty trail → the neutral state on the badge **and** in the dialog
- no pkid → **no request at all**; a pkid that arrives later fetches then
- a failed fetch says unavailable, not empty

`core/services/row-audit.service.spec.ts` (2) and four `formatDateTime` cases in
`core/utils/date.util.spec.ts`, including the offsetless timestamp that must stay local.

**Every detail and form page spec now has a second request to answer.** The badge fetches on all
thirteen pages, so those specs flush it in `afterEach` before `httpMock.verify()`:

```ts
httpMock.match((req) => req.url.endsWith('/rowaudit')).forEach((req) => req.flush([]));
```

Without it, 72 existing tests fail on an open request. The badge's own behaviour is pinned in its
spec and nowhere else — the page specs answer the request, they do not assert on it.

---

## Files to Create / Modify

### Backend

- `Models/RowAuditHistoryEntry.cs` — new
- `Repositories/IRowAuditRepository.cs`, `Repositories/RowAuditRepository.cs` — new
- `Repositories/RowAuditSql.cs` — add `SelectForRecord`
- `Controllers/RowAuditController.cs` — new
- `Program.cs` — register `IRowAuditRepository`

### Frontend

- `core/models/row-audit.model.ts` — new
- `core/services/row-audit.service.ts` — new
- `core/components/row-audit-badge/` (`.ts`, `.html`, `.scss`) — new
- `core/utils/date.util.ts` — add `formatDateTime`
- 13 detail / form components + templates — import and render the badge; three forms gain
  `auditPkid`

### Tests

- `Controllers/RowAuditControllerTests.cs`, `Repositories/RowAuditSqlTests.cs`,
  `Fakes/FakeRowAuditRepository.cs` — new
- `Security/TestApiFactory.cs`, `Security/AuthorizationTests.cs` — extend
- `core/components/row-audit-badge/row-audit-badge.spec.ts`,
  `core/services/row-audit.service.spec.ts` — new
- `core/utils/date.util.spec.ts` + 14 page / list specs — extend

---

## Deviations from the /crud skill template

- **~~No `RowAuditBadgeComponent`~~ — closed.** It exists as `RowAuditBadge`. Recorded as missing
  in five feature specs while nothing read the trail back; those notes are now corrected.
- **No `p-toolbar` `#start` slot.** There is no `p-toolbar` in this codebase, so the badge goes at
  the start of `.page-header`. See **Placement** above.
- **No mocking library.** Hand-written fakes in `src/CMS.API.Tests/Fakes/`, per CLAUDE.md; the
  skill's suggestion of Moq is not followed.
- **One endpoint, not six.** No list, no query, no create, update or delete — see **API
  Endpoints**.
- **No list / detail / form triple, no route, no sidebar entry.** The trail is rendered inside the
  pages of the records it describes, which is where the question is asked.

## Deviations from `feature-spec.template.md`

- **The `+ 'Z'` rule under Date Handling does not apply.** `RowAudit.[DateTime]` is written local,
  not UTC. See **Date Handling** above — this is the one documented exception, and it is a bug if
  followed here.
- **The component lives in `core/`, not `features/`.** The template's layout assumes a component
  belongs to one entity; this one belongs to all of them.
