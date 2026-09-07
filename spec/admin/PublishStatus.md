# Build Spec for PublishStatus
- database schema: `.\database\admin.sql`

`PublishStatus` is a small, hand-curated lookup table describing the publication lifecycle of
content (草稿 / 已發布 / 已停用). It carries no foreign keys of its own and is referenced by
`Course` and `Promotion2`. Its `pkid` is a **`tinyint` that is NOT an IDENTITY column** — the
value is supplied by the operator when the row is created, which drives most of the non-standard
handling in this spec.

---

## Summary

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **tinyint NOT NULL, not IDENTITY** — client-supplied, immutable after create |
| Foreign Keys | None |
| Required Fields | `pkid`, `Description`, `IsDraft`, `IsPublished`, `IsDiscontinued` |
| N-N Relationships | N/A |
| Primary-Foreign Links | `Course.PublishStatus_pkid`, `Promotion2.PublishStatus_pkid` |
| Query Filters | keyword (`Description`); tri-state bool `IsDraft` / `IsPublished` / `IsDiscontinued` |
| Default Sort | `ORDER BY p.pkid ASC` |

---

## Localization

### Chinese Table Name

- PublishStatus: 發布狀態
- Description: 內容發布狀態代碼表 — 供課程與活動引用的發布生命週期

### Chinese Column Names

- pkid: 主代碼
- Description: 狀態說明
- IsDraft: 草稿
- IsPublished: 已發布
- IsDiscontinued: 已停用

Derived (not columns):

- CourseCount: 課程數
- PromotionCount: 活動數

---

## Required Fields

Required (NOT NULL):

- `pkid` — tinyint (0–255). **Not IDENTITY**, so it is required on create and is part of the write
  DTO, unlike every IDENTITY-keyed entity.
- `Description` — nvarchar(50)
- `IsDraft` — bit
- `IsPublished` — bit
- `IsDiscontinued` — bit

Optional (nullable): none. Every column is NOT NULL.

The three bit columns are independent in the schema — there is no CHECK constraint forcing exactly
one to be set — so the API does **not** enforce mutual exclusivity. The form leaves all three as
free toggles.

---

## Foreign Keys

`PublishStatus` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

`PublishStatus` has no foreign key columns.

**N/A**

---

## Primary-Foreign Links

Two tables reference `PublishStatus.pkid`:

| Child table | FK column | Constraint |
|-------------|-----------|------------|
| `Course` | `PublishStatus_pkid` tinyint NOT NULL | `FK_Course_PublishStatus` (`database/course.sql`) |
| `Promotion2` | `PublishStatus_pkid` tinyint NOT NULL | `FK_Promotion2_PublishStatus` (`database/promotion.sql`) |

Neither the Course nor the Promotion feature has been built yet, so there are **no routes to link
to**. Instead of dead buttons, this feature surfaces the relationship as **counts**:

- `CourseCount` and `PromotionCount` are correlated subqueries in the base projection.
- List page shows both as right-aligned numeric columns.
- Detail page shows both under a 使用狀況 section.
- Delete confirmation warns when either count is greater than zero, because the FK constraints will
  make the DELETE fail.

When `/courses` and `/promotions` are built later, turn these counts into `pi pi-list` link buttons
targeting `/courses?publishStatusPkid={pkid}` and `/promotions?publishStatusPkid={pkid}`.

---

## N-N Relationships

No junction table references `PublishStatus`.

**N/A**

---

## Query Filters

`POST /api/publish-statuses/query` accepts:

- **keyword** — `string?`
  - `LIKE` on `Description` only. It is the single string column; `pkid` is numeric and is covered
    by sorting rather than keyword search.
  - LIKE wildcards (`%`, `_`, `[`) are escaped so a literal keyword matches literally.

- **isDraft** — `bool?` (tri-state: `null` = no filter, `true` = only 草稿, `false` = only non-草稿)
  - Exact match on `p.IsDraft`.

- **isPublished** — `bool?` (tri-state)
  - Exact match on `p.IsPublished`.

- **isDiscontinued** — `bool?` (tri-state)
  - Exact match on `p.IsDiscontinued`.

No FK filters (no FK columns) and no date-range filters (no date columns).

---

## Lookup Endpoints Required

`PublishStatus` needs no lookups of its own, but it **is** an FK target for `Course` and
`Promotion2`, so it must publish one:

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/publish-statuses` | **New** | `PublishStatusLookup[]` — `pkid`, `description`, ordered by `pkid ASC` |

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/publish-statuses` | List all, `ORDER BY pkid ASC` |
| `POST` | `/api/publish-statuses/query` | Filtered query (body: `PublishStatusQuery`; null body = unfiltered) |
| `GET` | `/api/publish-statuses/{id:int}` | Get by pkid; `404` when missing |
| `POST` | `/api/publish-statuses` | Create; `409` when `pkid` already exists |
| `PUT` | `/api/publish-statuses` | Update — **pkid comes from the body**, not the route; `404` when missing |
| `DELETE` | `/api/publish-statuses/{id:int}` | `204` on success, `404` when missing, `409` when Course/Promotion2 still reference it |

Route constraint is `{id:int}`, not `{id}` — the key is numeric, so unlike `AppRole` the Angular
service does **not** need `encodeURIComponent`. The action parameter is typed `byte`; an
out-of-range value (e.g. `9999`) fails model binding and returns `400`.

`POST` returns `201 CreatedAtAction(nameof(GetById), new { id = pkid }, created)`.

No auth attributes — the API has no authentication wired yet, matching `AppRolesController`.

---

## Backend Notes

### Models

`Models/PublishStatus.cs` — response model:

```csharp
public class PublishStatus
{
    public byte Pkid { get; set; }                           // 主代碼
    public string Description { get; set; } = string.Empty;  // 狀態說明
    public bool IsDraft { get; set; }                        // 草稿
    public bool IsPublished { get; set; }                    // 已發布
    public bool IsDiscontinued { get; set; }                 // 已停用
    public int CourseCount { get; set; }                     // 課程數 — subquery
    public int PromotionCount { get; set; }                  // 活動數 — subquery
}
```

`Models/PublishStatusRequest.cs` — write DTO. Unlike IDENTITY-keyed entities, `Pkid` **is** part of
the request because the column is not IDENTITY:

```csharp
public class PublishStatusRequest
{
    [Range(0, 255)]
    public byte Pkid { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string Description { get; set; } = string.Empty;

    public bool IsDraft { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDiscontinued { get; set; }
}
```

`Models/PublishStatusQuery.cs` — search DTO:

```csharp
public class PublishStatusQuery
{
    public string? Keyword { get; set; }
    public bool? IsDraft { get; set; }
    public bool? IsPublished { get; set; }
    public bool? IsDiscontinued { get; set; }
}
```

`Models/PublishStatusLookup.cs` — slim option row (mirrors `AppUserLookup`):

```csharp
public class PublishStatusLookup
{
    public byte Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}
```

### SQL — SELECT

`Repositories/PublishStatusSql.cs` holds the projection plus `BuildWhere`, following `AppRoleSql`
so the filter logic is unit-testable without a database.

```sql
SELECT p.pkid AS Pkid,
       p.Description,
       p.IsDraft,
       p.IsPublished,
       p.IsDiscontinued,
       (SELECT COUNT(*) FROM Course c WHERE c.PublishStatus_pkid = p.pkid) AS CourseCount,
       (SELECT COUNT(*) FROM Promotion2 pr WHERE pr.PublishStatus_pkid = p.pkid) AS PromotionCount
FROM PublishStatus p
```

`DefaultOrderBy` = `ORDER BY p.pkid ASC`.

No JOINs and no multi-map: there are no FK nav objects. No `nchar` columns, so no `RTRIM()`. No
`date` / `time(7)` columns, so no type handlers beyond the ones already registered in `Program.cs`.

### SQL — INSERT

All five columns are writable, `pkid` included, because it is not IDENTITY. There is no
`SCOPE_IDENTITY()` call — the repository returns `request.Pkid`.

```sql
INSERT INTO PublishStatus (pkid, Description, IsDraft, IsPublished, IsDiscontinued)
VALUES (@Pkid, @Description, @IsDraft, @IsPublished, @IsDiscontinued);
```

### SQL — UPDATE

`pkid` is the key and is immutable — it appears only in the WHERE clause.

```sql
UPDATE PublishStatus
SET Description = @Description,
    IsDraft = @IsDraft,
    IsPublished = @IsPublished,
    IsDiscontinued = @IsDiscontinued
WHERE pkid = @Pkid;
```

`UpdateAsync` returns `false` when zero rows are affected, which the controller turns into `404`.

### SQL — DELETE

```sql
DELETE FROM PublishStatus WHERE pkid = @Pkid;
```

No junction rows to clean up first. `Course` and `Promotion2` hold enforced FKs, so deleting a
referenced row raises SQL error 547. The controller guards against that by reading the record first
— `GetByIdAsync` already projects `CourseCount` and `PromotionCount`, so no extra repository method
is needed — and returns `409` with a Chinese `ProblemDetails` title rather than letting the
exception surface as a 500. The same call supplies the `404` for a missing row.

### N-N Sync Pattern

**N/A** — no junction tables.

### Special Column Notes

- **`pkid` is `tinyint` and NOT IDENTITY.** This is the one thing to get right. It maps to C# `byte`,
  is required in the write DTO, is checked for duplicates before INSERT (`409`), and is disabled in
  the edit form.
- `Description` is `nvarchar(50)` — a short label, not prose. `StringLength(50)` and `maxlength="50"`.
- The PK constraint is named `PK_PublishingStatus` (note the "ing") while the table is
  `PublishStatus`. Cosmetic only; no code depends on the constraint name.
- The identical `CREATE TABLE [dbo].[PublishStatus]` block appears in `admin.sql`, `course.sql` and
  `promotion.sql`. All three scripts are `USE [CMS]` and the definitions match exactly — it is one
  table repeated for readability, not three tables.

---

## Frontend Notes

### Routes

| Path | Component |
|------|-----------|
| `publish-statuses` | `PublishStatusList` |
| `publish-statuses/new` | `PublishStatusForm` |
| `publish-statuses/:id` | `PublishStatusDetail` |
| `publish-statuses/:id/edit` | `PublishStatusForm` |

Lazy `loadComponent` in `app.routes.ts`, `/new` registered **before** `/:id`, inserted ahead of the
`**` wildcard.

### Angular Model — `core/models/publish-status.model.ts`

```ts
export interface PublishStatus {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
  courseCount: number;
  promotionCount: number;
}

export interface PublishStatusRequest {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
}

export interface PublishStatusQuery {
  keyword?: string | null;
  isDraft?: boolean | null;
  isPublished?: boolean | null;
  isDiscontinued?: boolean | null;
}
```

`core/models/publish-status-lookup.model.ts` holds `{ pkid: number; description: string }`.

### Service — `core/services/publish-status.service.ts`

Standard six methods against `${environment.apiUrl}/publish-statuses`. The key is numeric, so
`getById(pkid: number)` and `delete(pkid: number)` interpolate directly — **no
`encodeURIComponent`**, which is the `AppRole` string-PK special case and does not apply here.
`update()` PUTs to the collection route with the key in the body.

`LookupService` gains `getPublishStatuses()` hitting `/lookups/publish-statuses`.

### List component

Columns: 主代碼 / 狀態說明 / 草稿 / 已發布 / 已停用 / 課程數 / 活動數 / 操作.
The three bit columns render as `pi pi-check` / `pi pi-minus` icons rather than raw `true` / `false`.

Sortable, paginated `p-table`, `dataKey="pkid"`, default sort `{ field: 'pkid', order: 1 }`,
default page `{ first: 0, rows: 20 }`, paginator on top, `rowsPerPageOptions [10, 20, 50, 100]`.

Filter drawer (`p-drawer`, `position="right"`): a keyword `input pInputText` plus three `p-select`
tri-state dropdowns (`全部` / `是` / `否`, values `null` / `true` / `false`), each with
`appendTo="body"`. Under 10 options each, so no `[filter]` and no virtual scroll.

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `publish-status-list-filters` | Applied `PublishStatusQuery` |
| `publish-status-list-sort` | `{ field, order }` |
| `publish-status-list-page` | `{ first, rows }` |

No incoming cross-entity query params — nothing navigates into this list yet.

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.description}」？
```

When `courseCount + promotionCount > 0`, append:

```
此狀態仍被 ${courseCount} 筆課程與 ${promotionCount} 筆活動使用，將無法刪除。
```

A `409` response surfaces the toast 「此發布狀態仍被課程或活動使用，無法刪除。」

PrimeNG 20's `p-confirmDialog` has **no `escape` input** — it always renders `message` through
`[innerHTML]`, so the `<b>` markup works as written and the record's own text must be HTML-escaped
before interpolation (the list component has a small `escapeHtml` helper for this).

### Date Handling

**N/A** — no `date`, `time` or `datetime` columns, so neither the `toIso()` local-components rule nor
the `+ 'Z'` UTC fix applies.

### Form layout

| Field | Widget | Notes |
|-------|--------|-------|
| 主代碼 pkid | `p-inputNumber` `[min]="0" [max]="255" [useGrouping]="false"` | Required. **Disabled in edit mode** — read with `getRawValue()` |
| 狀態說明 Description | `input pInputText` `maxlength="50"` | Required |
| 草稿 IsDraft | `p-toggleswitch` | Defaults `false` |
| 已發布 IsPublished | `p-toggleswitch` | Defaults `false` |
| 已停用 IsDiscontinued | `p-toggleswitch` | Defaults `false` |

Reactive Forms. There is no lookup to load, so **no `forkJoin` is needed in add mode**; edit mode
loads the single record directly. Validation messages are Traditional Chinese.

### Special Form Behaviors

- `pkid` is editable only in add mode; `this.form.controls.pkid.disable()` when `isEdit()`, and
  `save()` uses `getRawValue()` so the disabled key still reaches the request.
- A `409` on create keeps the user on the form and toasts 「主代碼已存在。」
- The three toggles are independent — no cross-field validation.

### Sub-panels (edit mode only)

**N/A**

### Sidebar placement

Existing group `系統管理 Admin` in `app.ts` `navGroups`, appended after `角色 AppRole`:

```ts
{ label: '發布狀態 PublishStatus', icon: 'pi pi-flag', route: '/publish-statuses' }
```

`app.spec.ts` is extended to assert the new item renders under that group.

---

## Tests

### Backend — `src/CMS.API.Tests/`

Fakes, not a mocking library (house rule).

- `Fakes/FakePublishStatusRepository.cs` — in-memory `IPublishStatusRepository` mirroring the real
  contract: keyword + tri-state bool filtering, `pkid ASC` ordering, duplicate-key detection,
  settable reference counts so the delete-conflict path is reachable.
- `Fakes/FakeLookupRepository.cs` — extended with `GetPublishStatusesAsync`.
- `Controllers/PublishStatusesControllerTests.cs` — list, query (keyword, each bool, combined, no
  match, null body), get-by-id found/not-found, create 201 + route value, create duplicate 409,
  update 200 with key-from-body, update missing 404, delete 204, delete missing 404, delete
  referenced 409.
- `Repositories/PublishStatusSqlTests.cs` — `BuildWhere` against every filter combination, keyword
  trimming and LIKE escaping, `false` filters producing a clause (not skipped as falsy), and the
  projection containing both count subqueries.
- `Controllers/LookupsControllerTests.cs` — a case for the new lookup route.

### Frontend — `src/CMS.NG/`

- `core/services/publish-status.service.spec.ts` — each method's URL and verb; the key interpolates
  as a plain number; PUT carries the key in the body.
- `core/services/lookup.service.spec.ts` — a case for `getPublishStatuses()`.
- `features/publish-statuses/publish-status-list/publish-status-list.spec.ts` — loads on init,
  renders rows, unfiltered body by default, applies/clears drawer filters, persists and restores
  session state, handles query failure, navigates to add/view/edit, deletes on confirm.
- `.../publish-status-detail/publish-status-detail.spec.ts` — loads the record, renders fields and
  counts, 404 path, no-id path, back/edit navigation.
- `.../publish-status-form/publish-status-form.spec.ts` — add mode (pkid enabled, required-field
  validation, POST body, 409 handling) and edit mode (pkid disabled, PUT includes the disabled key,
  load-failure recovery).
- `app.spec.ts` — asserts both Admin nav items render.

---

## Files to Create / Modify

### Backend

| File | Action |
|------|--------|
| `src/CMS.API/Models/PublishStatus.cs` | Create |
| `src/CMS.API/Models/PublishStatusRequest.cs` | Create |
| `src/CMS.API/Models/PublishStatusQuery.cs` | Create |
| `src/CMS.API/Models/PublishStatusLookup.cs` | Create |
| `src/CMS.API/Repositories/PublishStatusSql.cs` | Create |
| `src/CMS.API/Repositories/IPublishStatusRepository.cs` | Create |
| `src/CMS.API/Repositories/PublishStatusRepository.cs` | Create |
| `src/CMS.API/Controllers/PublishStatusesController.cs` | Create |
| `src/CMS.API/Repositories/ILookupRepository.cs` | Modify — add `GetPublishStatusesAsync` |
| `src/CMS.API/Repositories/LookupRepository.cs` | Modify — implement it |
| `src/CMS.API/Controllers/LookupsController.cs` | Modify — add `GET /publish-statuses` |
| `src/CMS.API/Program.cs` | Modify — register `IPublishStatusRepository` |

### Frontend

| File | Action |
|------|--------|
| `src/CMS.NG/src/app/core/models/publish-status.model.ts` | Create |
| `src/CMS.NG/src/app/core/models/publish-status-lookup.model.ts` | Create |
| `src/CMS.NG/src/app/core/services/publish-status.service.ts` | Create |
| `src/CMS.NG/src/app/features/publish-statuses/publish-status-list/*` (ts/html/scss) | Create |
| `src/CMS.NG/src/app/features/publish-statuses/publish-status-detail/*` | Create |
| `src/CMS.NG/src/app/features/publish-statuses/publish-status-form/*` | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.ts` | Modify — add `getPublishStatuses()` |
| `src/CMS.NG/src/app/app.routes.ts` | Modify — four lazy routes |
| `src/CMS.NG/src/app/app.ts` | Modify — nav item under 系統管理 Admin |

### Tests

| File | Action |
|------|--------|
| `src/CMS.API.Tests/Fakes/FakePublishStatusRepository.cs` | Create |
| `src/CMS.API.Tests/Controllers/PublishStatusesControllerTests.cs` | Create |
| `src/CMS.API.Tests/Repositories/PublishStatusSqlTests.cs` | Create |
| `src/CMS.API.Tests/Fakes/FakeLookupRepository.cs` | Modify |
| `src/CMS.API.Tests/Controllers/LookupsControllerTests.cs` | Modify |
| `src/CMS.NG/.../publish-status.service.spec.ts` | Create |
| `src/CMS.NG/.../publish-status-list.spec.ts` | Create |
| `src/CMS.NG/.../publish-status-detail.spec.ts` | Create |
| `src/CMS.NG/.../publish-status-form.spec.ts` | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.spec.ts` | Modify |
| `src/CMS.NG/src/app/app.spec.ts` | Modify |

---

## Deviations from the /crud skill template

- **No `RowAuditWriter` injection and no `RowAuditBadgeComponent`.** The `RowAudit` table exists in
  `database/admin.sql`, but the repository has no `RowAuditWriter`, no audit badge component, and no
  authentication to source a `UserName` from. `AppRoleRepository` — the only existing precedent —
  writes no audit rows. Building an audit subsystem here would invent a cross-cutting pattern for
  one entity; it is called out as follow-up work instead.
- **No sticky `p-toolbar`.** The existing pages use a `.page-header` action bar; this feature matches
  `AppRole` rather than introducing a second header pattern.
