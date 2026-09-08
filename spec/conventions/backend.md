# Backend conventions (`src/CMS.API`)

Read before touching anything under `src/CMS.API/`. The short version lives in `CLAUDE.md`; this
file is the detail.

## Per-entity file set

```
Models/{Table}.cs         # response  (nav objects for FKs, subquery counts for children)
Models/{Table}Request.cs  # write DTO (FK pkids only; n-n as List<T>)
Models/{Table}Query.cs    # search DTO
Repositories/{Table}Sql.cs         # projection + BuildWhere  <- keep filter logic HERE
Repositories/I{Table}Repository.cs + {Table}Repository.cs
Controllers/{TablePlural}Controller.cs   # route /api/{table-plural}
```

Register the repository in `Program.cs` alongside the others.

## Core rules

- **Dapper only.** No EF, ever.
- Filter logic goes in a static `{Table}Sql.BuildWhere` returning `(where, parameters)`. This is
  what makes query filters unit-testable without a database. Follow `AppRoleSql` as the model, and
  escape LIKE wildcards (`[`, `%`, `_`) in keyword parameters.
- `PUT` takes the key **from the body**, not a route param.
- `nchar(n)` columns need `RTRIM()` in every SELECT. `date` / `time(7)` map to `DateOnly` /
  `TimeOnly` via the handlers already registered in `Program.cs`.
- n-n writes are delete-then-reinsert inside a transaction; n-n reads are a separate query on the
  same connection.
- FK nav objects come from Dapper multi-map. Append each nav block to the end of the projection
  opening with `pkid AS Pkid`, and key `splitOn` on that. `CourseSql` (three nav objects, one of
  them a `LEFT JOIN` for the nullable FK) and `FeaturedPromoItemSql` (two, both `INNER`) are the
  worked examples.
- Lookup endpoints for select options live in `LookupsController` at `/api/lookups/{plural}`.
  Current set: `app-users`, `app-roles`, `publish-statuses`, `partners`, `course-groups`,
  `certifications`, `job-categories`, `training-centers`, `promotions?keyword=` (autocomplete) and
  `promotions/{promoCode}` (exact match, `404` when unknown).

## Primary keys — check the schema, never assume

`pkid int IDENTITY` is the common case, but two entities already break it in different ways. Read
the `CREATE TABLE` before writing anything.

**String PK — `AppRole`.** The PK is `RoleId` (nvarchar); `pkid` is a non-key IDENTITY column
displayed as 主代碼.

- Controller route is `{id}` with **no** `:int` constraint.
- The Angular service must `encodeURIComponent(id)`.

**Non-IDENTITY numeric PK — `PublishStatus`.** `pkid` is `tinyint NOT NULL` with no IDENTITY, so
the operator supplies the value.

- The key belongs in `{Table}Request`; there is no `SCOPE_IDENTITY()` round trip.
- Check `ExistsAsync` before INSERT and return `409` on a duplicate.
- Controller route keeps `{id:int}`, so the Angular service needs **no** `encodeURIComponent`.

Both share one rule: **the key is immutable on edit** — `disable()` the control and read it back
with `getRawValue()` on save.

## Unique constraints other than the PK

A `UNIQUE` index that is not the primary key gets the same treatment as `PublishStatus`'s duplicate
key: check before writing and return `409`, rather than letting the index violation surface as a
500.

`FeaturedPromoItem` is the worked example. `IX_FeaturedPromoItem_UniqueDateLocSlot` covers
(`ScheduleOn`, `TrainingCenter_pkid`, `Slot`), so the repository exposes
`SlotTakenAsync(scheduleOn, centre, slot, excludePkid)` and the controller calls it before both
INSERT and UPDATE. On update the row's own pkid is excluded, so re-saving a row into the cell it
already occupies is not a conflict.

## Deleting a row other tables reference

Where enforced FKs point at the entity (`Course` and `Promotion2` → `PublishStatus`), project the
child counts as correlated subqueries in `{Table}Sql.SelectBase`, then have the controller read the
record before deleting and return `409` when a count is non-zero. Letting the DELETE run and
surfacing SQL error 547 as a 500 is the thing to avoid. The same read supplies the `404`.

**Read each FK's `ON DELETE` action before trusting the database to stop you** — error 547 is not
the only outcome, and the guard is not always mere politeness. Two cases already break that
assumption in opposite directions:

- `FK_Course_CourseGroup` is **`ON DELETE CASCADE`**. Although `Course.CourseGroup_pkid` is
  nullable, SQL Server deletes the referencing `Course` rows rather than nulling the column, and
  `Course` cascades onward to `CourseInCertification` and `CourseJobCategories`. An unguarded
  DELETE here "succeeds" with a `204` and destroys courses, so the `409` is the only thing
  preventing silent data loss.
- `Seminar.Partner_pkid` is a real reference with **no `FOREIGN KEY` constraint** behind it, so the
  database would accept the delete and orphan those rows.

Either way the rule is the same: guard on the projected counts, and never rely on the database to
refuse.

An entity nothing references (`FeaturedPromoItem`) needs no guard and no count subqueries — a plain
`204` / `404` delete is correct there. Confirm that from the schema rather than assuming it.

## Multi-step writes

Anything that touches more than one row runs in a single transaction. Two shapes exist:

- **n-n back-fill** — `CourseRepository.CreateAsync` inserts the row and both junction tables in one
  transaction; a half-saved course is worse than a failed one.
- **Reordering under a unique index** — `FeaturedPromoItemRepository.MoveToSlotAsync` swaps two rows
  between slots. The unique index forbids two rows sharing a slot even mid-statement, so the
  neighbour is parked on slot `0` (a value the UI never assigns), the moving row takes the target,
  and the neighbour lands in the vacated slot. All three steps share one transaction so a failure
  cannot strand a row on slot 0. **Nothing else may ever write slot 0.**
