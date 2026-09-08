# Build Spec for Course

- database schema: `.\database\course.sql` (the `CREATE TABLE` at lines 20–52, plus the
  `DF_Course_*` defaults, the `OtherInfo` late-add `ALTER TABLE`, and the three outbound FK
  constraints near the end of the file)

`Course` is the largest entity built so far and the first that is genuinely *relational*: three
foreign keys resolved into nav objects, two junction tables, two `date` columns, two `decimal`
columns of different scale, and six referencing tables. Everything the four features built to date
introduced one at a time — count subqueries, tri-state filters, immutable keys — shows up here at
once, and three things are new: **multi-map FK nav objects**, **n-n writes inside a transaction**,
and **`DateOnly` round-tripping** through the type handlers that have been registered in
`Program.cs` since day one but never exercised.

---

## Summary

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **int IDENTITY(1,1)** — server-generated, immutable after create |
| Foreign Keys | `Partner_pkid` → `Partner.pkid` (NOT NULL), `CourseGroup_pkid` → `CourseGroup.pkid` (**nullable**), `PublishStatus_pkid` → `PublishStatus.pkid` (NOT NULL) |
| Required Fields | `Title`, `CourseId`, `ProdCourseId`, `FriendlyUrl`, `DisplayOrder`, `Partner_pkid`, `PublishStatus_pkid`, `ScheduleOn`, `ScheduleOff`, `Hour`, `ListPrice`, `LearningCredit`, `CanRepeat` |
| N-N Relationships | `CourseInCertification` (Course ↔ Certification), `CourseJobCategories` (Course ↔ JobCategory) |
| Primary-Foreign Links | `CourseFAQ`, `CourseRelatedLink`, `HotCourse` (enforced FKs); `CourseRecomm` (unenforced, joins on `CourseId`) |
| Query Filters | keyword; three FK selects; `ScheduleOn` range; `ScheduleOff` range; tri-state `canRepeat` |
| Default Sort | `ORDER BY c.DisplayOrder ASC, c.pkid ASC` |

---

## Localization

### Chinese Table Name

- Course: 課程
- Description: 訓練課程主資料 — 課程的完整內容、排程、定價與分類

### Chinese Column Names

The labels below follow the display-name hints supplied with the `/crud` invocation, which take
precedence over `spec/sample1.spec.md` where the two disagree. The differences are deliberate and
are listed under **Deviations** at the end.

- pkid: 主代碼
- Title: 課程名稱
- OfficialTitle: 官方課程名稱
- CourseId: 簡介代碼
- ProdCourseId: 科目代碼
- FriendlyUrl: 友善網址
- DisplayOrder: 顯示順序
- Partner_pkid: 原廠
- CourseGroup_pkid: 課程群組
- PublishStatus_pkid: 上架狀態
- ScheduleOn: 上架日期
- ScheduleOff: 下架日期
- Hour: 時數
- ListPrice: 定價
- LearningCredit: 點數
- Material: 教材
- Objective: 課程目標
- Target: 適合對象
- Prerequisites: 先備知識
- Outline: 課程大綱
- TowardCertOrExam: 考試／認證說明
- Note: 備註
- OtherInfo: 其他資訊
- CanRepeat: 允許重聽

Derived (not columns):

- CertificationPkids / 對應認證
- JobCategoryPkids / 對應職務類別
- CourseFaqCount: 課程問答數
- CourseRelatedLinkCount: 相關連結數
- HotCourseCount: 熱門課程數
- CourseRecommCount: 推薦課程數

`Partner_pkid` is labelled 原廠, matching the built `Partner` feature's own heading rather than
sample1's 合作廠商.

---

## Required Fields

Required (NOT NULL, excluding the IDENTITY key):

- `Title` — nvarchar(200)
- `CourseId` — varchar(50)
- `ProdCourseId` — varchar(50)
- `FriendlyUrl` — nvarchar(100)
- `DisplayOrder` — int
- `Partner_pkid` — smallint
- `PublishStatus_pkid` — tinyint
- `ScheduleOn` — date
- `ScheduleOff` — date
- `Hour` — smallint, `DEFAULT 0`
- `ListPrice` — decimal(9,0), `DEFAULT 0`
- `LearningCredit` — decimal(9,1), `DEFAULT 0`
- `CanRepeat` — bit, `DEFAULT 0`

Optional (nullable):

- `OfficialTitle` nvarchar(300), `CourseGroup_pkid` smallint, `Material` nvarchar(500),
  `Objective` nvarchar(4000), `Target` nvarchar(500), `Prerequisites` nvarchar(4000),
  `Outline` nvarchar(max), `TowardCertOrExam` nvarchar(max), `Note` nvarchar(4000),
  `OtherInfo` nvarchar(4000)

`pkid` is `int IDENTITY(1,1)` — the ordinary case. Neither the `AppRole` string-PK special case nor
the `PublishStatus` operator-supplied-key case applies: the key is omitted from create and immutable
on edit.

The four `DEFAULT` constraints match C#'s own defaults (`0` / `false`), so no server-side default
handling is needed — the request always carries an explicit value.

The schema declares **no UNIQUE constraint** on `CourseId`, `ProdCourseId` or `FriendlyUrl`, so the
CRUD endpoints enforce no uniqueness and there is no duplicate `409` on create or update. (The copy
endpoint is the one exception — see **Copy action** — and its check is an explicit `EXISTS`, not a
constraint violation being caught.)

---

## Foreign Keys

All three lookup endpoints already exist — `Partner`, `CourseGroup` and `PublishStatus` are built
features. List and detail resolve the FK into a nav object via JOIN; the form uses the lookup
endpoints for select options.

- **`Partner_pkid`** → `Partner.pkid`, NOT NULL
  - Aliased `PartnerPkid` in SELECT for the C# property.
  - Nav object label = `Name`; the lookup also carries `AppKey` so similarly-named brands can be
    told apart in the dropdown.
  - Dropdown order: `DisplayOrder ASC, Name ASC` (as `GET /api/lookups/partners` already returns).
  - `INNER JOIN` — the column is NOT NULL and the FK is enforced.

- **`CourseGroup_pkid`** → `CourseGroup.pkid`, **NULL allowed**
  - Aliased `CourseGroupPkid`.
  - Nav object label = `Description`.
  - Dropdown order: `Description ASC` (as `GET /api/lookups/course-groups` already returns —
    `CourseGroup.pkid` is an opaque IDENTITY, so alphabetical is the only useful ordering).
  - `LEFT JOIN`, and the form's `p-select` carries a `[showClear]` / 無 option.
  - List and detail render `—` when null.

- **`PublishStatus_pkid`** → `PublishStatus.pkid`, NOT NULL
  - Aliased `PublishStatusPkid`.
  - Nav object label = `Description`.
  - Dropdown order: `pkid ASC` — unlike `CourseGroup`, these keys are meaningful hand-assigned
    status codes that operators think in.
  - `INNER JOIN`.

**Nav objects, not a list-side `forkJoin`.** The `table.column` entries in the request
(`partner.name`, `courseGroup.description`, `publishStatus.description`) are resolved by JOINing in
SQL, matching CLAUDE.md's "nav objects for FKs" rule. sample1 described loading the three lookups on
the list page and mapping IDs to labels client-side; that is not done here — the list renders
`course.partner.name` straight from the payload and issues exactly one request.

---

## Foreign-Primary Links

Outbound navigation from a Course row to the referenced record's detail page. All three targets are
built, so these are real links rather than deferred counts.

| FK column | Target | Link |
|-----------|--------|------|
| `Partner_pkid` | 原廠 Partner | `/partners/{partnerPkid}` |
| `CourseGroup_pkid` | 課程群組 CourseGroup | `/course-groups/{courseGroupPkid}` — rendered only when not null |
| `PublishStatus_pkid` | 發布狀態 PublishStatus | `/publish-statuses/{publishStatusPkid}` |

Shown on the **detail page** as `routerLink` anchors on the three nav-object values. The **list**
renders the same three values as plain text: the row already carries three action buttons and a
copy button, and turning eight of fourteen columns into links makes the table unreadable. This is a
deliberate narrowing of sample1, which asked for links "in list, view, edit and insert pages" — the
form shows them as `p-select` controls, where a link would fight the control for the same click.

---

## Primary-Foreign Links

Six tables point at `Course`. Two of them are this entity's own junction tables and are handled
under **N-N Relationships**; the other four are child entities:

| Child table | FK column | Constraint | On delete | Feature built? |
|-------------|-----------|------------|-----------|----------------|
| `CourseFAQ` | `Course_pkid` int | `FK_CourseFAQ_Course` | NO ACTION | No |
| `CourseRelatedLink` | `Course_pkid` int | `FK_CourseRelatedLink_Course` | NO ACTION | No |
| `HotCourse` | `Course_pkid` int | `FK_HotCourse_Course` | NO ACTION | No |
| `CourseRecomm` | `CourseId` varchar(50) | **none** | — | No |
| `CourseInCertification` | `Course_pkid` int | `FK_CourseInCertification_Course` | **CASCADE** | junction — see N-N |
| `CourseJobCategories` | `Course_pkid` int | `FK_CourseJobCategories_Course` | **CASCADE** | junction — see N-N |

None of the four child features exists yet, so — following the `Partner`, `PublishStatus` and
`CourseGroup` precedent — all four are surfaced as **counts** projected as correlated subqueries in
`CourseSql.SelectBase`, not as link buttons. When `/course-faqs`, `/course-related-links`,
`/hot-courses` and `/course-recomms` are built, turn the counts into `pi pi-list` buttons targeting
`?coursePkid={pkid}` (and `?courseId={courseId}` for `CourseRecomm`, which joins on the string).

**`CourseRecomm` is the `Seminar.Partner_pkid` case again.** It stores `CourseId` and
`RecommCourseId` as `varchar(50)` with no `FOREIGN KEY` behind either — `Course.CourseId` carries no
unique index, so one could not be declared. The database would accept a delete and silently orphan
both sides of every recommendation pair. It is counted, and guarded, for exactly that reason. The
count is `COUNT(*)` over rows where **either** column matches, so a course that is only ever
*recommended by* others is still protected.

`ClassSection` — which sample1 lists as a child of Course — **does not exist in any of the four
schema files**. There is no 對應開課時間 column, no `pi pi-calendar` row button and no
`/class-sections` route in this feature. Do not add them back from sample1.

### Delete guard

The controller reads the record before deleting and returns `409` when
`CourseFaqCount + CourseRelatedLinkCount + HotCourseCount + CourseRecommCount > 0`. The same read
supplies the `404`.

The two junction counts are deliberately **not** part of the guard: those rows belong to this course
and are deleted with it. `DeleteAsync` removes them explicitly inside the transaction rather than
leaning on `ON DELETE CASCADE`, so the intent is visible at the call site and the behaviour does not
change if the constraint is ever redeclared.

---

## N-N Relationships

### CourseInCertification — Course ↔ Certification

Junction table `CourseInCertification` (`Course_pkid` int, `Certification_pkid` int); composite PK
over both columns, so a duplicate pair is impossible at the database level.

- **Related entity**: `Certification` (`pkid` int IDENTITY, `Partner_pkid` smallint NOT NULL,
  `Title` **nchar(100) NULL**).
- **Lookup**: `GET /api/lookups/certifications` — **new**. Returns `pkid`, `title`, `partnerPkid`,
  `partnerName`. `Title` is `nchar(100)`, so it **must** be `RTRIM()`ed; it is also nullable, so it
  coalesces to `''` and the frontend falls back to `認證 #{pkid}` for a blank one.
- **Ordering**: `Partner.DisplayOrder ASC, Partner.Name ASC, Certification.Title ASC` — certifications
  are only meaningful under their vendor, so the dropdown groups by partner the way an operator
  reads them.
- **Form**: `p-multiselect`, `[filter]="true"`, `[maxSelectedLabels]="9999"`, option label
  `原廠名稱 — 認證名稱`. Chips wrap via `::ng-deep`.
- **Detail**: the associated certification titles as `p-tag` chips, or `—` when empty.
- **Request field**: `CertificationPkids` — `List<int>`.

### CourseJobCategories — Course ↔ JobCategory

Junction table `CourseJobCategories` (`Course_pkid` int, `JobCategory_pkid` smallint); composite PK
over both columns.

- **Related entity**: `JobCategory` (`pkid` smallint IDENTITY, `Description` nvarchar(70) NOT NULL).
- **Lookup**: `GET /api/lookups/job-categories` — **new**. Returns `pkid`, `description`, ordered
  `Description ASC` (`pkid` is an opaque IDENTITY, same reasoning as `CourseGroup`).
- **Form**: `p-multiselect`, `[filter]="true"`, `[maxSelectedLabels]="9999"`.
- **Detail**: the associated descriptions as `p-tag` chips, or `—` when empty.
- **Request field**: `JobCategoryPkids` — `List<short>` (the FK column is `smallint`).

### Read and write

- **Read**: both lists are populated on `GET /api/courses/{id}` only — never on the list or query
  endpoints, where fourteen columns of scalar data are what the table shows and two extra round
  trips per row would be wasted. `Course.CertificationPkids` / `.JobCategoryPkids` come back empty
  from `QueryAsync`, and the model documents that.
- **Write**: delete-then-reinsert for both tables, inside the same transaction as the INSERT or
  UPDATE. A diff would be smaller but the tables have no surrogate key, no ordering column and no
  payload of their own, so there is nothing a diff would preserve.

Neither junction carries a `DisplayOrder`, so — unlike `spec/sample2.spec.md`'s SkillTrain — the
insert order is not meaningful and the multiselect's selection order is not persisted.

---

## Query Filters

`POST /api/courses/query` accepts:

- **keyword** — `string?`
  - `LIKE` on `Title`, `OfficialTitle`, `CourseId`, `ProdCourseId`, `FriendlyUrl`, as one
    parenthesised `OR` group.
  - The eight long-text columns (`Material`, `Objective`, `Target`, `Prerequisites`, `Outline`,
    `TowardCertOrExam`, `Note`, `OtherInfo` — four of them `nvarchar(4000)`, two `nvarchar(max)`)
    are **excluded**: scanning them is slow and an operator searching for a course searches by name
    or code. This is the template's explicit rule.
  - LIKE wildcards (`%`, `_`, `[`) are escaped so a literal keyword matches literally, reusing
    `PartnerSql.EscapeLike`'s implementation.

- **partnerPkid** — `short?`, exact match on `Partner_pkid`.
  Options from `GET /api/lookups/partners`, label `Name`, order `DisplayOrder ASC`.

- **courseGroupPkid** — `short?`, exact match on `CourseGroup_pkid`.
  Options from `GET /api/lookups/course-groups`, label `Description`, order `Description ASC`.
  The dropdown offers 全部 only — there is no "未分類 / IS NULL" option, because a nullable-FK
  three-state select would need a sentinel value the `short?` parameter cannot express. Noted here
  as a known gap rather than smuggled in as a magic `-1`.

- **publishStatusPkid** — `byte?`, exact match on `PublishStatus_pkid`.
  Options from `GET /api/lookups/publish-statuses`, label `Description`, order `pkid ASC`.

- **scheduleOnFrom / scheduleOnTo** — `DateOnly?` each.
  `c.ScheduleOn >= @ScheduleOnFrom` and `c.ScheduleOn <= @ScheduleOnTo`, each emitted independently
  so an open-ended range works. Both bounds inclusive.

- **scheduleOffFrom / scheduleOffTo** — `DateOnly?` each, same shape against `c.ScheduleOff`.

- **canRepeat** — `bool?` (tri-state: `null` = 全部, `true` = 允許, `false` = 不允許).
  Exact match on `c.CanRepeat`. **`false` is a real filter** — the falsy-check bug CLAUDE.md warns
  about applies here as much as to `hasImage` and `inUse`.

Four date parameters are bound as `DateOnly`, which the registered `DateOnlyTypeHandler` sends as
`DbType.Date` — so a `date` column comparison never picks up a spurious time component.

No filter on the four reference counts: unlike `CourseGroup`'s `inUse`, a course with no FAQs is
entirely ordinary and "show me unreferenced courses" is not a question anyone asks.

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/partners` | Exists | `PartnerLookup[]` — `pkid`, `name`, `appKey` |
| `GET /api/lookups/course-groups` | Exists | `CourseGroupLookup[]` — `pkid`, `description` |
| `GET /api/lookups/publish-statuses` | Exists | `PublishStatusLookup[]` — `pkid`, `description` |
| `GET /api/lookups/certifications` | **New** | `CertificationLookup[]` — `pkid`, `title` (RTRIMmed), `partnerPkid`, `partnerName` |
| `GET /api/lookups/job-categories` | **New** | `JobCategoryLookup[]` — `pkid`, `description` |

`Course` is itself an FK target for four unbuilt child features. A `GET /api/lookups/courses` is
**not** added — nothing consumes it, and the three lookups that shipped ahead of their consumers
(`partners`, `course-groups`, `publish-statuses`) each did so as part of *their own* entity's
feature. `Course`'s lookup belongs to whichever child feature first needs it.

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/courses` | List all, default sort |
| `POST` | `/api/courses/query` | Filtered query (body: `CourseQuery`; null body = unfiltered) |
| `GET` | `/api/courses/{id:int}` | Get by pkid, **including both n-n pkid lists**; `404` when missing |
| `POST` | `/api/courses` | Create; `201 CreatedAtAction`; writes both junctions |
| `PUT` | `/api/courses` | Update — **pkid from the body**, not the route; `404` when missing; rewrites both junctions |
| `DELETE` | `/api/courses/{id:int}` | `204`; `404` when missing; `409` when any child count is non-zero |
| `POST` | `/api/courses/{id:int}/copy` | Duplicate a course under a new `CourseId` — see below |

Route constraint is `{id:int}` and the action parameter is `int`, so the Angular service needs **no**
`encodeURIComponent` — that is the `AppRole` string-PK case only.

No auth attributes: the API has no authentication wired yet, matching all four existing controllers.

### Copy action

`POST /api/courses/{id:int}/copy`, body `CourseCopyRequest { string NewCourseId }`.

| Outcome | Response |
|---------|----------|
| Source course not found | `404 Not Found` |
| `NewCourseId` blank or > 50 chars | `400 Bad Request` (data annotations) |
| `NewCourseId` already used by any course | `409 Conflict` |
| Success | `201 Created` at `GetById`, body = the new `Course` |

Copies every scalar column except `pkid` (fresh IDENTITY) and `CourseId` (the supplied value), plus
both n-n relations. `FriendlyUrl` is copied verbatim — it has no unique constraint, and silently
mangling it would be worse than leaving an obvious duplicate for the operator to fix.

The whole copy runs in one transaction: the `EXISTS` check, the INSERT, and both junction
back-fills. Sample1 specified this endpoint returning `{ pkid }`; it returns the full created
`Course` instead, so the frontend can navigate and toast without a second fetch, and so the shape
matches `POST /api/courses`.

---

## Backend Notes

### Models

`Models/Course.cs` — response model. FK nav objects reuse the existing `*Lookup` types rather than
introducing parallel ones:

```csharp
public class Course
{
    public int Pkid { get; set; }                       // 主代碼
    public string Title { get; set; } = string.Empty;   // 課程名稱
    public string? OfficialTitle { get; set; }          // 官方課程名稱
    public string CourseId { get; set; } = string.Empty;      // 簡介代碼
    public string ProdCourseId { get; set; } = string.Empty;  // 科目代碼
    public string FriendlyUrl { get; set; } = string.Empty;   // 友善網址
    public int DisplayOrder { get; set; }               // 顯示順序
    public short PartnerPkid { get; set; }
    public short? CourseGroupPkid { get; set; }
    public byte PublishStatusPkid { get; set; }
    public DateOnly ScheduleOn { get; set; }            // 上架日期
    public DateOnly ScheduleOff { get; set; }           // 下架日期
    public short Hour { get; set; }                     // 時數
    public decimal ListPrice { get; set; }              // 定價
    public decimal LearningCredit { get; set; }         // 點數
    public string? Material { get; set; }
    public string? Objective { get; set; }
    public string? Target { get; set; }
    public string? Prerequisites { get; set; }
    public string? Outline { get; set; }
    public string? TowardCertOrExam { get; set; }
    public string? Note { get; set; }
    public string? OtherInfo { get; set; }
    public bool CanRepeat { get; set; }                 // 允許重聽

    // Nav objects — populated by the multi-map. CourseGroup is null when the FK is null.
    public PartnerLookup? Partner { get; set; }
    public CourseGroupLookup? CourseGroup { get; set; }
    public PublishStatusLookup? PublishStatus { get; set; }

    // Child reference counts — correlated subqueries; drive the delete guard.
    public int CourseFaqCount { get; set; }
    public int CourseRelatedLinkCount { get; set; }
    public int HotCourseCount { get; set; }
    public int CourseRecommCount { get; set; }

    // Populated on GET by pkid only; always empty from GetAll / Query.
    public List<int> CertificationPkids { get; set; } = [];
    public List<short> JobCategoryPkids { get; set; } = [];
}
```

`Models/CourseRequest.cs` — write DTO. FK **pkids only**, no nav objects; n-n as lists. `Pkid` is
ignored on create (IDENTITY) and carries the key on update.

```csharp
public class CourseRequest
{
    public int Pkid { get; set; }

    [Required(AllowEmptyStrings = false)] [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(300)] public string? OfficialTitle { get; set; }

    [Required(AllowEmptyStrings = false)] [StringLength(50)]
    public string CourseId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)] [StringLength(50)]
    public string ProdCourseId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)] [StringLength(100)]
    public string FriendlyUrl { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
    public short PartnerPkid { get; set; }
    public short? CourseGroupPkid { get; set; }
    public byte PublishStatusPkid { get; set; }
    public DateOnly ScheduleOn { get; set; }
    public DateOnly ScheduleOff { get; set; }

    [Range(0, short.MaxValue)] public short Hour { get; set; }
    [Range(0, 999999999)]      public decimal ListPrice { get; set; }
    [Range(0, 99999999.9)]     public decimal LearningCredit { get; set; }

    [StringLength(500)]  public string? Material { get; set; }
    [StringLength(4000)] public string? Objective { get; set; }
    [StringLength(500)]  public string? Target { get; set; }
    [StringLength(4000)] public string? Prerequisites { get; set; }
    public string? Outline { get; set; }            // nvarchar(max) — unbounded
    public string? TowardCertOrExam { get; set; }   // nvarchar(max) — unbounded
    [StringLength(4000)] public string? Note { get; set; }
    [StringLength(4000)] public string? OtherInfo { get; set; }

    public bool CanRepeat { get; set; }

    public List<int> CertificationPkids { get; set; } = [];
    public List<short> JobCategoryPkids { get; set; } = [];
}
```

`Models/CourseQuery.cs`:

```csharp
public class CourseQuery
{
    public string? Keyword { get; set; }
    public short? PartnerPkid { get; set; }
    public short? CourseGroupPkid { get; set; }
    public byte? PublishStatusPkid { get; set; }
    public DateOnly? ScheduleOnFrom { get; set; }
    public DateOnly? ScheduleOnTo { get; set; }
    public DateOnly? ScheduleOffFrom { get; set; }
    public DateOnly? ScheduleOffTo { get; set; }
    public bool? CanRepeat { get; set; }
}
```

`Models/CourseCopyRequest.cs`:

```csharp
public class CourseCopyRequest
{
    [Required(AllowEmptyStrings = false)] [StringLength(50)]
    public string NewCourseId { get; set; } = string.Empty;
}
```

`Models/CertificationLookup.cs` (`Pkid` int, `Title` string, `PartnerPkid` short,
`PartnerName` string) and `Models/JobCategoryLookup.cs` (`Pkid` short, `Description` string).

`ScheduleOff` is **not** validated as `>= ScheduleOn`. The schema does not constrain it and there is
no evidence the existing data respects it; a validator would reject rows the operator can already
see in the database.

### SQL — SELECT

`Repositories/CourseSql.cs` holds the projection plus `BuildWhere`, following `AppRoleSql` /
`PartnerSql` / `CourseGroupSql` so the filter logic is unit-testable without a database.

```sql
SELECT c.pkid AS Pkid, c.Title, c.OfficialTitle, c.CourseId, c.ProdCourseId, c.FriendlyUrl,
       c.DisplayOrder,
       c.Partner_pkid AS PartnerPkid,
       c.CourseGroup_pkid AS CourseGroupPkid,
       c.PublishStatus_pkid AS PublishStatusPkid,
       c.ScheduleOn, c.ScheduleOff, c.Hour, c.ListPrice, c.LearningCredit,
       c.Material, c.Objective, c.Target, c.Prerequisites, c.Outline,
       c.TowardCertOrExam, c.Note, c.OtherInfo, c.CanRepeat,
       (SELECT COUNT(*) FROM CourseFAQ f WHERE f.Course_pkid = c.pkid) AS CourseFaqCount,
       (SELECT COUNT(*) FROM CourseRelatedLink rl WHERE rl.Course_pkid = c.pkid) AS CourseRelatedLinkCount,
       (SELECT COUNT(*) FROM HotCourse hc WHERE hc.Course_pkid = c.pkid) AS HotCourseCount,
       (SELECT COUNT(*) FROM CourseRecomm cr
         WHERE cr.CourseId = c.CourseId OR cr.RecommCourseId = c.CourseId) AS CourseRecommCount,
       pt.pkid AS Pkid, pt.Name, pt.AppKey,
       cg.pkid AS Pkid, cg.Description,
       ps.pkid AS Pkid, ps.Description
FROM Course c
INNER JOIN Partner pt       ON pt.pkid = c.Partner_pkid
LEFT  JOIN CourseGroup cg   ON cg.pkid = c.CourseGroup_pkid
INNER JOIN PublishStatus ps ON ps.pkid = c.PublishStatus_pkid
```

`DefaultOrderBy` = `ORDER BY c.DisplayOrder ASC, c.pkid ASC` — `pkid` breaks the tie so rows sharing
a `DisplayOrder` still paginate deterministically, exactly as `PartnerSql` does.

**Multi-map**: `QueryAsync<Course, PartnerLookup, CourseGroupLookup, PublishStatusLookup, Course>`
with `splitOn: "Pkid,Pkid,Pkid"`. The three nav blocks are the last three column groups and each
opens with `pkid AS Pkid`, which is what the split boundary keys on.

**The `LEFT JOIN` needs an explicit null.** Dapper hands the map lambda a constructed
`CourseGroupLookup` even when every joined column is `NULL`, so the lambda assigns
`course.CourseGroup = course.CourseGroupPkid.HasValue ? courseGroup : null` rather than trusting the
mapper. Without that, a course with no group reports `{ pkid: 0, description: '' }` and the list
renders an empty cell instead of `—`.

**n-n reads** are two separate queries on the same connection, issued only by `GetByIdAsync`:

```sql
SELECT Certification_pkid FROM CourseInCertification WHERE Course_pkid = @Pkid ORDER BY Certification_pkid;
SELECT JobCategory_pkid   FROM CourseJobCategories   WHERE Course_pkid = @Pkid ORDER BY JobCategory_pkid;
```

`ORDER BY` is not required by the schema but keeps the payload — and therefore the tests — stable.

### SQL — INSERT

Every column except the IDENTITY key is writable:

```sql
INSERT INTO Course (Title, OfficialTitle, CourseId, ProdCourseId, FriendlyUrl, DisplayOrder,
    Partner_pkid, CourseGroup_pkid, PublishStatus_pkid, ScheduleOn, ScheduleOff, Hour,
    ListPrice, LearningCredit, Material, Objective, Target, Prerequisites, Outline,
    TowardCertOrExam, Note, OtherInfo, CanRepeat)
VALUES (@Title, @OfficialTitle, @CourseId, @ProdCourseId, @FriendlyUrl, @DisplayOrder,
    @PartnerPkid, @CourseGroupPkid, @PublishStatusPkid, @ScheduleOn, @ScheduleOff, @Hour,
    @ListPrice, @LearningCredit, @Material, @Objective, @Target, @Prerequisites, @Outline,
    @TowardCertOrExam, @Note, @OtherInfo, @CanRepeat);
SELECT CAST(SCOPE_IDENTITY() AS int);
```

`SCOPE_IDENTITY()` returns `decimal`, so the `CAST` is required for `ExecuteScalarAsync<int>` — the
same cast `PartnerRepository` and `CourseGroupRepository` need.

The INSERT and both junction back-fills share one transaction: a course that half-saves its
certifications is worse than one that fails outright.

### SQL — UPDATE

Same column list, `pkid` in the WHERE only (it is the key and is immutable):

```sql
UPDATE Course
SET Title = @Title, OfficialTitle = @OfficialTitle, CourseId = @CourseId,
    ProdCourseId = @ProdCourseId, FriendlyUrl = @FriendlyUrl, DisplayOrder = @DisplayOrder,
    Partner_pkid = @PartnerPkid, CourseGroup_pkid = @CourseGroupPkid,
    PublishStatus_pkid = @PublishStatusPkid, ScheduleOn = @ScheduleOn, ScheduleOff = @ScheduleOff,
    Hour = @Hour, ListPrice = @ListPrice, LearningCredit = @LearningCredit,
    Material = @Material, Objective = @Objective, Target = @Target,
    Prerequisites = @Prerequisites, Outline = @Outline, TowardCertOrExam = @TowardCertOrExam,
    Note = @Note, OtherInfo = @OtherInfo, CanRepeat = @CanRepeat
WHERE pkid = @Pkid;
```

`UpdateAsync` returns `false` on zero rows affected, which the controller turns into `404`. The
junction rewrite is skipped in that case — there is nothing to attach them to.

`CourseId` **is** updatable, and `FK_CourseInCertification_Course` / `FK_CourseJobCategories_Course`
are `ON UPDATE CASCADE` — but they cascade on `Course.pkid`, which never changes, so the cascade
never fires. `CourseRecomm` joins on `CourseId` with no constraint at all, so renaming a course's
`CourseId` silently breaks its recommendation rows. That is pre-existing schema behaviour, out of
scope for this feature, and worth a note when `CourseRecomm` is built.

### SQL — DELETE

```sql
DELETE FROM CourseInCertification WHERE Course_pkid = @Pkid;
DELETE FROM CourseJobCategories   WHERE Course_pkid = @Pkid;
DELETE FROM Course                WHERE pkid = @Pkid;
```

All three in one transaction. The junction deletes are explicit rather than left to
`ON DELETE CASCADE` so the intent is visible and the behaviour survives a constraint change. The
controller has already returned `409` if any of the four child counts is non-zero, so
`FK_CourseFAQ_Course`, `FK_CourseRelatedLink_Course` and `FK_HotCourse_Course` never get the chance
to raise SQL error 547, and `CourseRecomm` — which has no constraint and would simply be orphaned —
is caught by the same guard.

### N-N Sync Pattern

Identical for both junctions, run inside the caller's transaction after INSERT / UPDATE:

```sql
DELETE FROM CourseInCertification WHERE Course_pkid = @Pkid;
-- then, when the list is non-empty:
INSERT INTO CourseInCertification (Course_pkid, Certification_pkid) VALUES (@Pkid, @RelatedPkid);
```

Dapper executes the INSERT once per list element from a single `ExecuteAsync` with an
`IEnumerable` parameter. The lists are `.Distinct()`ed first — the composite PK would reject a
duplicate pair and take the whole transaction down with it.

### Special Column Notes

- **`Certification.Title` is `nchar(100)`** — `RTRIM()` in the lookup's SELECT, per CLAUDE.md. It is
  also nullable, so the projection is `RTRIM(ISNULL(ct.Title, ''))`. No column on `Course` itself is
  `nchar`.
- **`ScheduleOn` / `ScheduleOff` are `date`** → `DateOnly` via `DateOnlyTypeHandler`, already
  registered in `Program.cs`. This is the **first feature to actually exercise those handlers**; the
  handler sets `DbType.Date`, so no time component leaks into a comparison.
- **`Hour` is `smallint`** → `short`, not `int`.
- **`ListPrice` is `decimal(9,0)`** — zero scale, i.e. whole NT dollars. Displayed with no decimal
  places and entered with `[maxFractionDigits]="0"`. **`LearningCredit` is `decimal(9,1)`** — one
  decimal place, `[minFractionDigits]="1" [maxFractionDigits]="1"`. The two are adjacent in the list
  and easy to conflate; the scales are different and both matter.
- **`Partner_pkid` is `smallint`, `PublishStatus_pkid` is `tinyint`** → `short` and `byte`.
  `CourseGroup_pkid` is `smallint NULL` → `short?`.
- **`Outline` and `TowardCertOrExam` are `nvarchar(max)`** — no `StringLength`, `p-textarea` with a
  generous row count, and excluded from the keyword search.
- **`OtherInfo` was added by a later `ALTER TABLE`**, so it sits at the end of the physical column
  order. It is an ordinary `nvarchar(4000)` and is treated like `Note`.
- **`CourseId` / `ProdCourseId` / `FriendlyUrl` are `varchar`, not `nvarchar`** — ASCII codes.
  Nothing special is needed for Dapper, but the form marks them `maxlength` at their byte lengths
  and they are the columns the keyword search is really for.

---

## Frontend Notes

### Routes

| Path | Component |
|------|-----------|
| `courses` | `CourseList` |
| `courses/new` | `CourseForm` |
| `courses/:id` | `CourseDetail` |
| `courses/:id/edit` | `CourseForm` |

Lazy `loadComponent` in `app.routes.ts`, `/new` **before** `/:id`, all four ahead of the `**`
wildcard.

### Angular Model — `core/models/course.model.ts`

```ts
import { PartnerLookup } from '@core/models/partner-lookup.model';
import { CourseGroupLookup } from '@core/models/course-group-lookup.model';
import { PublishStatusLookup } from '@core/models/publish-status-lookup.model';

export interface Course {
  pkid: number;
  title: string;
  officialTitle: string | null;
  courseId: string;
  prodCourseId: string;
  friendlyUrl: string;
  displayOrder: number;
  partnerPkid: number;
  courseGroupPkid: number | null;
  publishStatusPkid: number;
  scheduleOn: string;          // ISO yyyy-MM-dd — DateOnly serializes without a time part
  scheduleOff: string;
  hour: number;
  listPrice: number;
  learningCredit: number;
  material: string | null;
  objective: string | null;
  target: string | null;
  prerequisites: string | null;
  outline: string | null;
  towardCertOrExam: string | null;
  note: string | null;
  otherInfo: string | null;
  canRepeat: boolean;
  partner: PartnerLookup | null;
  courseGroup: CourseGroupLookup | null;
  publishStatus: PublishStatusLookup | null;
  courseFaqCount: number;
  courseRelatedLinkCount: number;
  hotCourseCount: number;
  courseRecommCount: number;
  certificationPkids: number[];
  jobCategoryPkids: number[];
}

export interface CourseRequest { /* scalars + the two pkid arrays; no nav objects */ }
export interface CourseQuery {
  keyword?: string | null;
  partnerPkid?: number | null;
  courseGroupPkid?: number | null;
  publishStatusPkid?: number | null;
  scheduleOnFrom?: string | null;
  scheduleOnTo?: string | null;
  scheduleOffFrom?: string | null;
  scheduleOffTo?: string | null;
  canRepeat?: boolean | null;
}
export interface CourseCopyRequest { newCourseId: string }
```

Plus `core/models/certification-lookup.model.ts` and `core/models/job-category-lookup.model.ts`.

### Service — `core/services/course.service.ts`

The standard six against `${environment.apiUrl}/courses`, plus
`copy(pkid: number, request: CourseCopyRequest)` → `POST /courses/{pkid}/copy`. The key is numeric,
so **no `encodeURIComponent`**. `update()` PUTs to the collection route with the key in the body.

`LookupService` gains `getCertifications()` and `getJobCategories()`.

### List component

Fourteen data columns plus 操作, exactly as requested:

| # | Column | Field | Notes |
|---|--------|-------|-------|
| 1 | 主代碼 | `pkid` | sortable, 6rem |
| 2 | 顯示順序 | `displayOrder` | sortable, right-aligned |
| 3 | 簡介代碼 | `courseId` | sortable |
| 4 | 科目代碼 | `prodCourseId` | sortable |
| 5 | 課程名稱 | `title` | sortable, widest column |
| 6 | 原廠 | `partner.name` | sortable on `partner.name` |
| 7 | 課程群組 | `courseGroup.description` | `—` when null |
| 8 | 上架狀態 | `publishStatus.description` | |
| 9 | 上架日期 | `scheduleOn` | `\| date:'yyyy/MM/dd'` |
| 10 | 下架日期 | `scheduleOff` | `\| date:'yyyy/MM/dd'` |
| 11 | 時數 | `hour` | right-aligned |
| 12 | 定價 | `listPrice` | right-aligned, `\| number:'1.0-0'` |
| 13 | 點數 | `learningCredit` | right-aligned, `\| number:'1.1-1'` |
| 14 | 允許重聽 | `canRepeat` | `p-tag` 是／否 |
| 15 | 操作 | — | 檢視 / 編輯 / 複製 / 刪除 |

The table needs horizontal scrolling at this width: `[scrollable]="true"`, `scrollHeight="flex"`,
and explicit `style="width: …"` on the narrow columns so the wide ones absorb the slack.

PrimeNG sorts `pSortableColumn="partner.name"` through a dotted field path natively, so the nav
object needs no flattening.

Sortable, paginated `p-table`, `dataKey="pkid"`, default sort `{ field: 'displayOrder', order: 1 }`,
default page `{ first: 0, rows: 20 }`, paginator on top, `rowsPerPageOptions [10, 20, 50, 100]`.

**Filter drawer** (`p-drawer`, `position="right"`), in order: 關鍵字 `input pInputText`; 原廠,
課程群組 and 上架狀態 `p-select` (each `appendTo="body"`, each `[filter]="true"` — partner and course
group lists will exceed ten options); 上架日期 from/to and 下架日期 from/to as four `p-datepicker`
(`appendTo="body"`, `dateFormat="yy/mm/dd"`, `[showButtonBar]="true"`); 允許重聽 tri-state `p-select`
(全部 / 允許 / 不允許 → `null` / `true` / `false`).

The 搜尋條件 `[badge]` counts applied filters as a **number**: keyword when non-blank, each of the
three FK selects when not null, each of the four date bounds when set, and `canRepeat` whenever it
is neither `null` nor `undefined` — a `false` (不允許) selection is a real filter and a falsy check
would silently drop it. Nine possible filters, so the badge can read up to `9`.

**複製 button** (per row, `pi pi-copy`): opens a `p-dialog` showing the source
`courseId — title` and a required 新簡介代碼 input, then `POST /courses/{pkid}/copy`. On success,
toast and navigate to the new course's detail page. On `409`, keep the dialog open and show
「簡介代碼已存在。」 under the input.

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `course-list-filters` | Applied `CourseQuery` |
| `course-list-sort` | `{ field, order }` |
| `course-list-page` | `{ first, rows }` |

**Incoming query params override saved filter state.** `?partnerPkid=`, `?courseGroupPkid=` and
`?publishStatusPkid=` are each read from the route on init and, when present, replace that one
saved filter (leaving the others intact) before the first query. This is what lets the existing
`Partner`, `CourseGroup` and `PublishStatus` features turn their 課程數 counts into links — the
first cross-entity navigation in the app. Converting those three count columns into link buttons is
follow-up work in those features, not part of this one; the receiving end ships here so it is ready.

### Lookup Binding in List

**N/A for display** — the nav objects arrive with the payload. The list still `forkJoin`s the three
FK lookups on init, but only to populate the drawer's three `p-select` controls; the saved filters
are restored *after* that `forkJoin` resolves so a restored `partnerPkid` has a label to render.

Each lookup carries its own `catchError` rather than relying on one error branch on the `forkJoin`:
`forkJoin` cancels its remaining sources the moment one errors, so a single failed lookup would
leave the other two dropdowns empty even though their requests would have succeeded. A flag set
inside the `catchError` raises one 載入失敗 toast, and the list query runs either way.

### Date Handling

`Course` is the first entity with date columns, so `core/utils/date.util.ts` is created here:

- `toIso(d: Date | null): string | null` — builds `yyyy-MM-dd` from **local** components
  (`d.getFullYear()`, `d.getMonth() + 1`, `d.getDate()`). Never `d.toISOString().split('T')[0]`,
  which converts to UTC first and lands a UTC+8 operator on the previous day.
- `fromIso(s: string | null): Date | null` — parses `yyyy-MM-dd` into a local `Date` by passing the
  three components to the constructor, for the same reason in reverse.
- `addYears(d: Date, years: number): Date`.

The `'Z'`-suffix fix in the template does **not** apply: `ScheduleOn` / `ScheduleOff` are `date`,
not `datetime`, and `DateOnly` serializes as a bare `yyyy-MM-dd` with no time or zone to
misinterpret. `{{ course.scheduleOn | date:'yyyy/MM/dd' }}` on that string is already correct.

### Special Form Behaviors

- **`ScheduleOff` auto-defaults to `ScheduleOn + 10 years`.** A `valueChanges` subscription on
  `scheduleOn` sets `scheduleOff` via `addYears(value, 10)`, guarded on `value instanceof Date` and
  written with `{ emitEvent: false }` so it cannot loop. In edit mode the loaded record's
  `scheduleOff` is patched **after** `scheduleOn` in the same `patchValue`, so the stored value wins
  over the subscription that the `scheduleOn` patch just fired.
- **`courseGroup` is clearable** (`[showClear]="true"`); an empty selection saves as `null`, not `0`.
- **Optional text fields normalise blank to `null`** on save, so the database does not accumulate a
  mix of `''` and `NULL` in the ten nullable columns.
- **The key is never an editable control**: add mode has no key field (IDENTITY supplies it), edit
  mode renders 主代碼 as static text beside the heading — the `PartnerForm` pattern. The
  `disable()` / `getRawValue()` dance belongs to `AppRole` and `PublishStatus`, whose keys are
  client-supplied.
- Both `p-multiselect`s start empty in add mode and are patched from `certificationPkids` /
  `jobCategoryPkids` in edit mode, after the `forkJoin` of five lookups + the record resolves.

### Form layout

Three cards. The long-text fields are separated out so the operator is not scrolling past four
textareas to reach 定價.

**基本資料**

| Field | Widget | Notes |
|-------|--------|-------|
| 主代碼 | static text | edit mode only |
| 課程名稱 Title | `input pInputText` `maxlength="200"` | required |
| 官方課程名稱 OfficialTitle | `input pInputText` `maxlength="300"` | |
| 簡介代碼 CourseId | `input pInputText` `maxlength="50"` | required |
| 科目代碼 ProdCourseId | `input pInputText` `maxlength="50"` | required |
| 友善網址 FriendlyUrl | `input pInputText` `maxlength="100"` | required |
| 顯示順序 DisplayOrder | `p-inputNumber` `[useGrouping]="false"` | required |

**分類與排程**

| Field | Widget | Notes |
|-------|--------|-------|
| 原廠 Partner | `p-select` `[filter]="true"` | required |
| 課程群組 CourseGroup | `p-select` `[filter]="true" [showClear]="true"` | optional |
| 上架狀態 PublishStatus | `p-select` | required |
| 上架日期 ScheduleOn | `p-datepicker` `dateFormat="yy/mm/dd"` | required |
| 下架日期 ScheduleOff | `p-datepicker` | required; auto-defaults |
| 時數 Hour | `p-inputNumber` `[min]="0"` | required |
| 定價 ListPrice | `p-inputNumber` `[maxFractionDigits]="0" [min]="0"` | required |
| 點數 LearningCredit | `p-inputNumber` `[minFractionDigits]="1" [maxFractionDigits]="1" [min]="0"` | required |
| 允許重聽 CanRepeat | `p-toggleswitch` | |
| 對應認證 Certifications | `p-multiselect` `[filter]="true" [maxSelectedLabels]="9999"` | n-n |
| 對應職務類別 JobCategories | `p-multiselect` `[filter]="true" [maxSelectedLabels]="9999"` | n-n |

**課程內容**

| Field | Widget | Notes |
|-------|--------|-------|
| 教材 Material | `textarea pTextarea` `maxlength="500"` `[rows]="2"` | |
| 課程目標 Objective | `textarea pTextarea` `maxlength="4000"` `[rows]="4"` | |
| 適合對象 Target | `textarea pTextarea` `maxlength="500"` `[rows]="2"` | |
| 先備知識 Prerequisites | `textarea pTextarea` `maxlength="4000"` `[rows]="4"` | |
| 課程大綱 Outline | `textarea pTextarea` `[rows]="8"` | nvarchar(max) — no maxlength |
| 考試／認證說明 TowardCertOrExam | `textarea pTextarea` `[rows]="4"` | nvarchar(max) — no maxlength |
| 備註 Note | `textarea pTextarea` `maxlength="4000"` `[rows]="3"` | |
| 其他資訊 OtherInfo | `textarea pTextarea` `maxlength="4000"` `[rows]="3"` | |

Reactive Forms throughout, with a `forkJoin` of the five lookups (and, in edit mode, the record) on
init — the first feature where the `forkJoin` CLAUDE.md describes actually has parallel work to do.

### Detail page

Four sections: **基本資料**, **分類與排程** (with the three FK values as `routerLink` anchors to
their detail pages), **課程內容** (the eight long-text fields, `white-space: pre-wrap`, `—` when
empty), and **使用狀況** (the four child counts, with 「此課程仍被引用，無法刪除。」 when any is
non-zero). The two n-n sets render as `p-tag` chips under 分類與排程.

No 列印PDF button — see **Deviations**.

#### QR Code

A `QR Code` field at the end of **基本資料**, rendered as a `<figure class="qr-code">`:

| Part | Content |
|------|---------|
| Title (`figcaption`) | `Course.CourseId` |
| Image | the QR, an `<img>` fed a `image/png` data URL |
| Caption link | the encoded URL, `target="_blank" rel="noopener"` |
| Action | `下載 QR Code` — `p-button` `size="small"` `severity="secondary"` |

Encoded URL: `https://www.uuu.com.tw/Course/Show/{Course.pkid}/{Course.CourseId}`. Both values come
from the loaded **record**, not from the route param. `CourseId` is operator-entered `varchar(50)`
under no unique constraint, so it is `trim()`ed and `encodeURIComponent`d rather than dropped into
the path as-is.

`core/utils/qr-code.util.ts` holds the generic half — `renderQrToCanvas`, `qrPngDataUrl` and
`downloadDataUrl`. It wraps `qrcode-generator` (zero runtime dependencies, ships its own `.d.ts` and
an ESM build, so no `allowedCommonJsDependencies` entry is needed). The library only yields the
module matrix and a GIF data URL, so the util draws the matrix onto a canvas itself — error
correction `M`, type number `0` (smallest symbol that fits), a 4-module quiet zone, 6px cells — and
takes the PNG off that canvas. The same data URL serves the `<img>` and the download, so the file
the operator saves is exactly the code on screen. Download is an `<a download>` click named
`course-{CourseId}-qrcode.png`; the image is the QR alone, with no caption drawn into it.

The whole block is inside `@if (qrImage(); as image)`, so the not-found state renders `—`.

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.courseId} ${item.title}」？
```

Appended when any child count is non-zero:

```
<br>此課程仍被 ${courseFaqCount} 筆課程問答、${courseRelatedLinkCount} 筆相關連結、
${hotCourseCount} 筆熱門課程與 ${courseRecommCount} 筆推薦課程使用，將無法刪除。
```

A `409` surfaces the toast 「此課程仍被其他資料使用，無法刪除。」

`p-confirmDialog` has **no `escape` input** in PrimeNG 20 — it always renders `message` through
`[innerHTML]`, so the `<b>` works as written and both `courseId` and `title` are passed through the
same `escapeHtml` helper the other list components carry.

### Sub-panels (edit mode only)

**N/A.** sample1 lists 課程相關連結 and 推薦課程 inline-edit sub-panels citing
`spec/course/CourseRelatedLink.md` and `spec/course/CourseRecomm.md`. Neither spec file exists,
neither entity is built, and there is no `spec/inline-edit.md` to define the cell pattern. Both are
substantial child features in their own right.

### Sidebar placement

Existing group `課程管理 Course` in `app.ts` `navGroups`, appended after `課程群組 CourseGroup`:

```ts
{ label: '課程 Course', icon: 'pi pi-book', route: '/courses' }
```

`expandedGroups` still defaults to `系統管理 Admin` alone, so `app.spec.ts` must
`toggleGroup('課程管理 Course')` before asserting on the new item — the existing Course-group case
already does exactly that and simply grows by one entry.

---

## Tests

### Backend — `src/CMS.API.Tests/`

Hand-written fakes, not a mocking library (house rule).

- `Fakes/FakeCourseRepository.cs` — in-memory `ICourseRepository` mirroring the real contract:
  every filter, `DisplayOrder ASC, pkid ASC` ordering, IDENTITY key assignment, nav objects
  populated from seeded lookups (and `CourseGroup` left null when the FK is null), n-n lists
  returned by `GetByIdAsync` but empty from `QueryAsync`, settable child counts so the delete-409
  path is reachable, and a `CopyAsync` honouring the duplicate-`CourseId` conflict.
- `Fakes/FakeLookupRepository.cs` — extended with `GetCertificationsAsync` / `GetJobCategoriesAsync`.
- `Controllers/CoursesControllerTests.cs` — list ordering and nav objects; query across each filter
  individually, in combination, with a null body, with no match, and with **`canRepeat = false`**;
  open-ended date ranges; get-by-id found / not-found and the n-n lists only appearing there;
  create `201` + route value + IDENTITY key + junction contents; update `200` with key-from-body and
  a rewritten junction set; update missing `404`; delete `204`; delete missing `404`; and four
  separate delete-`409` cases, one per child table — including the `CourseRecomm` arm, which has no
  FK constraint and would otherwise orphan silently.
- `Controllers/CoursesControllerCopyTests.cs` — copy success (`201`, new pkid, `CourseId` replaced,
  every other scalar and both n-n sets carried over), duplicate `CourseId` `409`, missing source
  `404`.
- `Repositories/CourseSqlTests.cs` — `BuildWhere` for every filter and combination; keyword trimming
  and LIKE escaping across all five searched columns; the eight long-text columns **absent** from
  the keyword clause; `canRepeat = false` emitting a clause rather than being skipped as falsy;
  each date bound emitted independently; the projection containing all four count subqueries, the
  three JOINs (one of them `LEFT`), the three aliased FK columns and the three nav-object blocks;
  and the default ORDER BY.
- `Controllers/LookupsControllerTests.cs` — cases for the two new routes.

### Frontend — `src/CMS.NG/`

- `core/services/course.service.spec.ts` — each method's URL and verb, the numeric key
  interpolating without encoding, PUT carrying the key in the body, and `copy()` POSTing to
  `/courses/{id}/copy`.
- `core/services/lookup.service.spec.ts` — cases for `getCertifications()` / `getJobCategories()`.
- `core/utils/date.util.spec.ts` — `toIso` returning the **local** date for a `Date` built in a
  UTC+8 evening (the case `toISOString()` gets wrong), `fromIso` round-tripping it, and `addYears`
  across a leap day.
- `features/courses/course-list/course-list.spec.ts` — loads on init after the lookup `forkJoin`;
  renders all fourteen columns including the three nav-object values and `—` for a null course
  group; applies and clears drawer filters; the badge counts `canRepeat = false`; persists and
  restores session state; an incoming `?partnerPkid=` overriding only that saved filter; query
  failure; add/view/edit navigation; the copy dialog's success and `409` paths; delete on confirm;
  and the 409 toast.
- `.../course-detail/course-detail.spec.ts` — loads the record, renders the nav-object links with
  the right hrefs, omits the course-group link when null, renders both n-n chip sets and the four
  counts, `404` path, no-id path, back/edit navigation; and the QR code — the URL built from the
  record's `pkid`/`CourseId` rather than the route param, surrounding whitespace trimmed out of it,
  the `<img src>` matching `qrPngDataUrl` of that URL, `CourseId` shown as the title inside 基本資料,
  the download producing PNG bytes under the `course-{CourseId}-qrcode.png` name, and no figure at
  all on the `404` path.
- `.../core/utils/qr-code.util.spec.ts` — canvas sized to module count plus both quiet zones; every
  drawn module sampled back out of `getImageData` and compared against an independently built
  `qrcode(0, 'M')` matrix, which is what proves the image really encodes the text; the quiet zone
  left light; the data URL carrying the PNG magic bytes; different text producing different images;
  and `downloadDataUrl` clicking an anchor with the right `href` and `download`.
- `.../course-form/course-form.spec.ts` — add mode (required-field validation across all thirteen
  required fields, POST body shape, blank optionals normalised to `null`, dates serialised as local
  `yyyy-MM-dd`, both pkid arrays sent); edit mode (主代碼 shown as text, PUT includes the key in the
  body, the loaded `scheduleOff` surviving the auto-default subscription, multiselects patched from
  the record); and the `ScheduleOn` → `ScheduleOff + 10 years` behaviour in add mode.
- `app.spec.ts` — 課程管理 Course now renders three items.

---

## Files to Create / Modify

### Backend

| File | Action |
|------|--------|
| `src/CMS.API/Models/Course.cs` | Create |
| `src/CMS.API/Models/CourseRequest.cs` | Create |
| `src/CMS.API/Models/CourseQuery.cs` | Create |
| `src/CMS.API/Models/CourseCopyRequest.cs` | Create |
| `src/CMS.API/Models/CertificationLookup.cs` | Create |
| `src/CMS.API/Models/JobCategoryLookup.cs` | Create |
| `src/CMS.API/Repositories/CourseSql.cs` | Create |
| `src/CMS.API/Repositories/ICourseRepository.cs` | Create |
| `src/CMS.API/Repositories/CourseRepository.cs` | Create |
| `src/CMS.API/Controllers/CoursesController.cs` | Create |
| `src/CMS.API/Repositories/ILookupRepository.cs` | Modify — two new methods |
| `src/CMS.API/Repositories/LookupRepository.cs` | Modify — implement them |
| `src/CMS.API/Controllers/LookupsController.cs` | Modify — two new routes |
| `src/CMS.API/Program.cs` | Modify — register `ICourseRepository` |

### Frontend

| File | Action |
|------|--------|
| `src/CMS.NG/src/app/core/models/course.model.ts` | Create |
| `src/CMS.NG/src/app/core/models/certification-lookup.model.ts` | Create |
| `src/CMS.NG/src/app/core/models/job-category-lookup.model.ts` | Create |
| `src/CMS.NG/src/app/core/services/course.service.ts` | Create |
| `src/CMS.NG/src/app/core/utils/date.util.ts` | Create |
| `src/CMS.NG/src/app/core/utils/qr-code.util.ts` | Create |
| `src/CMS.NG/package.json` | Modify — add `qrcode-generator` |
| `src/CMS.NG/src/app/features/courses/course-list/*` (ts/html/scss) | Create |
| `src/CMS.NG/src/app/features/courses/course-detail/*` | Create |
| `src/CMS.NG/src/app/features/courses/course-form/*` | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.ts` | Modify — two getters |
| `src/CMS.NG/src/app/app.routes.ts` | Modify — four lazy routes |
| `src/CMS.NG/src/app/app.ts` | Modify — nav item under 課程管理 Course |

### Tests

| File | Action |
|------|--------|
| `src/CMS.API.Tests/Fakes/FakeCourseRepository.cs` | Create |
| `src/CMS.API.Tests/Controllers/CoursesControllerTests.cs` | Create |
| `src/CMS.API.Tests/Controllers/CoursesControllerCopyTests.cs` | Create |
| `src/CMS.API.Tests/Repositories/CourseSqlTests.cs` | Create |
| `src/CMS.API.Tests/Fakes/FakeLookupRepository.cs` | Modify |
| `src/CMS.API.Tests/Controllers/LookupsControllerTests.cs` | Modify |
| `src/CMS.NG/.../course.service.spec.ts` | Create |
| `src/CMS.NG/.../date.util.spec.ts` | Create |
| `src/CMS.NG/.../qr-code.util.spec.ts` | Create |
| `src/CMS.NG/.../course-list.spec.ts` | Create |
| `src/CMS.NG/.../course-detail.spec.ts` | Create |
| `src/CMS.NG/.../course-form.spec.ts` | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.spec.ts` | Modify |
| `src/CMS.NG/src/app/app.spec.ts` | Modify |

---

## Deviations

### From the `/crud` skill template

- **No `RowAuditWriter` injection and no `RowAuditBadgeComponent`.** The `RowAudit` table exists in
  `database/admin.sql`, but there is no writer, no badge component and no authentication to source a
  `UserName` from, and none of the four built repositories writes audit rows. Same call as
  `spec/admin/PublishStatus.md`, `spec/course/Partner.md` and `spec/course/CourseGroup.md`:
  follow-up work, not a one-entity cross-cutting invention.
- **No mocking library.** CLAUDE.md mandates hand-written fakes in `src/CMS.API.Tests/Fakes/`; the
  skill's suggestion of Moq is not followed.
- **No sticky `p-toolbar`.** The existing pages use a `.page-header` action bar; this feature
  matches them rather than introducing a second header pattern.

### From `spec/sample1.spec.md`

sample1 is the Course worked example, and it describes a more finished system than the one that
exists. Where it and this spec differ:

- **Column labels** follow the display-name hints given with this `/crud` invocation:
  `CourseId` 簡介代碼 (not 課程代碼), `ProdCourseId` 科目代碼 (not 產品代碼), `PublishStatus` 上架狀態
  (not 發布狀態), `LearningCredit` 點數 (not 學習學分), `CanRepeat` 允許重聽 (not 可重複上課),
  `Partner` 原廠 (not 合作廠商 — 原廠 is what the built Partner feature calls itself).
- **Default sort is `DisplayOrder ASC, pkid ASC`, not `CourseId ASC`.** `DisplayOrder` is NOT NULL,
  is the second column requested for the list, and is what `PartnerSql` already sorts by; the skill
  template also states the `DisplayOrder ASC` preference. Change it in one place (`CourseSql.
  DefaultOrderBy`, plus the list's default sort signal) if `CourseId` is genuinely wanted.
- **FK labels come from JOINed nav objects, not a client-side lookup map.** sample1 had the list
  `forkJoin` the three lookups and resolve IDs to labels in the component. CLAUDE.md's model
  convention and the `table.column` notation in the request both call for the JOIN.
- **`ClassSection` is dropped entirely** — no such table exists in `database/*.sql`.
- **Primary-Foreign navigation is counts, not link buttons**, for all four child tables, because
  none of those features is built. sample1 specified 查看 buttons to routes that would 404.
- **No inline sub-panels** (課程相關連結, 推薦課程) — the specs they reference don't exist.
- **No 列印PDF.** sample1 defers it to "`spec/course/Course.md` for details" — i.e. to this file —
  but no print stylesheet exists and it is not CRUD. The QR code sample1 defers the same way **is**
  built — see **Detail page → QR Code**; it added `qrcode-generator` to `package.json`.
- **Copy returns the full created `Course`**, not `{ pkid }`, matching `POST /api/courses`.
- **`date.util.ts` is created here**, not assumed to exist; sample1 refers to it as if it did.
- **`spec/dapper.md` does not exist** either; the type handlers are in
  `src/CMS.API/Data/DapperTypeHandlers.cs` and are already registered in `Program.cs`.
