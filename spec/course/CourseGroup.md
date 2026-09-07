# Build Spec for CourseGroup

- database schema: `.\database\course.sql` (lines 77–90; the `PartnerCourseGroup` FK back to it is
  repeated verbatim in `database\promotion.sql`)

`CourseGroup` is the smallest table in the schema built so far: a `pkid` and a single
`Description`. It is a pure lookup / classification table — the course taxonomy buckets that
`Course` rows are filed under. With no columns of its own to speak of, the interesting parts of
this feature are entirely in the relationships: the **delete guard** (one of the two inbound FKs is
`ON DELETE CASCADE`, so an unguarded delete silently destroys courses) and the **new lookup
endpoint** that `Course` and `PartnerCourseGroup` will both need.

---

## Summary

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **smallint IDENTITY(1,1)** — server-generated, immutable after create |
| Foreign Keys | None |
| Required Fields | `Description` |
| N-N Relationships | N/A — `PartnerCourseGroup` is a child entity, not a junction table |
| Primary-Foreign Links | `Course.CourseGroup_pkid` (**ON DELETE CASCADE**), `PartnerCourseGroup.CourseGroup_pkid` |
| Query Filters | keyword (`Description`); tri-state `inUse` |
| Default Sort | `ORDER BY cg.pkid ASC` |

---

## Localization

### Chinese Table Name

- CourseGroup: 課程群組
- Description: 課程分類群組代碼表 — 課程歸屬的分類桶，供課程與原廠課程群組引用

### Chinese Column Names

- pkid: 主代碼
- Description: 群組說明

Derived (not columns):

- CourseCount: 課程數
- PartnerCourseGroupCount: 原廠群組數

`Description` is labelled 群組說明 rather than 群組名稱, matching the `PublishStatus.Description`
→ 狀態說明 precedent: the column is a `Description`, and both tables are lookup code tables of the
same shape.

---

## Required Fields

Required (NOT NULL):

- `Description` — nvarchar(100)

Optional (nullable):

- None. Every non-key column in the table is NOT NULL.

`pkid` is `smallint IDENTITY(1,1)`, so it is excluded from the create request and is immutable on
edit — the ordinary case, as with `Partner`. Neither the `AppRole` string-PK special case nor the
`PublishStatus` non-IDENTITY (operator-supplied key) case applies.

The schema declares **no UNIQUE constraint** on `Description`, so the API does not enforce
uniqueness and there is no duplicate-description `409`.

---

## Foreign Keys

`CourseGroup` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

`CourseGroup` has no foreign key columns.

**N/A**

---

## Primary-Foreign Links

Two columns in the schema point at `CourseGroup.pkid`:

| Child table | FK column | Nullable | Constraint | On delete |
|-------------|-----------|----------|------------|-----------|
| `Course` | `CourseGroup_pkid` smallint | **NULL** | `FK_Course_CourseGroup` (`course.sql`) | **CASCADE** |
| `PartnerCourseGroup` | `CourseGroup_pkid` smallint | NOT NULL | `FK_PartnerCourseGroup_CourseGroup` (`course.sql`, repeated in `promotion.sql`) | NO ACTION |

**The `Course` cascade is the single most important fact in this spec.** `FK_Course_CourseGroup` is
declared `ON DELETE CASCADE` even though the FK column is nullable — so SQL Server does not null the
column out, it **deletes the `Course` rows**. Deleting a populated course group would therefore
destroy every course filed under it, silently and with a `204 No Content` in reply. `Course` in turn
cascades to `CourseInCertification` and `CourseJobCategories`, so the blast radius is wider still.

The delete guard is consequently not the usual "avoid surfacing SQL error 547 as a 500" politeness
that `Partner` and `PublishStatus` implement — for the `Course` arm it is the only thing standing
between the operator and data loss, because the database will happily perform the cascade. The
`PartnerCourseGroup` arm is the ordinary case and would raise 547.

Following the `PublishStatus` and `Partner` precedent, both relationships are surfaced as **counts**
rather than link buttons, because neither `/courses` nor `/partner-course-groups` has been built yet:

- Both are correlated subqueries in `CourseGroupSql.SelectBase`.
- The list shows them as two separate right-aligned columns — the table is only five columns wide,
  so there is room, and no summed 引用數 column with a tooltip is needed (that compression was a
  `Partner` concession to a nine-column table).
- The detail page repeats them under a 使用狀況 section.
- The controller reads the record before deleting and returns `409` when either count is non-zero.

When `/courses` and `/partner-course-groups` are built later, turn the counts into `pi pi-list` link
buttons targeting `/courses?courseGroupPkid={pkid}` and
`/partner-course-groups?courseGroupPkid={pkid}`.

---

## N-N Relationships

`PartnerCourseGroup` reads like a junction table between `Partner` and `CourseGroup`, and it is not
one: it carries its own `pkid` IDENTITY, a `DisplayOrder` and its own `Description`, and
`Promotion2.RelatedPartnerCourseGroup_pkid` holds an FK pointing at it. It is a child entity in its
own right and belongs in its own feature — not in a `p-multiselect` on this form. This is the same
call `spec/course/Partner.md` made about the same table.

**N/A**

---

## Query Filters

`POST /api/course-groups/query` accepts:

- **keyword** — `string?`
  - `LIKE` on `Description` only. It is the table's single string column; `pkid` is numeric and is
    covered by sorting rather than search. The column is nvarchar(100) — a short label, not prose,
    so there is nothing to exclude on length grounds.
  - LIKE wildcards (`%`, `_`, `[`) are escaped so a literal keyword matches literally, matching
    `PartnerSql.EscapeLike`.
  - Single column, so no parenthesised `OR` group is strictly required — one is emitted anyway so
    the clause composes safely if a second searchable column is ever added.

- **inUse** — `bool?` (tri-state: `null` = 全部, `true` = 已被引用, `false` = 未被引用)
  - `true` → the group is referenced by at least one `Course` **or** one `PartnerCourseGroup` row
  - `false` → referenced by neither
  - Implemented with `EXISTS` / `NOT EXISTS`, not by re-running the `COUNT(*)` subqueries, so the
    filter short-circuits on the first matching child row.

> **Judgment call, flagged for review.** `inUse` is not derived from a column — the table has no
> `bit` column and no dates, so keyword alone would be the strictly schema-derived filter set. It is
> included because a two-column code table is exactly the kind of thing that accumulates orphan rows,
> and "show me the groups nothing references" is the one question an operator maintaining this table
> actually asks. It reuses the reference counts the delete guard already needs. Strike it from this
> spec if you would rather the drawer carried keyword only.

No FK filters (`CourseGroup` has no FK columns) and no date-range filters (no `date` / `datetime`
columns).

---

## Lookup Endpoints Required

`CourseGroup` needs no lookups of its own — it has no FK columns, so neither its form nor its list
resolves anything into labels. It *is* an FK target twice over, so it must publish one:

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/course-groups` | **New** | `CourseGroupLookup[]` — `pkid`, `description`, ordered by `Description ASC` |

Ordered by `Description ASC` rather than `pkid ASC`: unlike `PublishStatus`, whose `pkid` values are
meaningful hand-assigned status codes that operators think in, `CourseGroup.pkid` is an opaque
IDENTITY number. Alphabetical is the only ordering a dropdown of these can usefully offer.

Nothing consumes this endpoint yet — `Course` and `PartnerCourseGroup` are unbuilt. It ships now so
the lookup layer stays complete per entity, exactly as `GET /api/lookups/partners` did.

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/course-groups` | List all, `ORDER BY pkid ASC` |
| `POST` | `/api/course-groups/query` | Filtered query (body: `CourseGroupQuery`; null body = unfiltered) |
| `GET` | `/api/course-groups/{id:int}` | Get by pkid; `404` when missing |
| `POST` | `/api/course-groups` | Create; `201 CreatedAtAction` |
| `PUT` | `/api/course-groups` | Update — **pkid comes from the body**, not the route; `404` when missing |
| `DELETE` | `/api/course-groups/{id:int}` | `204` on success, `404` when missing, `409` when any `Course` or `PartnerCourseGroup` row still references it |

Route constraint is `{id:int}` — the key is numeric, so the Angular service does **not** need
`encodeURIComponent` (that is the `AppRole` string-PK case). The action parameter is typed `short`;
an out-of-range value fails model binding and returns `400`.

No auth attributes — the API has no authentication wired yet, matching `AppRolesController`,
`PublishStatusesController` and `PartnersController`.

---

## Backend Notes

### Models

`Models/CourseGroup.cs` — response model:

```csharp
public class CourseGroup
{
    public short Pkid { get; set; }                              // 主代碼
    public string Description { get; set; } = string.Empty;      // 群組說明
    public int CourseCount { get; set; }                         // 課程數 — subquery
    public int PartnerCourseGroupCount { get; set; }             // 原廠群組數 — subquery
}
```

`Models/CourseGroupRequest.cs` — write DTO. `Pkid` is present but is only read on `PUT`; `POST`
ignores it because the column is IDENTITY:

```csharp
public class CourseGroupRequest
{
    public short Pkid { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string Description { get; set; } = string.Empty;
}
```

`Models/CourseGroupQuery.cs` — search DTO:

```csharp
public class CourseGroupQuery
{
    public string? Keyword { get; set; }
    public bool? InUse { get; set; }
}
```

`Models/CourseGroupLookup.cs` — slim option row (mirrors `PublishStatusLookup`):

```csharp
public class CourseGroupLookup
{
    public short Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}
```

### SQL — SELECT

`Repositories/CourseGroupSql.cs` holds the projection plus `BuildWhere`, following `AppRoleSql` /
`PublishStatusSql` / `PartnerSql` so the filter logic is unit-testable without a database.

```sql
SELECT cg.pkid AS Pkid,
       cg.Description,
       (SELECT COUNT(*) FROM Course c WHERE c.CourseGroup_pkid = cg.pkid) AS CourseCount,
       (SELECT COUNT(*) FROM PartnerCourseGroup g WHERE g.CourseGroup_pkid = cg.pkid) AS PartnerCourseGroupCount
FROM CourseGroup cg
```

`DefaultOrderBy` = `ORDER BY cg.pkid ASC`, matching `PublishStatusSql`. There is no `DisplayOrder`
column to prefer and no date column to fall back on; `pkid` ASC keeps insertion order, which for a
hand-curated code table is the order operators added the rows in. The list page's own default sort
is the same field, and the column is sortable so alphabetical is one click away.

No JOINs and no multi-map: there are no FK nav objects. No `nchar` columns, so no `RTRIM()`. No
`date` / `time(7)` columns, so no type handlers beyond the ones already registered in `Program.cs`.

### SQL — INSERT

`Description` is the only writable column; `pkid` is IDENTITY and is omitted.

```sql
INSERT INTO CourseGroup (Description)
VALUES (@Description);
SELECT CAST(SCOPE_IDENTITY() AS smallint);
```

`SCOPE_IDENTITY()` returns `decimal`, so the `CAST` to `smallint` is required for
`ExecuteScalarAsync<short>` to map cleanly — the same cast `PartnerRepository.CreateAsync` needs.

### SQL — UPDATE

`pkid` is the key and is immutable — it appears only in the WHERE clause.

```sql
UPDATE CourseGroup
SET Description = @Description
WHERE pkid = @Pkid;
```

`UpdateAsync` returns `false` when zero rows are affected, which the controller turns into `404`.

### SQL — DELETE

```sql
DELETE FROM CourseGroup WHERE pkid = @Pkid;
```

No junction rows to clean up first. The controller blocks the call before it reaches the database
whenever either count is non-zero — `GetByIdAsync` already projects both, so no extra repository
method is needed, and the same read supplies the `404`.

This guard is load-bearing rather than cosmetic. `FK_PartnerCourseGroup_CourseGroup` would raise SQL
error 547 on its own, but `FK_Course_CourseGroup` is `ON DELETE CASCADE`: the database would accept
the delete and take every referencing `Course` row with it (and, transitively,
`CourseInCertification` and `CourseJobCategories`). Do not relax this check.

### N-N Sync Pattern

**N/A** — no junction tables.

### Special Column Notes

- **`pkid` is `smallint`, not `int`** — C# `short` throughout: model, request, repository signatures,
  `ExecuteScalarAsync<short>`, and the controller's `short id` action parameter. The route keeps the
  `{id:int}` constraint (ASP.NET Core routing has no `:short`); binding narrows to `short` and
  rejects out-of-range values with `400`. Same as `Partner`.
- `Description` is `nvarchar(100)` — a short label, not prose. `StringLength(100)` and
  `maxlength="100"` on the input.
- No `nchar(n)` columns, so no `RTRIM()` anywhere in this feature.
- No nullable columns at all, so there is no null-vs-blank normalisation to do on save (the
  `Partner.ImageFilename` concern does not arise). `Description` is still `.trim()`ed client-side and
  rejected when blank by `[Required(AllowEmptyStrings = false)]`.
- `FK_Course_CourseGroup` is `ON DELETE CASCADE` — see **SQL — DELETE** above.

---

## Frontend Notes

### Routes

| Path | Component |
|------|-----------|
| `course-groups` | `CourseGroupList` |
| `course-groups/new` | `CourseGroupForm` |
| `course-groups/:id` | `CourseGroupDetail` |
| `course-groups/:id/edit` | `CourseGroupForm` |

Lazy `loadComponent` in `app.routes.ts`, `/new` registered **before** `/:id`, inserted ahead of the
`**` wildcard.

### Angular Model — `core/models/course-group.model.ts`

```ts
export interface CourseGroup {
  pkid: number;
  description: string;
  courseCount: number;
  partnerCourseGroupCount: number;
}

export interface CourseGroupRequest {
  pkid: number;
  description: string;
}

export interface CourseGroupQuery {
  keyword?: string | null;
  inUse?: boolean | null;
}
```

`core/models/course-group-lookup.model.ts` holds `{ pkid: number; description: string }`.

### Service — `core/services/course-group.service.ts`

Standard six methods against `${environment.apiUrl}/course-groups`. The key is numeric, so
`getById(pkid: number)` and `delete(pkid: number)` interpolate directly — **no
`encodeURIComponent`**. `update()` PUTs to the collection route with the key in the body.

`LookupService` gains `getCourseGroups()` hitting `/lookups/course-groups`.

### List component

Columns: 主代碼 / 群組說明 / 課程數 / 原廠群組數 / 操作.

- 課程數 and 原廠群組數 are separate right-aligned columns. The `Partner` list folded five counts
  into one 引用數 column with a tooltip because nine columns did not fit; here there are only five
  columns, so the counts are shown plainly and no tooltip helper is needed.

Sortable, paginated `p-table`, `dataKey="pkid"`, default sort `{ field: 'pkid', order: 1 }`, default
page `{ first: 0, rows: 20 }`, paginator on top, `rowsPerPageOptions [10, 20, 50, 100]`.

Filter drawer (`p-drawer`, `position="right"`): a keyword `input pInputText` plus one tri-state
`p-select` for 使用狀況 (`全部` / `已被引用` / `未被引用`, values `null` / `true` / `false`) with
`appendTo="body"`. Two options plus 全部, so no `[filter]` and no virtual scroll.

The 搜尋條件 button's `[badge]` counts applied filters as a **number**: keyword counts when
non-blank, `inUse` counts whenever it is neither `null` nor `undefined` — a `false` selection
(未被引用) is a real filter and a falsy check would silently drop it.

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `course-group-list-filters` | Applied `CourseGroupQuery` |
| `course-group-list-sort` | `{ field, order }` |
| `course-group-list-page` | `{ first, rows }` |

No incoming cross-entity query params — nothing navigates into this list yet. When `Course` and
`PartnerCourseGroup` are built, it is `CourseGroup` that will *emit* `?courseGroupPkid=` links.

### Lookup Binding in List

**N/A** — no FK columns, so there is nothing to resolve into labels and no `forkJoin` on the list.

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.description}」？
```

When either count is greater than zero, append the warning — and it is worded more strongly than
`Partner`'s, because the `Course` FK cascades:

```
此群組仍被 ${courseCount} 筆課程與 ${partnerCourseGroupCount} 筆原廠課程群組使用，將無法刪除。
```

A `409` response surfaces the toast 「此課程群組仍被其他資料使用，無法刪除。」

PrimeNG 20's `p-confirmDialog` has **no `escape` input** — it always renders `message` through
`[innerHTML]`, so the `<b>` markup works as written and `item.description` must be HTML-escaped
before interpolation (the same `escapeHtml` helper the other list components carry).

### Date Handling

**N/A** — no `date`, `time` or `datetime` columns, so neither the `toIso()` local-components rule nor
the `+ 'Z'` UTC fix applies.

### Form layout

| Field | Widget | Notes |
|-------|--------|-------|
| 主代碼 pkid | plain read-only text | Edit mode only; IDENTITY, never an input |
| 群組說明 Description | `input pInputText` `maxlength="100"` | Required |

One editable field. Reactive Forms all the same, for consistency with the other three features and
because the validation-message and dirty-state plumbing comes free with it.

There is no lookup to load, so **no `forkJoin` is needed** — add mode initialises an empty form and
edit mode loads the single record directly, exactly as `PartnerForm` does.

### Special Form Behaviors

- `pkid` is never an editable control: add mode has no key field at all (IDENTITY supplies it), and
  edit mode renders it as static text next to the heading. Nothing to `disable()` and no
  `getRawValue()` gymnastics — that pattern belongs to `AppRole` and `PublishStatus`, whose keys are
  client-supplied.
- `Description` is `.trim()`ed on save. It is NOT NULL with no blank-vs-null ambiguity, so no
  normalisation to `null`.
- No cross-field validation, no conditional visibility, no auto-defaulting.

### Sub-panels (edit mode only)

**N/A** — `Course` and `PartnerCourseGroup` are unbuilt, and both are substantial entities in their
own right rather than inline child rows.

### Sidebar placement

Existing group `課程管理 Course` in `app.ts` `navGroups`, appended after the existing
`原廠 Partner` item:

```ts
{ label: '課程群組 CourseGroup', icon: 'pi pi-sitemap', route: '/course-groups' }
```

`app.spec.ts` is extended to assert both items render under 課程管理 Course. That group is **not**
in `expandedGroups` (which still defaults to `系統管理 Admin` alone), so the spec must
`toggleGroup('課程管理 Course')` before asserting — the existing Partner spec case already does.

---

## Tests

### Backend — `src/CMS.API.Tests/`

Fakes, not a mocking library (house rule).

- `Fakes/FakeCourseGroupRepository.cs` — in-memory `ICourseGroupRepository` mirroring the real
  contract: keyword filtering on `Description`, tri-state `inUse`, `pkid ASC` ordering,
  IDENTITY-style key assignment on create, and settable `CourseCount` /
  `PartnerCourseGroupCount` so the delete-conflict path is reachable.
- `Fakes/FakeLookupRepository.cs` — extended with `GetCourseGroupsAsync`.
- `Controllers/CourseGroupsControllerTests.cs` — list, query (keyword, `inUse` true/false, combined,
  no match, null body), get-by-id found/not-found, create 201 + route value + IDENTITY key,
  update 200 with key-from-body, update missing 404, delete 204, delete missing 404, and **two
  separate delete-409 cases**: one referenced only by `Course` (the cascade arm — the case where the
  database would otherwise destroy rows) and one referenced only by `PartnerCourseGroup`.
- `Repositories/CourseGroupSqlTests.cs` — `BuildWhere` across every filter combination, keyword
  trimming and LIKE escaping, `inUse = false` producing a `NOT EXISTS` clause rather than being
  skipped as falsy, and the projection containing both count subqueries plus the default ORDER BY.
- `Controllers/LookupsControllerTests.cs` — a case for the new lookup route.

### Frontend — `src/CMS.NG/`

- `core/services/course-group.service.spec.ts` — each method's URL and verb; the key interpolates as
  a plain number; PUT carries the key in the body.
- `core/services/lookup.service.spec.ts` — a case for `getCourseGroups()`.
- `features/course-groups/course-group-list/course-group-list.spec.ts` — loads on init, renders rows,
  unfiltered body by default, applies/clears drawer filters, the filter badge counts `inUse = false`,
  persists and restores session state, handles query failure, navigates to add/view/edit, deletes on
  confirm, and surfaces the 409 toast.
- `.../course-group-detail/course-group-detail.spec.ts` — loads the record, renders fields and both
  counts, 404 path, no-id path, back/edit navigation.
- `.../course-group-form/course-group-form.spec.ts` — add mode (required-field validation, POST body,
  description trimmed) and edit mode (pkid shown as text, PUT includes the key in the body,
  load-failure recovery).
- `app.spec.ts` — asserts 課程管理 Course now renders both its Partner and CourseGroup items.

---

## Files to Create / Modify

### Backend

| File | Action |
|------|--------|
| `src/CMS.API/Models/CourseGroup.cs` | Create |
| `src/CMS.API/Models/CourseGroupRequest.cs` | Create |
| `src/CMS.API/Models/CourseGroupQuery.cs` | Create |
| `src/CMS.API/Models/CourseGroupLookup.cs` | Create |
| `src/CMS.API/Repositories/CourseGroupSql.cs` | Create |
| `src/CMS.API/Repositories/ICourseGroupRepository.cs` | Create |
| `src/CMS.API/Repositories/CourseGroupRepository.cs` | Create |
| `src/CMS.API/Controllers/CourseGroupsController.cs` | Create |
| `src/CMS.API/Repositories/ILookupRepository.cs` | Modify — add `GetCourseGroupsAsync` |
| `src/CMS.API/Repositories/LookupRepository.cs` | Modify — implement it |
| `src/CMS.API/Controllers/LookupsController.cs` | Modify — add `GET /course-groups` |
| `src/CMS.API/Program.cs` | Modify — register `ICourseGroupRepository` |

### Frontend

| File | Action |
|------|--------|
| `src/CMS.NG/src/app/core/models/course-group.model.ts` | Create |
| `src/CMS.NG/src/app/core/models/course-group-lookup.model.ts` | Create |
| `src/CMS.NG/src/app/core/services/course-group.service.ts` | Create |
| `src/CMS.NG/src/app/features/course-groups/course-group-list/*` (ts/html/scss) | Create |
| `src/CMS.NG/src/app/features/course-groups/course-group-detail/*` | Create |
| `src/CMS.NG/src/app/features/course-groups/course-group-form/*` | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.ts` | Modify — add `getCourseGroups()` |
| `src/CMS.NG/src/app/app.routes.ts` | Modify — four lazy routes |
| `src/CMS.NG/src/app/app.ts` | Modify — nav item under 課程管理 Course |

### Tests

| File | Action |
|------|--------|
| `src/CMS.API.Tests/Fakes/FakeCourseGroupRepository.cs` | Create |
| `src/CMS.API.Tests/Controllers/CourseGroupsControllerTests.cs` | Create |
| `src/CMS.API.Tests/Repositories/CourseGroupSqlTests.cs` | Create |
| `src/CMS.API.Tests/Fakes/FakeLookupRepository.cs` | Modify |
| `src/CMS.API.Tests/Controllers/LookupsControllerTests.cs` | Modify |
| `src/CMS.NG/.../course-group.service.spec.ts` | Create |
| `src/CMS.NG/.../course-group-list.spec.ts` | Create |
| `src/CMS.NG/.../course-group-detail.spec.ts` | Create |
| `src/CMS.NG/.../course-group-form.spec.ts` | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.spec.ts` | Modify |
| `src/CMS.NG/src/app/app.spec.ts` | Modify |

---

## Deviations from the /crud skill template

- **No `RowAuditWriter` injection and no `RowAuditBadgeComponent`.** The `RowAudit` table exists in
  `database/admin.sql`, but the repository layer has no `RowAuditWriter`, no audit badge component,
  and no authentication to source a `UserName` from. None of `AppRoleRepository`,
  `PublishStatusRepository` or `PartnerRepository` writes audit rows. Same call as
  `spec/admin/PublishStatus.md` and `spec/course/Partner.md`: follow-up work, not a one-entity
  cross-cutting invention.
- **No mocking library for the backend tests.** CLAUDE.md mandates hand-written fakes in
  `src/CMS.API.Tests/Fakes/`; the skill's suggestion of Moq is not followed.
- **No sticky `p-toolbar`.** The existing pages use a `.page-header` action bar; this feature matches
  the three built features rather than introducing a second header pattern.
