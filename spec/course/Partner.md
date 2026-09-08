# Build Spec for Partner

- database schema: `.\database\course.sql` (the identical `CREATE TABLE [dbo].[Partner]` block also
  appears in `database\promotion.sql` — one table, repeated for readability)

`Partner` is a small, hand-curated top-level catalogue entity: the training brands / vendors
(原廠) whose courses and certifications the site sells. It carries **no foreign keys of its own**
and is the FK target of five tables across two sub-systems, which makes the delete guard and the
new lookup endpoint the two interesting parts of this feature.

---

## Summary

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **smallint IDENTITY(1,1)** — server-generated, immutable after create |
| Foreign Keys | None |
| Required Fields | `Name`, `AppKey`, `NameOnPartnerMenu`, `NameOnCourseDetailPage`, `DisplayOrder` |
| N-N Relationships | N/A — `PartnerCourseGroup` is a child entity, not a junction table |
| Primary-Foreign Links | `Certification`, `Course`, `PartnerCourseGroup`, `Promotion2.RelatedPartner_pkid`, `Seminar` (unenforced) |
| Query Filters | keyword (`Name`, `AppKey`, `NameOnPartnerMenu`, `NameOnCourseDetailPage`); tri-state `hasImage` |
| Default Sort | `ORDER BY p.DisplayOrder ASC, p.pkid ASC` |

---

## Localization

### Chinese Table Name

- Partner: 原廠
- Description: 課程原廠／品牌合作夥伴 — 課程與認證的來源單位

### Chinese Column Names

- pkid: 主代碼
- Name: 原廠名稱
- AppKey: 應用代碼
- NameOnPartnerMenu: 選單顯示名稱
- NameOnCourseDetailPage: 課程頁顯示名稱
- DisplayOrder: 顯示順序
- ImageFilename: 圖片檔名

Derived (not columns):

- CertificationCount: 認證數
- CourseCount: 課程數
- CourseGroupCount: 課程群組數
- PromotionCount: 活動數
- SeminarCount: 說明會數
- (list column) 引用數 — the sum of the five counts, computed client-side

---

## Required Fields

Required (NOT NULL):

- `Name` — nvarchar(50)
- `AppKey` — varchar(10)
- `NameOnPartnerMenu` — nvarchar(200)
- `NameOnCourseDetailPage` — nvarchar(50)
- `DisplayOrder` — int

Optional (nullable):

- `ImageFilename` — varchar(50)

`pkid` is `smallint IDENTITY(1,1)`, so it is excluded from the create request and is immutable on
edit. This is the ordinary case — neither the `AppRole` string-PK special case nor the
`PublishStatus` non-IDENTITY case applies.

`AppKey` is `varchar`, not `nvarchar` — ASCII only in practice. The schema declares **no UNIQUE
constraint** on it, so the API does not enforce uniqueness and there is no duplicate-`AppKey` 409.

---

## Foreign Keys

`Partner` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

`Partner` has no foreign key columns.

**N/A**

---

## Primary-Foreign Links

Five columns across the schema point at `Partner.pkid`:

| Child table | FK column | Nullable | Constraint | Enforced |
|-------------|-----------|----------|------------|----------|
| `Certification` | `Partner_pkid` smallint | NOT NULL | `FK_Certification_Partner` (`course.sql`) | Yes |
| `Course` | `Partner_pkid` smallint | NOT NULL | `FK_Course_Partner` (`course.sql`) | Yes |
| `PartnerCourseGroup` | `Partner_pkid` smallint | NOT NULL | `FK_PartnerCourseGroup_Partner` (`course.sql`, repeated in `promotion.sql`) | Yes |
| `Promotion2` | `RelatedPartner_pkid` smallint | NULL | `FK_Promotion2_Partner` (`promotion.sql`) | Yes |
| `Seminar` | `Partner_pkid` smallint | NULL | — none — | **No** |

`Seminar.Partner_pkid` is a real reference with **no `FOREIGN KEY` constraint behind it** — deleting
a referenced `Partner` would silently orphan seminar rows rather than raising SQL error 547.

None of these five features has been built yet, so there are **no routes to link to**. Following the
`PublishStatus` precedent, the relationship is surfaced as **counts** instead of dead buttons:

- All five are correlated subqueries in `PartnerSql.SelectBase`.
- List page shows a single right-aligned 引用數 column (the sum) — five separate count columns would
  make an already nine-column table unreadable.
- Detail page breaks the five out individually under a 使用狀況 section.
- The controller reads the record before deleting and returns `409` when the total is non-zero —
  `Seminar` included, precisely because its FK is *not* enforced and the database would not stop it.

When `/courses`, `/certifications`, `/partner-course-groups`, `/promotions` and `/seminars` are
built later, turn the detail-page counts into `pi pi-list` link buttons targeting
`/courses?partnerPkid={pkid}` and friends.

---

## N-N Relationships

`PartnerCourseGroup` looks like a junction table by name but is not one: it carries `pkid` IDENTITY,
`DisplayOrder` and `Description` alongside its two FK columns, and `Promotion2` holds an FK to *it*.
It is a child entity in its own right and belongs in its own feature, not in a `p-multiselect` here.

**N/A**

---

## Query Filters

`POST /api/partners/query` accepts:

- **keyword** — `string?`
  - `LIKE` across the four short identifying string columns: `Name`, `AppKey`,
    `NameOnPartnerMenu`, `NameOnCourseDetailPage`. All are nvarchar(200) or shorter; the table has
    no `nvarchar(max)` or long-prose column to exclude.
  - Wrapped in a single parenthesised `OR` group so it ANDs correctly with the other filters.
  - LIKE wildcards (`%`, `_`, `[`) are escaped so a literal keyword matches literally.

- **hasImage** — `bool?` (tri-state: `null` = no filter, `true` = 有圖, `false` = 無圖)
  - `true` → `p.ImageFilename IS NOT NULL AND p.ImageFilename <> ''`
  - `false` → `p.ImageFilename IS NULL OR p.ImageFilename = ''`
  - The empty-string arm matters: `varchar` columns in this schema hold `''` as often as `NULL`, so
    a plain `IS NULL` test would report blank rows as 有圖.

No FK filters (no FK columns) and no date-range filters (no `date` / `datetime` columns).

---

## Lookup Endpoints Required

`Partner` needs no lookups of its own, but it is the FK target of five tables, so it must publish
one:

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/partners` | **New** | `PartnerLookup[]` — `pkid`, `name`, `appKey`, ordered by `DisplayOrder ASC, Name ASC` |

`appKey` rides along because the operator-facing option label is `Name`, but `Course` and
`Certification` forms will want the code visible to disambiguate similarly-named brands.

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/partners` | List all, `ORDER BY DisplayOrder ASC, pkid ASC` |
| `POST` | `/api/partners/query` | Filtered query (body: `PartnerQuery`; null body = unfiltered) |
| `GET` | `/api/partners/{id:int}` | Get by pkid; `404` when missing |
| `POST` | `/api/partners` | Create; `201 CreatedAtAction` |
| `PUT` | `/api/partners` | Update — **pkid comes from the body**, not the route; `404` when missing |
| `DELETE` | `/api/partners/{id:int}` | `204` on success, `404` when missing, `409` when any child table still references it |

Route constraint is `{id:int}` — the key is numeric, so the Angular service does **not** need
`encodeURIComponent` (that is the `AppRole` string-PK case). The action parameter is typed `short`;
an out-of-range value (e.g. `99999`) fails model binding and returns `400`.

No auth attributes — the API has no authentication wired yet, matching `AppRolesController` and
`PublishStatusesController`.

---

## Backend Notes

### Models

`Models/Partner.cs` — response model:

```csharp
public class Partner
{
    public short Pkid { get; set; }                                     // 主代碼
    public string Name { get; set; } = string.Empty;                    // 原廠名稱
    public string AppKey { get; set; } = string.Empty;                  // 應用代碼
    public string NameOnPartnerMenu { get; set; } = string.Empty;       // 選單顯示名稱
    public string NameOnCourseDetailPage { get; set; } = string.Empty;  // 課程頁顯示名稱
    public int DisplayOrder { get; set; }                               // 顯示順序
    public string? ImageFilename { get; set; }                          // 圖片檔名
    public int CertificationCount { get; set; }                         // 認證數 — subquery
    public int CourseCount { get; set; }                                // 課程數 — subquery
    public int CourseGroupCount { get; set; }                           // 課程群組數 — subquery
    public int PromotionCount { get; set; }                             // 活動數 — subquery
    public int SeminarCount { get; set; }                               // 說明會數 — subquery
}
```

`Models/PartnerRequest.cs` — write DTO. `Pkid` is present but is only read on `PUT`; `POST` ignores
it because the column is IDENTITY:

```csharp
public class PartnerRequest
{
    public short Pkid { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(10)]
    public string AppKey { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string NameOnPartnerMenu { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string NameOnCourseDetailPage { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    [StringLength(50)]
    public string? ImageFilename { get; set; }
}
```

`Models/PartnerQuery.cs` — search DTO:

```csharp
public class PartnerQuery
{
    public string? Keyword { get; set; }
    public bool? HasImage { get; set; }
}
```

`Models/PartnerLookup.cs` — slim option row (mirrors `PublishStatusLookup`):

```csharp
public class PartnerLookup
{
    public short Pkid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
}
```

### SQL — SELECT

`Repositories/PartnerSql.cs` holds the projection plus `BuildWhere`, following `AppRoleSql` /
`PublishStatusSql` so the filter logic is unit-testable without a database.

```sql
SELECT p.pkid AS Pkid,
       p.Name,
       p.AppKey,
       p.NameOnPartnerMenu,
       p.NameOnCourseDetailPage,
       p.DisplayOrder,
       p.ImageFilename,
       (SELECT COUNT(*) FROM Certification ct WHERE ct.Partner_pkid = p.pkid) AS CertificationCount,
       (SELECT COUNT(*) FROM Course c WHERE c.Partner_pkid = p.pkid) AS CourseCount,
       (SELECT COUNT(*) FROM PartnerCourseGroup g WHERE g.Partner_pkid = p.pkid) AS CourseGroupCount,
       (SELECT COUNT(*) FROM Promotion2 pr WHERE pr.RelatedPartner_pkid = p.pkid) AS PromotionCount,
       (SELECT COUNT(*) FROM Seminar s WHERE s.Partner_pkid = p.pkid) AS SeminarCount
FROM Partner p
```

`DefaultOrderBy` = `ORDER BY p.DisplayOrder ASC, p.pkid ASC`. `pkid` is the tie-breaker so that the
common case of several partners sharing `DisplayOrder = 0` still paginates deterministically.

No JOINs and no multi-map: there are no FK nav objects. No `nchar` columns, so no `RTRIM()`. No
`date` / `time(7)` columns, so no type handlers beyond the ones already registered in `Program.cs`.

### SQL — INSERT

All six non-key columns are writable; `pkid` is IDENTITY and is omitted.

```sql
INSERT INTO Partner (Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage, DisplayOrder, ImageFilename)
VALUES (@Name, @AppKey, @NameOnPartnerMenu, @NameOnCourseDetailPage, @DisplayOrder, @ImageFilename);
SELECT CAST(SCOPE_IDENTITY() AS smallint);
```

`SCOPE_IDENTITY()` returns `decimal`, so the `CAST` to `smallint` is required for
`ExecuteScalarAsync<short>` to map cleanly.

### SQL — UPDATE

`pkid` is the key and is immutable — it appears only in the WHERE clause.

```sql
UPDATE Partner
SET Name = @Name,
    AppKey = @AppKey,
    NameOnPartnerMenu = @NameOnPartnerMenu,
    NameOnCourseDetailPage = @NameOnCourseDetailPage,
    DisplayOrder = @DisplayOrder,
    ImageFilename = @ImageFilename
WHERE pkid = @Pkid;
```

`UpdateAsync` returns `false` when zero rows are affected, which the controller turns into `404`.

### SQL — DELETE

```sql
DELETE FROM Partner WHERE pkid = @Pkid;
```

No junction rows to clean up first. Four of the five referencing tables hold enforced FKs, so
deleting a referenced row would raise SQL error 547; the controller blocks the call first —
`GetByIdAsync` already projects all five counts, so no extra repository method is needed — and
returns `409` with a Chinese `ProblemDetails` title. The same read supplies the `404`.

### N-N Sync Pattern

**N/A** — no junction tables.

### Special Column Notes

- **`pkid` is `smallint`, not `int`** — C# `short` throughout: model, request, repository signatures,
  `ExecuteScalarAsync<short>`, and the controller's `short id` action parameter. The route keeps the
  `{id:int}` constraint (there is no `:short` constraint in ASP.NET Core routing); binding narrows to
  `short` and rejects out-of-range values with `400`.
- `AppKey` and `ImageFilename` are `varchar`, the rest of the strings are `nvarchar`. No Dapper
  handling difference; only the `StringLength` limits differ.
- `ImageFilename` is the only nullable column. It is a bare filename, not a path or URL — there is no
  configured image base URL in `environment.ts`, so it renders as text, not an `<img>`.
- No `nchar(n)` columns, so no `RTRIM()` anywhere in this feature.
- The `Partner` `CREATE TABLE` block is duplicated verbatim in `course.sql` and `promotion.sql`.
  Both scripts are `USE [CMS]` and the definitions match exactly — one table, listed twice.

---

## Frontend Notes

### Routes

| Path | Component |
|------|-----------|
| `partners` | `PartnerList` |
| `partners/new` | `PartnerForm` |
| `partners/:id` | `PartnerDetail` |
| `partners/:id/edit` | `PartnerForm` |

Lazy `loadComponent` in `app.routes.ts`, `/new` registered **before** `/:id`, inserted ahead of the
`**` wildcard.

### Angular Model — `core/models/partner.model.ts`

```ts
export interface Partner {
  pkid: number;
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename: string | null;
  certificationCount: number;
  courseCount: number;
  courseGroupCount: number;
  promotionCount: number;
  seminarCount: number;
}

export interface PartnerRequest {
  pkid: number;
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename: string | null;
}

export interface PartnerQuery {
  keyword?: string | null;
  hasImage?: boolean | null;
}
```

`core/models/partner-lookup.model.ts` holds `{ pkid: number; name: string; appKey: string }`.

### Service — `core/services/partner.service.ts`

Standard six methods against `${environment.apiUrl}/partners`. The key is numeric, so
`getById(pkid: number)` and `delete(pkid: number)` interpolate directly — **no
`encodeURIComponent`**. `update()` PUTs to the collection route with the key in the body.

`LookupService` gains `getPartners()` hitting `/lookups/partners`.

### List component

Columns: 主代碼 / 原廠名稱 / 應用代碼 / 選單顯示名稱 / 課程頁顯示名稱 / 顯示順序 / 圖片檔名 /
引用數 / 操作.

- 引用數 is a right-aligned computed total (`certificationCount + courseCount + courseGroupCount +
  promotionCount + seminarCount`) exposed by a `referenceCount(item)` helper, with a `pTooltip`
  breaking the five parts out. Five separate columns would make the table unreadable.
- 圖片檔名 renders `—` when null or blank.

Sortable, paginated `p-table`, `dataKey="pkid"`, default sort `{ field: 'displayOrder', order: 1 }`,
default page `{ first: 0, rows: 20 }`, paginator on top, `rowsPerPageOptions [10, 20, 50, 100]`.

Filter drawer (`p-drawer`, `position="right"`): a keyword `input pInputText` plus one tri-state
`p-select` for 圖片 (`全部` / `有圖` / `無圖`, values `null` / `true` / `false`) with
`appendTo="body"`. Two options plus 全部, so no `[filter]` and no virtual scroll.

The 搜尋條件 button's `[badge]` counts applied filters as a **number**: keyword counts when
non-blank, `hasImage` counts whenever it is neither `null` nor `undefined` — a `false` selection
(無圖) is a real filter and a falsy check would silently drop it.

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `partner-list-filters` | Applied `PartnerQuery` |
| `partner-list-sort` | `{ field, order }` |
| `partner-list-page` | `{ first, rows }` |

No incoming cross-entity query params — nothing navigates into this list yet. When `Course` and
friends are built, it is `Partner` that will *emit* `?partnerPkid=` links, not receive them.

### Lookup Binding in List

**N/A** — no FK columns, so there is nothing to resolve into labels and no `forkJoin` on the list.

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.name}」？
```

When the reference total is greater than zero, append:

```
此原廠仍被 ${courseCount} 筆課程、${certificationCount} 筆認證、${courseGroupCount} 筆課程群組、
${promotionCount} 筆活動與 ${seminarCount} 筆說明會使用，將無法刪除。
```

A `409` response surfaces the toast 「此原廠仍被其他資料使用，無法刪除。」

PrimeNG 20's `p-confirmDialog` has **no `escape` input** — it always renders `message` through
`[innerHTML]`, so the `<b>` markup works as written and `item.name` must be HTML-escaped before
interpolation (the same `escapeHtml` helper the other list components carry).

### Date Handling

**N/A** — no `date`, `time` or `datetime` columns, so neither the `toIso()` local-components rule nor
the `+ 'Z'` UTC fix applies.

### Form layout

| Field | Widget | Notes |
|-------|--------|-------|
| 主代碼 pkid | plain read-only text | Edit mode only; IDENTITY, never an input |
| 原廠名稱 Name | `input pInputText` `maxlength="50"` | Required |
| 應用代碼 AppKey | `input pInputText` `maxlength="10"` | Required |
| 選單顯示名稱 NameOnPartnerMenu | `input pInputText` `maxlength="200"` | Required |
| 課程頁顯示名稱 NameOnCourseDetailPage | `input pInputText` `maxlength="50"` | Required |
| 顯示順序 DisplayOrder | `p-inputNumber` `[useGrouping]="false"` | Required, defaults `0` |
| 圖片檔名 ImageFilename | `input pInputText` `maxlength="50"` | Optional |

Reactive Forms. There is no lookup to load, so **no `forkJoin` is needed** — add mode initialises an
empty form and edit mode loads the single record directly. Validation messages are Traditional
Chinese.

### Special Form Behaviors

- `pkid` is never an editable control: add mode has no key field at all (IDENTITY supplies it), and
  edit mode renders it as static text next to the heading. Nothing to `disable()` and no
  `getRawValue()` gymnastics — that pattern belongs to `AppRole` and `PublishStatus`, whose keys are
  client-supplied.
- Blank 圖片檔名 is normalised to `null` on save rather than sent as `''`, keeping the `hasImage`
  filter's two arms honest.
- No cross-field validation.

### Sub-panels (edit mode only)

**N/A**

### Sidebar placement

Existing group `課程管理 Course` in `app.ts` `navGroups` — it currently has an **empty `items`
array**, so this is its first entry and the first non-Admin group to light up:

```ts
{ label: '原廠 Partner', icon: 'pi pi-building', route: '/partners' }
```

`app.spec.ts` is extended to assert the item renders under 課程管理 Course. The group's collapsed
state is unchanged (`expandedGroups` still defaults to `系統管理 Admin` only).

---

## Tests

### Backend — `src/CMS.API.Tests/`

Fakes, not a mocking library (house rule).

- `Fakes/FakePartnerRepository.cs` — in-memory `IPartnerRepository` mirroring the real contract:
  keyword filtering across the four string columns, tri-state `hasImage` (including the blank-string
  arm), `DisplayOrder ASC, pkid ASC` ordering, IDENTITY-style key assignment on create, and settable
  reference counts so the delete-conflict path is reachable.
- `Fakes/FakeLookupRepository.cs` — extended with `GetPartnersAsync`.
- `Controllers/PartnersControllerTests.cs` — list, query (keyword, `hasImage` true/false, combined,
  no match, null body), get-by-id found/not-found, create 201 + route value + IDENTITY key,
  update 200 with key-from-body, update missing 404, delete 204, delete missing 404, delete
  referenced 409 (including the unenforced `Seminar` case).
- `Repositories/PartnerSqlTests.cs` — `BuildWhere` across every filter combination, keyword trimming
  and LIKE escaping, the keyword OR-group being parenthesised, `hasImage = false` producing a clause
  rather than being skipped as falsy, and the projection containing all five count subqueries plus
  the two-column default ORDER BY.
- `Controllers/LookupsControllerTests.cs` — a case for the new lookup route.

### Frontend — `src/CMS.NG/`

- `core/services/partner.service.spec.ts` — each method's URL and verb; the key interpolates as a
  plain number; PUT carries the key in the body.
- `core/services/lookup.service.spec.ts` — a case for `getPartners()`.
- `features/partners/partner-list/partner-list.spec.ts` — loads on init, renders rows, unfiltered
  body by default, applies/clears drawer filters, the filter badge counts `hasImage = false`,
  persists and restores session state, handles query failure, navigates to add/view/edit, deletes on
  confirm, and surfaces the 409 toast.
- `.../partner-detail/partner-detail.spec.ts` — loads the record, renders fields and the five counts,
  404 path, no-id path, back/edit navigation.
- `.../partner-form/partner-form.spec.ts` — add mode (required-field validation, POST body, blank
  image filename normalised to null) and edit mode (pkid shown as text, PUT includes the key in the
  body, load-failure recovery).
- `app.spec.ts` — asserts the 課程管理 Course group now renders its Partner item.

---

## Files to Create / Modify

### Backend

| File | Action |
|------|--------|
| `src/CMS.API/Models/Partner.cs` | Create |
| `src/CMS.API/Models/PartnerRequest.cs` | Create |
| `src/CMS.API/Models/PartnerQuery.cs` | Create |
| `src/CMS.API/Models/PartnerLookup.cs` | Create |
| `src/CMS.API/Repositories/PartnerSql.cs` | Create |
| `src/CMS.API/Repositories/IPartnerRepository.cs` | Create |
| `src/CMS.API/Repositories/PartnerRepository.cs` | Create |
| `src/CMS.API/Controllers/PartnersController.cs` | Create |
| `src/CMS.API/Repositories/ILookupRepository.cs` | Modify — add `GetPartnersAsync` |
| `src/CMS.API/Repositories/LookupRepository.cs` | Modify — implement it |
| `src/CMS.API/Controllers/LookupsController.cs` | Modify — add `GET /partners` |
| `src/CMS.API/Program.cs` | Modify — register `IPartnerRepository` |

### Frontend

| File | Action |
|------|--------|
| `src/CMS.NG/src/app/core/models/partner.model.ts` | Create |
| `src/CMS.NG/src/app/core/models/partner-lookup.model.ts` | Create |
| `src/CMS.NG/src/app/core/services/partner.service.ts` | Create |
| `src/CMS.NG/src/app/features/partners/partner-list/*` (ts/html/scss) | Create |
| `src/CMS.NG/src/app/features/partners/partner-detail/*` | Create |
| `src/CMS.NG/src/app/features/partners/partner-form/*` | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.ts` | Modify — add `getPartners()` |
| `src/CMS.NG/src/app/app.routes.ts` | Modify — four lazy routes |
| `src/CMS.NG/src/app/app.ts` | Modify — nav item under 課程管理 Course |

### Tests

| File | Action |
|------|--------|
| `src/CMS.API.Tests/Fakes/FakePartnerRepository.cs` | Create |
| `src/CMS.API.Tests/Controllers/PartnersControllerTests.cs` | Create |
| `src/CMS.API.Tests/Repositories/PartnerSqlTests.cs` | Create |
| `src/CMS.API.Tests/Fakes/FakeLookupRepository.cs` | Modify |
| `src/CMS.API.Tests/Controllers/LookupsControllerTests.cs` | Modify |
| `src/CMS.NG/.../partner.service.spec.ts` | Create |
| `src/CMS.NG/.../partner-list.spec.ts` | Create |
| `src/CMS.NG/.../partner-detail.spec.ts` | Create |
| `src/CMS.NG/.../partner-form.spec.ts` | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.spec.ts` | Modify |
| `src/CMS.NG/src/app/app.spec.ts` | Modify |

---

## Deviations from the /crud skill template

- **~~No `RowAuditWriter` injection~~ — closed.** Recorded when no repository wrote the `RowAudit`
  table and there was no authentication to source a `UserName` from. Both arrived later:
  `PartnerRepository` writes an audit row on every insert, update and delete, on the same
  transaction as the change, and `PartnerRepositoryAuditTests` is the worked example the other
  entities' retrofits follow. See the 異動紀錄 section of `spec/conventions/backend.md`. The
  `RowAuditBadgeComponent` the skill asks for still does not exist — nothing reads the trail back.
- **No mocking library for the backend tests.** CLAUDE.md mandates hand-written fakes in
  `src/CMS.API.Tests/Fakes/`; the skill's suggestion of Moq is not followed.
- **No sticky `p-toolbar`.** The existing pages use a `.page-header` action bar; this feature matches
  `AppRole` / `PublishStatus` rather than introducing a second header pattern.
