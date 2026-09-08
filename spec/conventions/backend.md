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
