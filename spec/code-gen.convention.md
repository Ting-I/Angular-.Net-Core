# Code Generation Patterns

## The `/crud` skill — where it disagrees with this repo

`/crud` scaffolds an entity end to end: schema → spec → stop for confirmation → both sides plus
tests. It is a general skill, so it asks for a few things this codebase does not have. **Where it
conflicts with `CLAUDE.md` or this file, they win.** Record any further deviation in the spec the
run generates, the way `spec/promotion/FeaturedPromoItem.md` does.

| It asks for | Here instead |
|-------------|--------------|
| Moq | Hand-written fakes — see `spec/conventions/testing.md`. Moq is not a dependency of this solution. |
| A sticky `p-toolbar` | There is no `p-toolbar` in this codebase. Page actions sit in `.page-header`. |
| `RowAuditBadgeComponent` | It exists as `RowAuditBadge` (`core/components/`), placed at the **start** of `.page-header` — where a `#start` toolbar slot would have landed. |
| No audit writer | Every CRUD repository calls `IRowAuditWriter`, so a scaffolded one must too. See the 異動紀錄 section of `spec/conventions/backend.md`. |

## Backend

  ### Models
  - `{TABLE}.cs` — response model (with nav objects for FKs, subquery counts for n-n)
  - `{TABLE}Request.cs` — write DTO (FK pkids only; n-n as `List<int>`)
  - `{TABLE}Query.cs` — search DTO (Keyword?, FK pkids?, bool fields?, date ranges?)

  ### Repository
  - `I{TABLE}Repository.cs` + `{TABLE}Repository.cs`
  - Dapper only (no EF). Multi-map when JOINing nav objects.
  - `nchar` columns: always `RTRIM()` in SQL
  - n-n: delete-then-reinsert on update; separate query on same connection for read

  ### Controller
  - Route: `/api/{tablePlural}`; `PUT` takes pkid from body (no route param)
  - String PKs: route `{id}` (no `:int` constraint); service uses `encodeURIComponent`
  - `DateOnly`/`TimeOnly` fields: register Dapper type handlers (already in Program.cs)
  
 ## Frontend

  ### Files
  - `features/{table-plural}/{table}-list/`
  - `features/{table-plural}/{table}-detail/`
  - `features/{table-plural}/{table}-form/`

  ### List page
  - Session storage keys: `{table}-list-filters`, `{table}-list-sort`, `{table}-list-page`
  - Sortable/paginated `p-table`; filter drawer (`p-drawer`)
  - `p-select` in drawer: always `appendTo="body"`; mapped `{ pkid, label }[]` getter; `[filter]="true"` for 10+ options
  - `p-select` / `p-multiselect` with 100+ items: add `[virtualScroll]="true" [virtualScrollItemSize]="43"`

  ### Form page
  - Reactive Forms; `forkJoin` for parallel lookup calls on init
  - `p-datepicker`: convert ISO string ↔ `Date` on load/save
  - `p-datepicker [timeOnly]="true"` for `time` columns; `parseTime`/`toTimeStr` helpers
  - `p-multiselect` for n-n: `[maxSelectedLabels]="9999"`; wrap chips via `::ng-deep`

  ### Sidebar nav
  - Add entry under the appropriate nav group in `app.html` / `app.ts`
  
 
  ## Special Types

  | Column type | Handling |
  |-------------|---------|
  | `nchar(n)` | `RTRIM()` in all SQL SELECTs |
  | `time(7)` | C# `TimeOnly` via `TimeOnlyTypeHandler`; display with `\| slice:0:5`; `p-datepicker [timeOnly]` in form |
  | `date` | C# `DateOnly` via `DateOnlyTypeHandler`; `p-datepicker` in form |
  | `smallint` PK | No special handling |
  | `nvarchar` PK (string) | Controller route `{id}` (no `:int`); service calls `encodeURIComponent(id)` | 
  
  
   ## API Endpoints

  | Method | Route | Description |
  |--------|-------|-------------|
  | GET    | `/api/{plural}` | All records |
  | POST   | `/api/{plural}/query` | Filtered search |
  | GET    | `/api/{plural}/{id}` | Single record |
  | POST   | `/api/{plural}` | Create |
  | PUT    | `/api/{plural}` | Update (pkid in body) |
  | DELETE | `/api/{plural}/{id}` | Delete |
  | GET    | `/api/lookups/{plural}` | Slim lookup list (if used as FK target) |