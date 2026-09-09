# Build Spec for AppUser
- database schema: `.\database\auth.sql` (identical `CREATE TABLE` also appears in `.\database\admin.sql`)

`AppUser` is the operator account table. Like `AppRole` its primary key is a **string** (`UserId`),
while `pkid` is a non-key `IDENTITY` column shown as 主代碼. It carries no foreign keys of its own
and is joined to `AppRole` through the junction table `AppUserRole` (n-n).

The column that drives most of the non-standard handling in this spec is **`PasswordHash`**: it is
written by the server only, never read by or accepted from the client, and never touched by an
ordinary update.

---

## Summary

| Item | Detail |
|------|--------|
| Primary Key | `UserId` nvarchar(200) — **string PK**; `pkid` int IDENTITY is a non-key display column |
| Foreign Keys | None |
| Required Fields | `UserId`, `UserName`, `IsActive`, `PasswordHash` (server-supplied) |
| N-N Relationships | `AppUserRole` — AppUser ↔ AppRole (no `DisplayOrder`) |
| Primary-Foreign Links | `AppUserRole` only, managed as the n-n above → **N/A** |
| Query Filters | keyword (`UserId`, `UserName`); tri-state bool `IsActive`; `RoleId` (via `EXISTS` on the junction); `PasswordUpdatedTime` date range |
| Default Sort | `ORDER BY u.UserId ASC` |

---

## Localization

### Chinese Table Name

- AppUser: 使用者
- Description: 系統操作者帳號 — 與角色 AppRole 以 AppUserRole 建立多對多關聯

### Chinese Column Names

- pkid: 主代碼
- UserId: 使用者代碼
- UserName: 使用者名稱
- IsActive: 啟用
- PasswordHash: 密碼雜湊 *(backend only — never rendered)*
- PasswordUpdatedTime: 密碼更新時間

Derived (not columns):

- RoleCount: 角色數
- RoleIds: 角色

---

## Required Fields

Required (NOT NULL):

- `UserId` — nvarchar(200). **The primary key.** Required on create, immutable on edit.
- `UserName` — nvarchar(200).
- `IsActive` — bit, `DF_AppUser_IsActive` defaults to `1`. The form defaults the toggle to `true`
  to match, since the INSERT always names the column explicitly.
- `PasswordHash` — nvarchar(800). NOT NULL, but **never supplied by the client** — see
  [PasswordHash handling](#passwordhash-handling).

Optional (nullable):

- `PasswordUpdatedTime` — datetime. Null until the hash is first written. Server-maintained; it is
  in the response model but not in the write DTO.

`pkid` is `int IDENTITY` but is **not** the key. It is read-only, projected for display as 主代碼,
and excluded from INSERT and UPDATE exactly as an IDENTITY column would be.

---

## Foreign Keys

`AppUser` has no foreign key columns. `AppUserRole.UserId` points *at* it.

**N/A**

---

## Foreign-Primary Links

`AppUser` has no foreign key columns.

**N/A**

---

## Primary-Foreign Links

One table references `AppUser.UserId`:

| Child table | FK column | Constraint |
|-------------|-----------|------------|
| `AppUserRole` | `UserId` nvarchar(200) NOT NULL | `FK_AppUserRole_AppUser` |

That is the junction table for the n-n relationship below, owned by this feature and by the
existing `AppRole` feature — not a separate child entity with a list page of its own. Its rows are
deleted alongside the user inside the delete transaction, so there is no reference count to project
and no `409` guard on delete.

**N/A**

---

## N-N Relationships

### AppUser ↔ AppRole via `AppUserRole`

| Column | Type | Notes |
|--------|------|-------|
| pkid | int IDENTITY | Non-key identity column |
| UserId | nvarchar(200) NOT NULL | FK → `AppUser.UserId` (composite PK part) |
| RoleId | nvarchar(200) NOT NULL | FK → `AppRole.RoleId` (composite PK part) |

There is **no `DisplayOrder`** column, so the relationship is a plain set — a `p-multiselect` of
role ids, with no per-item ordering UI (unlike `SkillTrainCourse` in `spec/sample2.spec.md`).

- **List page**: `RoleCount` — a correlated subquery, shown as a right-aligned numeric column.
  Mirrors how `AppRole` shows 使用者數.
- **Detail page**: `RoleIds` resolved against the `app-roles` lookup and rendered as `p-tag` chips
  showing `RoleName (RoleId)`; falls back to 尚未指派角色.
- **Form (add + edit)**: `p-multiselect`, option label `RoleName (RoleId)`, ordered by
  `PermissionLevel ASC, RoleName ASC`, `appendTo="body"`, `[filter]="true"`,
  `[maxSelectedLabels]="9999"`, `display="chip"`.
- **Request field**: `AppUserRequest.RoleIds` — `List<string>`.
- **Write pattern** (create & update), inside the same transaction as the row write:
  1. `DELETE FROM AppUserRole WHERE UserId = @UserId`
  2. Bulk `INSERT INTO AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId)` for the trimmed,
     de-duplicated (`OrdinalIgnoreCase`) request list.

**Both ends of this junction are editable.** `AppRoleForm` already writes `AppUserRole` rows from
the role side (`AppRoleRequest.UserIds`) with the same delete-then-reinsert pattern scoped to
`RoleId`. Each side rewrites only its own slice — deleting `WHERE UserId = @UserId` never disturbs
another user's rows — so the two forms coexist; the last save of a *given pair* wins. No change is
made to the `AppRole` feature.

---

## PasswordHash handling

This is the feature's central deviation from the generated-CRUD shape. The rules, and where each
one lands:

| Rule | Implementation |
|------|----------------|
| Never sent to the frontend | `PasswordHash` is absent from `Models/AppUser.cs` and from every SELECT projection |
| Never accepted from the frontend | Absent from `AppUserRequest`, from the Angular models, and from every form control |
| Set on CREATE from `SysConfig` | `configKey = 'appConfig'` → `configValue` is JSON → `defaultPassword` property → SHA-256 → stored |
| Untouched on UPDATE | `UPDATE AppUser SET ...` never names `PasswordHash` or `PasswordUpdatedTime` |
| Changed only by an explicit reset | `POST /api/app-users/{id}/reset-password` |

### Reading the default password

```sql
SELECT configValue FROM SysConfig WHERE configKey = 'appConfig'
```

`configValue` is `nvarchar(4000)` holding a JSON **object**; the password is its `defaultPassword`
property, e.g. `{"defaultPassword":"Uwa@2026","sessionMinutes":30}`. It is parsed with
`System.Text.Json` (`JsonDocument`), matching the property name case-insensitively, and returns
`null` when the row is missing, the JSON does not parse, the property is absent, or its value is
not a non-empty string.

This lives in its own repository, `ISysConfigRepository.GetDefaultPasswordAsync`, rather than on
`IAppUserRepository`: it reads a different table, and password reset plus any later login feature
need the same value.

### Hashing

`Security/PasswordHasher.Sha256Hex(string)` returns the SHA-256 digest as **64 lowercase hex
characters** (`SHA256.HashData` over UTF-8 bytes). Deterministic and unsalted — this matches the
plain `PasswordHash nvarchar(800)` column, which has no salt or iteration-count companion column to
store anything stronger. 64 characters sits far inside the 800-character column.

> **Security note, recorded rather than acted on:** unsalted SHA-256 is not a password-storage
> primitive — it is fast and rainbow-tableable. The schema is read-only reference and has nowhere to
> put a salt or work factor, and the requested behaviour is explicit, so this spec implements SHA-256
> as asked. Moving to PBKDF2/bcrypt would need a schema change (or an encoded composite value in
> `PasswordHash`) and is left as follow-up.

### Who hashes what

The controller owns the plaintext for the duration of one call and hands the **hash** down; the
repository never sees a plaintext password:

```csharp
var defaultPassword = await _sysConfig.GetDefaultPasswordAsync(cancellationToken);
if (string.IsNullOrEmpty(defaultPassword)) { /* 500 ProblemDetails 系統設定缺少預設密碼 */ }
var userId = await _repository.CreateAsync(request, PasswordHasher.Sha256Hex(defaultPassword), ct);
```

The missing-config case is guarded up front and answered with a `500` `ProblemDetails` carrying a
Traditional Chinese title, in the same spirit as the delete guards in `CLAUDE.md` — a foreseeable
data condition is not allowed to surface as an unhandled exception.

### PasswordUpdatedTime

Set to `GETUTCDATE()` in the same statement that writes `PasswordHash` — on create and on reset,
never on update. The column then reads literally: *when the stored hash was last written*. UTC is
used because the frontend convention in `spec/feature-spec.template.md` parses API datetimes as
`new Date(dt + 'Z')`; `AppUser.PasswordUpdatedTime` is the first `datetime` column in the built
codebase, so this is the first place that rule applies.

---

## Query Filters

`POST /api/app-users/query` accepts:

- **keyword** — `string?`
  - `LIKE` on `u.UserId` and `u.UserName`. Those are the only string columns that are not the
    password hash, which is excluded from the projection entirely and so is unsearchable.
  - LIKE wildcards (`%`, `_`, `[`) escaped so a literal keyword matches literally.

- **isActive** — `bool?` (tri-state: `null` = no filter, `true` = 啟用, `false` = 停用)
  - Exact match on `u.IsActive`. `false` is a filter, not an absence of one.

- **roleId** — `string?`
  - `EXISTS (SELECT 1 FROM AppUserRole ur WHERE ur.UserId = u.UserId AND ur.RoleId = @RoleId)`.
  - `EXISTS` rather than a `JOIN` so a user holding the role once cannot appear twice.
  - Dropdown populated from `GET /api/lookups/app-roles`.
  - Blank/whitespace is ignored, like `keyword`.

- **passwordUpdatedFrom** / **passwordUpdatedTo** — `DateOnly?` (inclusive date range)
  - `u.PasswordUpdatedTime >= @PasswordUpdatedFrom`
  - `u.PasswordUpdatedTime < DATEADD(day, 1, @PasswordUpdatedTo)` — **not** `<= @PasswordUpdatedTo`.
    The column is `datetime` while the bound is a date; `<=` would silently drop everything after
    midnight on the closing day.
  - Each bound is emitted independently so an open-ended range works.
  - Rows with a null `PasswordUpdatedTime` drop out whenever either bound is set, which is the
    correct reading of "updated between X and Y".

No FK dropdown filters (no FK columns); `roleId` fills that role through the junction.

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/app-roles` | **New** | `AppRoleLookup[]` — `roleId`, `roleName`, `permissionLevel`, ordered `PermissionLevel ASC, RoleName ASC` |
| `GET /api/lookups/app-users` | Exists | Already published for `AppRoleForm`; unchanged. This feature does not consume it |

`AppUser` is itself an FK target only for `AppUserRole`, whose editor is the role multiselect, so no
new `AppUser` lookup is required — the existing one already covers it.

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/app-users` | List all, `ORDER BY UserId ASC` |
| `POST` | `/api/app-users/query` | Filtered query (body: `AppUserQuery`; null body = unfiltered) |
| `GET` | `/api/app-users/{id}` | Get by `UserId`; `404` when missing |
| `POST` | `/api/app-users` | Create; `409` on duplicate `UserId`; `500` when the default password is unavailable |
| `PUT` | `/api/app-users` | Update — **`UserId` comes from the body**, not the route; `404` when missing |
| `DELETE` | `/api/app-users/{id}` | `204`; `404` when missing. Junction rows removed in the same transaction |
| `POST` | `/api/app-users/{id}/reset-password` | Reset the hash to the configured default |

Route template is `{id}` with **no `:int` constraint** — the key is a string, so the Angular service
must `encodeURIComponent(id)` (the `AppRole` rule; the `PublishStatus` numeric-key exemption does
not apply here).

`POST` returns `201 CreatedAtAction(nameof(GetById), new { id = userId }, created)`.

### `POST /api/app-users/{id}/reset-password`

- **Request body**: none. There is no API input for a password anywhere in this feature; the reset
  always restores the `SysConfig` default.
- **Response**: `204 No Content`.
- **Errors**:
  - `404` — no such user (checked with `ExistsAsync` before touching `SysConfig`).
  - `500` `ProblemDetails` 「系統設定缺少預設密碼」 — `SysConfig` has no `appConfig` row, the JSON does
    not parse, or `defaultPassword` is missing/blank.
- Writes `PasswordHash` and `PasswordUpdatedTime` only; no other column, and no junction rows.

No auth attributes — the API still has no authentication wired, matching every existing controller.
Once it is, this feature (and this endpoint in particular) is the first that should get one.

---

## Backend Notes

### Models

`Models/AppUser.cs` — response model. **No `PasswordHash`.**

```csharp
public class AppUser
{
    public int Pkid { get; set; }                            // 主代碼 (non-key identity)
    public string UserId { get; set; } = string.Empty;       // 使用者代碼 (primary key)
    public string UserName { get; set; } = string.Empty;     // 使用者名稱
    public bool IsActive { get; set; }                       // 啟用
    public DateTime? PasswordUpdatedTime { get; set; }       // 密碼更新時間 (read-only)
    public int RoleCount { get; set; }                       // 角色數 — subquery
    public List<string> RoleIds { get; set; } = [];          // 角色 — populated on GET by id only
}
```

`Models/AppUserRequest.cs` — write DTO. **No `PasswordHash`, no `PasswordUpdatedTime`, no `Pkid`.**

```csharp
public class AppUserRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string UserId { get; set; } = string.Empty;       // immutable once created

    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string UserName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;               // matches DF_AppUser_IsActive

    public List<string> RoleIds { get; set; } = [];          // n-n
}
```

`Models/AppUserQuery.cs` — search DTO:

```csharp
public class AppUserQuery
{
    public string? Keyword { get; set; }
    public bool? IsActive { get; set; }
    public string? RoleId { get; set; }
    public DateOnly? PasswordUpdatedFrom { get; set; }
    public DateOnly? PasswordUpdatedTo { get; set; }
}
```

`Models/AppRoleLookup.cs` — slim option row (mirrors `AppUserLookup`):

```csharp
public class AppRoleLookup
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public int PermissionLevel { get; set; }
}
```

### SQL — SELECT

`Repositories/AppUserSql.cs` holds the projection plus `BuildWhere`, following `AppRoleSql` so the
filter logic is unit-testable without a database.

```sql
SELECT u.pkid AS Pkid,
       u.UserId,
       u.UserName,
       u.IsActive,
       u.PasswordUpdatedTime,
       (SELECT COUNT(*) FROM AppUserRole ur WHERE ur.UserId = u.UserId) AS RoleCount
FROM AppUser u
```

`DefaultOrderBy` = `ORDER BY u.UserId ASC`.

`PasswordHash` is deliberately absent — the projection is the single place that keeps it off the
wire, so it must stay absent from `GetAll`, `Query` **and** `GetById`.

No JOINs and no multi-map: there are no FK nav objects. `RoleIds` is a second query issued on the
same connection by `GetByIdAsync` only:

```sql
SELECT ur.RoleId FROM AppUserRole ur WHERE ur.UserId = @UserId ORDER BY ur.RoleId ASC
```

No `nchar` columns, so no `RTRIM()`. No `date`/`time(7)` **columns**, but the query DTO uses
`DateOnly` for the two range bounds, which the `DateOnlyTypeHandler` already registered in
`Program.cs` sends as `DbType.Date`.

### SQL — INSERT

Writable columns: `UserId`, `UserName`, `IsActive`, `PasswordHash`, `PasswordUpdatedTime`.
Excluded: `pkid` (IDENTITY). There is **no `SCOPE_IDENTITY()` round trip** — the key is the
client-supplied `UserId`, which the repository returns.

```sql
INSERT INTO AppUser (UserId, UserName, IsActive, PasswordHash, PasswordUpdatedTime)
VALUES (@UserId, @UserName, @IsActive, @PasswordHash, GETUTCDATE());
```

`@PasswordHash` is the SHA-256 hex of the `SysConfig` default password, computed by the controller.

### SQL — UPDATE

`UserId` is the key and is immutable — WHERE clause only. `PasswordHash` and `PasswordUpdatedTime`
are **not** in the SET list; that is the whole of the "do not modify on update" rule.

```sql
UPDATE AppUser
SET UserName = @UserName,
    IsActive = @IsActive
WHERE UserId = @UserId;
```

`UpdateAsync` returns `false` when zero rows are affected, which the controller turns into `404`.

### SQL — reset password

```sql
UPDATE AppUser
SET PasswordHash = @PasswordHash,
    PasswordUpdatedTime = GETUTCDATE()
WHERE UserId = @UserId;
```

Returns `false` on zero rows affected → `404`.

### SQL — DELETE

Junction rows first, then the row, in one transaction — the `AppRoleRepository.DeleteAsync` shape:

```sql
DELETE FROM AppUserRole WHERE UserId = @UserId;
DELETE FROM AppUser     WHERE UserId = @UserId;
```

`FK_AppUserRole_AppUser` has **no `ON DELETE` action**, so the child rows must be removed explicitly
or SQL error 547 follows. Nothing else in the schema references `AppUser`, so there is no reference
count to project and no `409` path on delete — unlike `PublishStatus`.

### SQL — SysConfig

```sql
SELECT configValue FROM SysConfig WHERE configKey = 'appConfig'
```

### N-N Sync Pattern

```sql
DELETE FROM AppUserRole WHERE UserId = @UserId;
-- then, for each RoleId in the request list:
INSERT INTO AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId);
```

Run inside the create/update transaction. The list is trimmed, blank-filtered and de-duplicated
case-insensitively before insert, so a repeated pick cannot violate `PK_AppUserRole`.

### Special Column Notes

- **`UserId` is the primary key and `pkid` is not.** Route `{id}` with no `:int`, `encodeURIComponent`
  in the Angular service, key disabled in the edit form and read back with `getRawValue()`.
- **`PasswordHash` never appears in a projection, a DTO, or a model.** See the section above.
- `PasswordUpdatedTime` is `datetime NULL` → `DateTime?`, written with `GETUTCDATE()`, displayed
  with the `+ 'Z'` UTC fix.
- `DF_AppUser_IsActive` defaults the column to `1`; the INSERT names the column explicitly, so the
  default never fires and the form's `true` default is what actually decides it.
- The identical `CREATE TABLE [dbo].[AppUser]` block appears in both `auth.sql` and `admin.sql` —
  one table written out twice for readability, as with `PublishStatus`.

---

## Frontend Notes

### Routes

| Path | Component |
|------|-----------|
| `app-users` | `AppUserList` |
| `app-users/new` | `AppUserForm` |
| `app-users/:id` | `AppUserDetail` |
| `app-users/:id/edit` | `AppUserForm` |

Lazy `loadComponent` in `app.routes.ts`, `/new` registered **before** `/:id`, inserted ahead of the
`**` wildcard.

### Angular Model — `core/models/app-user.model.ts`

No password field anywhere:

```ts
export interface AppUser {
  pkid: number;
  userId: string;
  userName: string;
  isActive: boolean;
  passwordUpdatedTime: string | null;
  roleCount: number;
  roleIds: string[];
}

export interface AppUserRequest {
  userId: string;
  userName: string;
  isActive: boolean;
  roleIds: string[];
}

export interface AppUserQuery {
  keyword?: string | null;
  isActive?: boolean | null;
  roleId?: string | null;
  passwordUpdatedFrom?: string | null;
  passwordUpdatedTo?: string | null;
}
```

`core/models/app-role-lookup.model.ts` holds
`{ roleId: string; roleName: string; permissionLevel: number }`.

### Service — `core/services/app-user.service.ts`

The standard six against `${environment.apiUrl}/app-users`, plus `resetPassword`. The key is a
string, so `getById`, `delete` and `resetPassword` all `encodeURIComponent(userId)`. `update()` PUTs
to the collection route with the key in the body.

```ts
resetPassword(userId: string): Observable<void> {
  return this.http.post<void>(
    `${this.baseUrl}/${encodeURIComponent(userId)}/reset-password`, {});
}
```

`LookupService` gains `getAppRoles()` hitting `/lookups/app-roles`.

### List component

Columns: 主代碼 / 使用者代碼 / 使用者名稱 / 啟用 / 密碼更新時間 / 角色數 / 操作.

- 啟用 renders as `pi pi-check` / `pi pi-minus`, the `PublishStatus` list convention, not raw
  `true`/`false`.
- 密碼更新時間 renders `{{ user.passwordUpdatedTime + 'Z' | date:'yyyy/MM/dd HH:mm' }}` guarded by
  `@if`, falling back to `—` when null.

Sortable, paginated `p-table`, `dataKey="userId"`, default sort `{ field: 'userId', order: 1 }`,
default page `{ first: 0, rows: 20 }`, paginator on top, `rowsPerPageOptions [10, 20, 50, 100]`.

Filter drawer (`p-drawer`, `position="right"`): keyword `input pInputText`; a tri-state `p-select`
for 啟用 (全部 / 啟用 / 停用 → `null` / `true` / `false`); a `p-select` of roles from the lookup
with `[showClear]="true"` and `[filter]="true"`; two `p-datepicker` bounds for 密碼更新時間.
Every `p-select` and `p-datepicker` in the drawer carries `appendTo="body"`.

The role list is well under 100 entries, so no `[virtualScroll]`.

The 搜尋條件 badge shows the **count** of applied filters. `isActive === false` is a real filter, so
the count uses an explicit `!== null && !== undefined` test rather than a falsy check.

The lookup load and the first query are sequenced as in `CourseList`: the drawer's role dropdown
needs its labels before restored filters make sense, and the lookup failure is caught on its own so
a failed lookup still leaves the list usable.

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `app-user-list-filters` | Applied `AppUserQuery` |
| `app-user-list-sort` | `{ field, order }` |
| `app-user-list-page` | `{ first, rows }` |

Incoming cross-entity query param: **`roleId`**. `/app-users?roleId=Admin` replaces just that one
saved filter and resets paging, leaving the rest of the restored state alone (the `CourseList`
`applyRouteParams` pattern). Nothing links here yet — `AppRoleDetail` showing a 使用者 link is
follow-up work — but the param is accepted so that link needs no change here later.

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${user.pkid}</b>「${escapeHtml(user.userId)} ${escapeHtml(user.userName)}」？
```

When `roleCount > 0`, append 「此使用者仍有 ${roleCount} 個角色關聯，將一併刪除。」 — a statement of
consequence, not a blocker: the junction rows are deleted with the user, so the delete succeeds.

PrimeNG 20's `p-confirmDialog` has no `escape` input and always renders `message` through
`[innerHTML]`, so the `<b>` works as written and the record text is HTML-escaped first.

### Date Handling

- `passwordUpdatedTime` is a **datetime** and is display-only: `+ 'Z'` before parsing, never edited.
- The two filter bounds are **dates**, serialised with `toIso()` from `@core/utils/date.util` (local
  components — never `toISOString()`) and read back with `fromIso()`.

### Form layout

| Field | Widget | Notes |
|-------|--------|-------|
| 使用者代碼 UserId | `input pInputText` `maxlength="200"` | Required. **Disabled in edit mode** — read with `getRawValue()` |
| 使用者名稱 UserName | `input pInputText` `maxlength="200"` | Required |
| 啟用 IsActive | `p-toggleswitch` | Defaults `true` |
| 角色 RoleIds | `p-multiselect` | Options from `/lookups/app-roles`; label `RoleName (RoleId)` |

Reactive Forms. `forkJoin` over the role lookup and — in edit mode — the record, since there **is** a
lookup to fetch alongside the record (unlike `PublishStatusForm`).

**No password control of any kind**, in either mode. A newly created user silently receives the
`SysConfig` default password; the form says so with a hint under 使用者代碼 in add mode:
「新帳號將套用系統預設密碼，建立後可於檢視頁重設。」

### Special Form Behaviors

- `userId` is editable only in add mode; `form.controls.userId.disable()` when `isEdit()`, and
  `save()` uses `getRawValue()` so the disabled key still reaches the request.
- A `409` on create keeps the user on the form and toasts 「使用者代碼已存在。」
- A `500` on create toasts 「系統設定缺少預設密碼，無法建立帳號。」 — the one place the operator meets
  the `SysConfig` dependency.

### Detail page

Sections: 使用者資料 (主代碼 / 使用者代碼 / 使用者名稱 / 啟用 / 密碼更新時間) and 角色 with the
`p-tag` chips and the `roleCount` badge.

The header carries a third button, 重設密碼, between 返回 and 編輯:

- `p-confirmDialog` first: 「確定要將「${userName}」的密碼重設為系統預設密碼？」
- On `204`: toast 「已重設密碼」 and re-fetch the record so 密碼更新時間 refreshes.
- On `500`: toast 「系統設定缺少預設密碼，無法重設。」
- The new password is never displayed — the operator is expected to know the site default.

### Sub-panels (edit mode only)

**N/A**

### Sidebar placement

Existing group `系統管理 Admin` in `app.ts` `navGroups`, appended after 發布狀態 PublishStatus:

```ts
{ label: '使用者 AppUser', icon: 'pi pi-users', route: '/app-users' }
```

`app.spec.ts` is extended: the `系統管理 Admin` href assertions become
`['/app-roles', '/publish-statuses', '/app-users']`, and the 課程管理 Course case's combined list
grows the same entry.

---

## Tests

### Backend — `src/CMS.API.Tests/`

Hand-written fakes, not a mocking library (house rule).

- `Fakes/FakeAppUserRepository.cs` — in-memory `IAppUserRepository` mirroring the real contract:
  string key, keyword / `IsActive` / `roleId` / date-range filtering, `UserId ASC` ordering,
  `RoleCount` + `RoleIds` projection, delete-then-reinsert n-n, recorded `PasswordHash` writes so
  the create-and-reset paths are assertable, and `PasswordUpdatedTime` set on both.
- `Fakes/FakeSysConfigRepository.cs` — a settable `DefaultPassword` (nullable) so the missing-config
  `500` is reachable.
- `Fakes/FakeLookupRepository.cs` — **modify**: add `AppRoles` + `GetAppRolesAsync`.
- `Controllers/AppUsersControllerTests.cs` — list; query (keyword, `isActive` true *and* false,
  `roleId`, date range, combined, no match, null body); get-by-id found / not-found; create `201`
  with the route value, create persisting roles, create hashing the `SysConfig` default into
  `PasswordHash`, create duplicate `409`, create with missing config `500`; update `200` with the
  key from the body, update replacing role assignments, **update leaving `PasswordHash` and
  `PasswordUpdatedTime` untouched**, update missing `404`; delete `204` removing junction rows,
  delete missing `404`; reset-password `204` writing a new hash and timestamp, reset on a missing
  user `404`, reset with missing config `500`.
- `Repositories/AppUserSqlTests.cs` — `BuildWhere` per filter and combined; keyword trimming and
  LIKE escaping; `isActive = false` producing a clause rather than being skipped as falsy; blank
  `roleId` ignored; the `EXISTS` form of the role clause; the `DATEADD(day, 1, ...)` upper bound;
  and `SelectBase` containing the `RoleCount` subquery **and not containing `PasswordHash`**.
- `Repositories/SysConfigJsonTests.cs` — `defaultPassword` extraction: happy path, case-insensitive
  property name, absent property, blank value, malformed JSON, JSON that is not an object.
- `Security/PasswordHasherTests.cs` — known-vector SHA-256 hex, 64-character lowercase output,
  determinism, and that different inputs differ.
- `Controllers/LookupsControllerTests.cs` — **modify**: a case for `GET /api/lookups/app-roles`.

### Frontend — `src/CMS.NG/`

- `core/services/app-user.service.spec.ts` — each method's URL and verb; `encodeURIComponent` on
  `getById` / `delete` / `resetPassword`; PUT carrying the key in the body; `resetPassword` POSTing
  to the `/reset-password` sub-route with an empty body.
- `core/services/lookup.service.spec.ts` — **modify**: a case for `getAppRoles()`.
- `features/app-users/app-user-list/app-user-list.spec.ts` — loads lookups then the list, renders
  rows including the 啟用 icon and the formatted 密碼更新時間, unfiltered body by default,
  applies/clears drawer filters, counts `isActive: false` in the badge, honours a `roleId` query
  param, persists and restores session state, handles query failure, navigates add/view/edit,
  deletes on confirm.
- `.../app-user-detail/app-user-detail.spec.ts` — loads record + role lookup in parallel, resolves
  role chips, 404 path, no-id path, back/edit navigation, reset-password confirm → POST → reload,
  and the `500` reset path.
- `.../app-user-form/app-user-form.spec.ts` — add mode (`userId` enabled, `isActive` defaulting to
  `true`, required-field validation, trimmed POST body, **no password key in the request body**,
  `409` and `500` handling) and edit mode (`userId` disabled, PUT including the disabled key,
  load-failure recovery).
- `app.spec.ts` — **modify**: assert the third Admin nav item and the updated href lists.

---

## Files to Create / Modify

### Backend

| File | Action |
|------|--------|
| `src/CMS.API/Models/AppUser.cs` | Create |
| `src/CMS.API/Models/AppUserRequest.cs` | Create |
| `src/CMS.API/Models/AppUserQuery.cs` | Create |
| `src/CMS.API/Models/AppRoleLookup.cs` | Create |
| `src/CMS.API/Security/PasswordHasher.cs` | Create |
| `src/CMS.API/Repositories/AppUserSql.cs` | Create |
| `src/CMS.API/Repositories/IAppUserRepository.cs` | Create |
| `src/CMS.API/Repositories/AppUserRepository.cs` | Create |
| `src/CMS.API/Repositories/ISysConfigRepository.cs` | Create |
| `src/CMS.API/Repositories/SysConfigRepository.cs` | Create |
| `src/CMS.API/Controllers/AppUsersController.cs` | Create |
| `src/CMS.API/Repositories/ILookupRepository.cs` | Modify — add `GetAppRolesAsync` |
| `src/CMS.API/Repositories/LookupRepository.cs` | Modify — implement it |
| `src/CMS.API/Controllers/LookupsController.cs` | Modify — add `GET /app-roles` |
| `src/CMS.API/Program.cs` | Modify — register `IAppUserRepository`, `ISysConfigRepository` |

### Frontend

| File | Action |
|------|--------|
| `src/CMS.NG/src/app/core/models/app-user.model.ts` | Create |
| `src/CMS.NG/src/app/core/models/app-role-lookup.model.ts` | Create |
| `src/CMS.NG/src/app/core/services/app-user.service.ts` | Create |
| `src/CMS.NG/src/app/features/app-users/app-user-list/*` (ts/html/scss) | Create |
| `src/CMS.NG/src/app/features/app-users/app-user-detail/*` | Create |
| `src/CMS.NG/src/app/features/app-users/app-user-form/*` | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.ts` | Modify — add `getAppRoles()` |
| `src/CMS.NG/src/app/app.routes.ts` | Modify — four lazy routes |
| `src/CMS.NG/src/app/app.ts` | Modify — nav item under 系統管理 Admin |

### Tests

| File | Action |
|------|--------|
| `src/CMS.API.Tests/Fakes/FakeAppUserRepository.cs` | Create |
| `src/CMS.API.Tests/Fakes/FakeSysConfigRepository.cs` | Create |
| `src/CMS.API.Tests/Controllers/AppUsersControllerTests.cs` | Create |
| `src/CMS.API.Tests/Repositories/AppUserSqlTests.cs` | Create |
| `src/CMS.API.Tests/Repositories/SysConfigJsonTests.cs` | Create |
| `src/CMS.API.Tests/Security/PasswordHasherTests.cs` | Create |
| `src/CMS.API.Tests/Fakes/FakeLookupRepository.cs` | Modify |
| `src/CMS.API.Tests/Controllers/LookupsControllerTests.cs` | Modify |
| `src/CMS.NG/.../app-user.service.spec.ts` | Create |
| `src/CMS.NG/.../app-user-list.spec.ts` | Create |
| `src/CMS.NG/.../app-user-detail.spec.ts` | Create |
| `src/CMS.NG/.../app-user-form.spec.ts` | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.spec.ts` | Modify |
| `src/CMS.NG/src/app/app.spec.ts` | Modify |

---

## Deviations from the /crud skill template

Recorded per `CLAUDE.md`, which wins where the skill conflicts with it.

- **No mocking library.** The skill asks for Moq; the house rule is hand-written fakes in
  `src/CMS.API.Tests/Fakes/`. Fakes it is.
- **~~No `RowAuditWriter`~~ — closed.** Recorded when nothing in the codebase wrote the `RowAudit`
  table and there was no authentication to source a `UserName` from. Both arrived later:
  `AppUserRepository` writes an audit row on every insert, update and delete, plus one for the
  profile rename and one for a password reset — that last reads `PasswordUpdatedTime`, because the
  snapshot projection may not select `PasswordHash`. See the 異動紀錄 section of
  `spec/conventions/backend.md`. The `RowAuditBadgeComponent` arrived with it:
  `RowAuditBadge` in `core/components/`, rendered in this feature's `.page-header`. See the
  異動紀錄 section of `spec/conventions/frontend.md`.
- **No sticky `p-toolbar`.** The existing pages use a `.page-header` action bar; this feature
  matches them rather than introducing a second header pattern.
- **Extra endpoint beyond the standard six.** `POST /{id}/reset-password`, required by the
  `PasswordHash` rules.
- **An extra repository.** `ISysConfigRepository` is not an entity CRUD repository; it exists because
  `SysConfig` is read by this feature and the schema is read-only.

---

## Build notes

Decisions taken while building that the spec above did not pin down.

- **The JSON parsing is a public static on `SysConfigRepository`, not an instance method.**
  `GetDefaultPasswordAsync` reads the row and delegates to
  `SysConfigRepository.ExtractDefaultPassword(string?)`, which is `public static` so
  `SysConfigJsonTests` can exercise every failure mode without a database — the same split as the
  `{Table}Sql.BuildWhere` helpers. `AppConfigKey` and `DefaultPasswordProperty` are exposed as
  constants so the tests assert the spec's literals rather than restating them.
- **`AppUserList` loads its one lookup with a plain `catchError`, not `forkJoin`.** The
  `CourseList` sequencing exists to keep three independent lookups from cancelling each other;
  with a single role lookup there is nothing to isolate.
- **`AppUserDetail` is the first detail page to carry `p-toast` and `p-confirmDialog`**, because
  重設密碼 needs both. It follows the list pages' `providers: [MessageService,
  ConfirmationService]` shape rather than introducing a new one.
- **`app-user-list.spec.ts` calls `TestBed.resetTestingModule()` at the top of its `setup()`.**
  The `roleId` query-param case needs a second `ActivatedRoute`, and reconfiguring an already
  instantiated module throws. No other list spec needs this because no other one varies its route.
