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

## Credentials

**`PasswordHash` leaves the server in exactly one shape: none.** `AuthSql.SelectCredential` is the
only query in the API that selects it, and `AppUserCredential` the only model that carries it — a
repository-to-controller value that is never serialized. Keep the column out of
`AppUserSql.SelectBase`, out of every response model, and out of the JWT payload. A second query
that needs it is a sign the check belongs where the first one already is.

**The client never hashes.** Plaintext goes up over HTTPS and `PasswordHasher.Sha256Hex` runs on
the server, so the stored format stays a server-side decision. `PasswordHasher.Matches` compares in
fixed time and ignores hex case, because rows written by hand or by an older tool may be uppercase.

SHA-256 here is unsalted and uniterated because that is what the schema can hold — `PasswordHash`
is a bare `nvarchar(800)` with no companion salt or work-factor column, and `database/*.sql` is
read-only reference. It is not a password-storage primitive; moving to PBKDF2 or bcrypt needs
either a schema change or an encoded composite value, and is open follow-up.

**`POST /api/auth/login` answers one identical `401` for all three failures** — an unknown UserId,
an `IsActive = 0` account, and a wrong password — so the endpoint cannot be used to enumerate
accounts. Every arm still runs the hash compare where it can rather than short-circuiting on the
cheap check, and the rejection body must stay free of anything attempt-specific.

A new password is checked against `PasswordPolicy`: at least 8 characters over at least 3 of the 4
classes (uppercase / lowercase / digit / symbol), where "symbol" is anything that is not one of the
other three, so a space or a 中文字 counts. `PasswordPolicy.RequirementMessage` is the exact
Chinese wording the UI shows — it travels as the `ProblemDetails` `Title`, with the English
sentence as `Detail`. The Angular form applies the same rule for the operator's sake; the API
re-checks regardless.

## Unhandled exceptions — one 500, and it says nothing

`ExceptionHandlingMiddleware` (`src/CMS.API/Middleware/`) is registered first in `Program.cs`, so
it wraps every later middleware as well as the controllers. Anything that escapes a controller or a
repository is logged in full on the server and answered with one fixed `ProblemDetails`:
`GenericTitle` (系統發生錯誤，請稍後再試。), `GenericDetail` (`An unexpected error occurred.`), a
`traceId`, and nothing else. The constants are what the tests assert against; do not re-word them
in one place only.

- **It catches, it never converts.** A 401 from the bearer middleware, a 403 from authorization, a
  400 from model validation and a controller's own 404 / 409 are all produced by *returning* a
  status, so they never reach the `catch` and travel out exactly as they were. This is why the
  guards elsewhere in this file still matter: the middleware is the net under a bug, not a
  substitute for reading a child count before a DELETE. Error 547 surfacing as a generic 500 is
  still a defect.
- **Nothing from the exception reaches the caller.** No stack trace, no statement, no object or
  server name — a `SqlException` message quotes all three. The server log is where it goes, and
  `traceId` is what ties a report of "it said 系統發生錯誤 at 14:32" back to it.
- **It must not `Response.Clear()`.** That would take the CORS headers with it, and a 500 the
  browser refuses to read cross-origin reaches the Angular interceptor as a status-0 network error
  rather than as the 500 whose body carries the message. Only the status code and the body are
  written. `ExceptionHandlingMiddlewareTests` pins this, and the pipeline test asserts the header
  survives on a real request.
- **A request the client abandoned is not a failure.** An `OperationCanceledException` raised while
  `RequestAborted` is cancelled logs at Information and answers 499; one raised without it is an
  ordinary bug and still a 500.
- **A response already on the wire is rethrown**, because its status line is gone.

Two test files cover it. `ExceptionHandlingMiddlewareTests` drives the middleware over a
`DefaultHttpContext` — the only way to pin the 403 pass-through, since no endpoint here issues one,
and the started-response case. `ExceptionHandlingPipelineTests` runs the real host through
`ThrowingApiFactory`, a `TestApiFactory` with 角色 AppRole backed by `ThrowingAppRoleRepository`
(the read-side counterpart of `ThrowingDbConnectionFactory`: its message reads like a
`SqlException` so a test can prove none of it leaks).

## Authorization

Every endpoint requires an authenticated user. That is a `FallbackPolicy` in `Program.cs`, not an
`[Authorize]` per controller, so a controller added later is protected by omission rather than left
open by it. `AuthController` is the only `[AllowAnonymous]` one — it has to be, or nobody could
obtain a token — and `AuthorizationTests` asserts by reflection that it stays the only one, at the
action level as well as the controller level.

Bearer tokens are validated with the same SysConfig `appConfig` `symmetricSecurityKey` that
`JwtTokenService` signs with, and **the key is read per request, never captured at startup** —
rotating the row has to rotate validation too. It cannot go straight into
`TokenValidationParameters` for two reasons: reading it needs a scoped `ISysConfigRepository`, and
`IssuerSigningKeyResolver` is synchronous. `SysConfigSigningKeys` bridges that — the JwtBearer
`OnMessageReceived` event (async, and holding the request's service scope) reads the key and parks
it on `HttpContext.Items`, and the resolver picks it up from there without blocking. A request
carrying no `Authorization: Bearer` header never triggers the read, so an anonymous login costs no
extra query.

Neither an issuer nor an audience is validated: `JwtTokenService` stamps neither, because there is
no second party to name. `ClockSkew` is zero — the default five minutes would keep a 24-hour token
alive past its expiry. `NameClaimType` / `RoleClaimType` point at the claims the token actually
carries, so `User.IsInRole("Admin")` reads the login's roles rather than looking for names nothing
here writes.

The 401 comes from middleware, not from a controller, so it is only observable through the real
pipeline: `TestApiFactory` hosts the API in process with every repository swapped for its fake and
`IDbConnectionFactory` swapped for one that throws.

**`[AllowAnonymous]` is not undone by an `[Authorize]` further in.** The authorization middleware
short-circuits on any `IAllowAnonymous` in an endpoint's metadata, wherever it came from, so an
`[Authorize]` action inside `AuthController` would still be reachable without a token. An endpoint
that needs the caller identified therefore belongs in a controller of its own, even when it shares
the route prefix: `ProfileController` is `[Route("api/auth")]` and serves `PUT /api/auth/profile`
next to `POST /api/auth/login`, protected by the fallback policy because it says nothing about
authorization at all. `AuthorizationTests` keeps `AuthController` the only anonymous class.

An endpoint acting **on the caller** takes the key from `User.FindFirstValue(
JwtTokenService.UserIdClaimType)` and nowhere else. The request DTO must not carry the key at all —
`ProfileRequest` has one property, `UserName` — so a client that names somebody else's account is
not "rejected", it is discarded by the deserializer with nothing to bind to. Anything else the
endpoint must not change (roles, there) is absent for the same reason, and the repository call is
narrowed to match: `UpdateUserNameAsync` writes one column, where `UpdateAsync` would rewrite
`AppUserRole` from a request that carries no roles.

`POST /api/auth/change-password` is the second endpoint on that controller and follows the same
rule: `ChangePasswordRequest` carries three plaintext passwords and no key. Three things about it
are deliberate.

- **The credential read stays inside the action.** `IAuthRepository.GetCredentialAsync` is still
  the only thing that returns a `PasswordHash`; the value it brings back is compared against
  `PasswordHasher.Sha256Hex(current)` and goes no further. The response is `204` with no body, so
  there is nowhere for a hash to leak even by accident.
- **A wrong current password is a `400`, not a `401`.** The caller's token is perfectly good — the
  password typed into a field is what was wrong. A `401` would trip the UI's interceptor into
  clearing the session and bouncing to `/login`, throwing the operator out over a typo. Reserve the
  `401` for the token itself.
- **Order matters, and it is the spec's order.** Current password, then complexity, then the
  confirmation, then the write. Answering the current-password failure first means a caller holding
  a stolen token learns nothing about the policy without also knowing the password, and every arm
  returns before `ResetPasswordAsync`, so a rejected request leaves `PasswordHash` and
  `PasswordUpdatedTime` untouched.

The complexity rule itself lives in `PasswordPolicy` — 8 characters and 3 of the 4 classes
(uppercase / lowercase / digit / symbol), where "symbol" is everything that is not one of the other
three, so a space or a 中文字 counts. `PasswordPolicy.RequirementMessage` is the exact Chinese
wording the UI shows; it is the `ProblemDetails` `Title`, with the English sentence as `Detail`.
The Angular form applies the same rule, but that is convenience — the API re-checks regardless.

`ResetPasswordAsync` is what writes, not `UpdateAsync`: it sets `PasswordHash` and
`PasswordUpdatedTime` and nothing else, where `UpdateAsync` would rewrite `AppUserRole` from a
request that carries no roles. It is the same narrowing `UpdateUserNameAsync` exists for.

### Changing a password revokes the tokens that came before it

A signature and an unexpired `exp` are **not** the whole of validity here. `TokenFreshness`, wired
as the JwtBearer `OnTokenValidated` event, also refuses a token whose `iat` predates the account's
`AppUser.PasswordUpdatedTime` — so a password change signs out every session that was holding a
token issued before it, everywhere, not just in the browser that made the change.

- **It needs no schema change and no list of live tokens.** `PasswordUpdatedTime` already exists
  and `ChangePassword` already writes it, so the row carries the moment every earlier token
  stopped counting. `AuthSql.SelectPasswordUpdatedTime` reads one column on the primary key.
- **It runs after the cryptographic checks**, so an unsigned or expired token never reaches the
  query. That is one narrow read per *validated* request — the same shape of cost the signing-key
  read already accepts, and for the same reason: the alternative is trusting a stale value.
- **`context.Fail`, never a thrown exception.** A stale token has to produce the identical plain
  401 a forged one does; anything else tells the caller which of the two it was holding.
- **The stored time is truncated to the second before comparing**, because `iat` is whole seconds.
  Skip that and a password changed at `10:00:00.400` rejects the token from the login at
  `10:00:00.900` — whose `iat` floors to `10:00:00` — and the operator cannot sign back in at all.
  The residue is a sub-second window in which a token issued earlier in the same second still
  passes; anything from a second before the change does not.
- **Two asymmetries are deliberate.** A `null` `PasswordUpdatedTime` proves nothing against the
  token, so it passes and the endpoint answers for itself. A token with no readable `iat` fails
  closed once the account *does* have a change to compare against — `JwtTokenService` always
  stamps `iat`, so such a token did not come from here.

The Angular side clears its session storage on a successful change as well, but that is
housekeeping over a token the API has already stopped accepting — it is not the revocation, and a
copy of the token taken elsewhere is refused just the same.

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

## 異動紀錄 — every write leaves a RowAudit row

`RowAudit` is the cross-cutting change log in `database/admin.sql`. Every repository that writes a
business table writes one, through `IRowAuditWriter` — injected alongside `IDbConnectionFactory`,
registered scoped in `Program.cs`. It is not an `I{Table}Repository`, and no controller calls it:
a change is audited by the code that made it, or the trail can be told a change happened that did
not.

**The three shapes.** Each one is `{Table}Repository`'s whole audit contract.

- **Insert** — write the row, read it back with `{Table}Sql.SelectRow`, `LogInsertAsync`. Reading
  back rather than echoing the request means the trail describes what was stored.
- **Update** — read the "before" **first**, apply the change, read the "after", `LogUpdateAsync`.
  The order is the point: ActionDesc is the list of columns that actually differ, and a comparison
  against anything but the row as it stood is a guess. The before-read doubles as the existence
  check, so `UpdateAsync` returns `false` from it rather than from an affected-row count.
- **Delete** — read the row **first**, delete it, `LogDeleteAsync`. Once it is gone the trail is
  the only thing that still says what it was.

**The audit row rides the caller's transaction.** Every `Log*Async` takes the `IDbTransaction` the
change is running in and inserts on its connection, inside it. So a repository that had no
transaction has one now — `Partner`, `CourseGroup`, `PublishStatus` and `FeaturedPromoItem` each
open one for a single statement, purely so the change and its trail entry commit together. A failed
or rolled-back change leaves no audit row, and the tests that matter are the ones proving that.

**`{Table}Sql.SelectRow` is a projection of its own, and has to be.** It is the base table's own
columns and nothing else — no `LEFT JOIN` nav objects, which compare by reference and would report
as changed on every save; no child-count subqueries, which move when another table changes. n-n
lists *are* part of the snapshot where the entity has them (`Course`, `AppRole`, `AppUser`), read
by the same query the record read uses: they compare element by element, and without them a save
that only re-picked roles or certifications would write no audit row at all — which is exactly the
change somebody would want the trail to show.

**`PasswordHash` stays out of it**, like every other projection. `AppUserSql.SelectRow` omits the
column, so `ResetPasswordAsync` audits as `PasswordUpdatedTime` — the stamped time moves on every
reset, so the row is still written and a password change is never silent in the trail.

**What is written.** `TableName` is the real table name (`"Course"`, `"FeaturedPromoItem"`), held as
a `private const string TableName` on the repository. `PrimaryKeyValues` is the row's `pkid` — every
table here carries one, including the two whose real key is something else. `ActionDesc` is the
entity's first string property on insert and delete, and the changed-column list on update. A save
that changed nothing writes no row — and costs no query, because `LogUpdateAsync` returns before it
resolves anything.

**`UserName`: identity from the token, name from the row.** The operator is never a parameter — a
repository is in no position to say who is signed in — so `RowAuditWriter` takes the `userId` claim
off the validated principal through `IHttpContextAccessor`. It then reads 使用者名稱 from `AppUser`
with `AppUserSql.SelectUserName`, because the `userName` claim is only ever as fresh as the login
that issued it: `PUT /api/auth/profile` renames an account without re-issuing a token,
`TokenFreshness` revokes on a password change and nothing else, and an administrator renaming
somebody else could not re-issue their token at all. Trusting the claim files up to 24 hours of
audit rows under a name the operator no longer has.

That read runs **on the caller's transaction, or not at all**. A rename is audited by the very
transaction that performed it, so the name has to be read from inside it — on any other connection
the new value is invisible, and the read would block on the lock the rename holds until the
caller's transaction commits, which it cannot do until the audit write returns. With no transaction
to read on, or with the account gone (an operator deleting their own row), it falls back to the
claim and then to `"system"`; the column is NOT NULL.

**It is a direct query, not `IAppUserRepository.GetByIdAsync`.** Three reasons, and the first is
fatal on its own: `AppUserRepository` takes an `IRowAuditWriter`, so injecting the repository back
into the writer is a DI cycle. It also opens a connection of its own — see above — and runs
`SelectBase` plus a second query for `RoleIds` to fetch one string. The audit INSERT itself is
executed directly for the same reasons.

**Two writes are audited that are not plain CRUD.** `CourseRepository.CopyAsync` audits as an
Insert on the new pkid — a row that did not exist now does, and nothing is recorded against the
source, which was only read. `FeaturedPromoItemRepository.MoveToSlotAsync` writes one Update per
row the swap moved; the neighbour's trip through slot 0 is scaffolding, so each before/after pair
spans the whole swap rather than each statement inside it.

**Testing a retrofitted repository** needs a provider, not a database: `FakeDbConnection` in
`src/CMS.API.Tests/Fakes/` is a scripted `DbConnection` (it must derive from `DbConnection` —
Dapper's async methods reject a bare `IDbConnection`) that records every statement, its bound
parameters and the transaction it ran on. `PartnerRepositoryAuditTests` is the worked example, and
it hands the writer a `ThrowingDbConnectionFactory` so an audit row that stops riding the caller's
transaction fails the test instead of quietly opening a second connection.

**Reading the trail back is a separate repository.** `GET /api/rowaudit?tableName=Course&pkid=123`
answers one record's history, newest first, through `IRowAuditRepository` /
`RowAuditRepository` — an ordinary read-only repository a controller injects, not the writer. The
two stay apart on purpose: the writer runs inside somebody else's transaction and is write-only,
and nothing that writes should acquire a way to read. `RowAuditSql` holds both statements and
nothing else — there is no update and no delete, because an audit row that can be edited is not an
audit row, and for the same reason `RowAuditController` exposes no write.

Three things about that endpoint are load-bearing:

- **Both halves of the filter.** `PrimaryKeyValues` is only unique within a `TableName`, so pkid 7
  alone mixes 原廠 7 with 課程 7.
- **`pkid` is nullable on the action, and a missing one is a `400`.** A plain `int` would default a
  malformed request to pkid 0 and answer it with a straight face — and 0 is a real key for a table
  whose pkid is not IDENTITY.
- **An unknown table or key is an empty array, not a `404`.** "This record has no history yet" is
  what the badge renders, and it is not an error.

The ordering is `[DateTime] DESC, pkid DESC`. The tie-break is not decoration: a change that writes
more than one audit row — `MoveToSlotAsync` writes one per row the swap moved — stamps them from
the same clock reading, and IDENTITY is the only thing that still increases inside one transaction.

**Scaffolding a new entity.** The `/crud` skill's file list says only "Inject `RowAuditWriter`; log
on INSERT / UPDATE / DELETE", which is not enough to get this right — this section is the contract,
and CLAUDE.md says so where the two disagree. Three things the skill's list leaves out:

- **`{Table}Sql.SelectRow` is a file the skill never mentions.** It is not optional: the repository
  has nothing to snapshot without it, and it is a projection of its own — see above.
- **Inject the interface, `IRowAuditWriter`**, not the concrete `RowAuditWriter` the skill names.
- **A single-statement write still opens a transaction**, purely so the change and its trail entry
  commit together.

`spec/admin/RowAudit.md` is the full specification of the trail — the read endpoint, the badge, and
the reasoning behind both.
