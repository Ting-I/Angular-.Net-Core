# Testing conventions

Read before writing or changing a test. The short version lives in `CLAUDE.md`; this file is the
detail.

Every new feature needs both sides covered: list/filter, view, add, edit.

## Backend (`src/CMS.API.Tests`)

- **Tests never touch SQL Server.** Controllers are tested against hand-written fakes in
  `src/CMS.API.Tests/Fakes/` that mirror the repository contract. **Do not add a mocking library**
  — the fakes are the pattern.
- A fake mirrors the behaviour the real repository gets from the database: server-assigned IDENTITY
  keys, the filters `BuildWhere` would apply, uniqueness checks, ordering, and any multi-row write
  such as a slot swap. It also records what it was asked to do (`CreatedPkids`, `UpdatedPkids`,
  `DeletedPkids`, `Moves`) so a test can assert that a rejected request wrote nothing.
- **One exception to "no host": authorization.** The 401 that guards a protected endpoint comes from
  middleware, so `AuthorizationTests` runs against `TestApiFactory`, a `WebApplicationFactory<Program>`
  with every repository swapped for its fake and `IDbConnectionFactory` swapped for
  `ThrowingDbConnectionFactory` — a repository nobody replaced fails loudly instead of opening a
  connection. Everything else still tests a controller instance directly.
- **A repository can be tested too, and still not touch SQL Server.** `FakeDbConnection` (with
  `FakeDbCommand` / `FakeDataReader` / `FakeDbConnectionFactory`) is a scripted ADO.NET provider:
  statements are matched by substring and consumed in registration order, and every one is recorded
  with its bound parameters and the transaction it ran on. It derives from `DbConnection` because
  Dapper's async methods reject a bare `IDbConnection`. Use it where the behaviour under test is
  the repository's own sequencing rather than a controller's decisions —
  `PartnerRepositoryAuditTests` pins the 異動紀錄 retrofit that way, including that a failed change
  writes no audit row.
- SQL and filter logic is tested directly against `{Table}Sql.BuildWhere` and the other static
  members — projection contents, `splitOn`, default ordering, and any date arithmetic such as
  `FeaturedPromoItemSql.WeekOf`. These need no fake and no database.
- Cover the failure arms, not just the happy path: `404` on a missing key, `409` on a duplicate key
  or occupied unique slot, `409` on a guarded delete, `400` on an out-of-range action.

## Frontend (`src/CMS.NG`)

- `HttpTestingController` for every HTTP expectation; `httpMock.verify()` in `afterEach`.
- Component specs reach `protected` members through a
  `const api = () => component as unknown as Record<string, any>` helper rather than widening the
  component's real API.
- Clear `sessionStorage` in both `beforeEach` and `afterEach` for a list spec, then seed it when the
  page's state must be pinned — it is how a week- or date-driven page is made independent of today's
  date.
- The signed-in session lives in `sessionStorage` too, and `AuthService` reads it **when it is
  constructed** — so seed `auth-profile` before the first `TestBed.inject`, not after. Build the
  token with `@core/testing/fake-jwt`; `fakeProfile(userId, userName, roles)` is the usual call.
  A spec that renders `App` needs one, or the shell does not render at all.

### Gotchas that cost time

- `expectOne` **consumes** the request it matches. A follow-up `expectOne(() => true)` on the same
  request then fails with "found none" — hold the returned `TestRequest` and `flush` that instead.
- Signal inputs are set with `fixture.componentRef.setInput(...)` before the first
  `detectChanges()`, not by assigning to the property.
- An `it` callback that `await`s anything must be declared `async`; the Angular compiler rejects the
  build otherwise, and the failure reads as a bare TS1308 rather than a test failure.
- Run headless and non-interactive:
  `npm test -- --watch=false --browsers=ChromeHeadless`.
