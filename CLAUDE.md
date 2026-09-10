# CLAUDE.md

What you always need, and where the rest lives. Every rule below is the short form of a section in
a reference file — read that file before touching its area rather than working from memory.

## What this repo is

A CMS scaffolded from an **existing** SQL Server schema. The database is the source of truth:
`database/*.sql` are the real `CREATE TABLE` scripts and are **read-only reference**. Never
generate migrations or alter the schema; write code that matches what is already there, and follow
the house patterns rather than inventing one per feature.

```
database/          # schema reference (read-only)
spec/              # conventions + feature specs + UI mockups
docs/designs/      # approved feature designs — the plan a branch implements
TODOS.md           # deferred work, one entry each, with why it was deferred
src/CMS.API/       # .NET 9 Web API, Dapper (NO Entity Framework), port 5000
src/CMS.API.Tests/ # xUnit
src/CMS.NG/        # Angular 20 standalone + PrimeNG 20, port 4200
```

## Commands

```powershell
dotnet run --project src\CMS.API   # -> http://localhost:5000/swagger
dotnet test

cd src\CMS.NG
npm start                          # -> http://localhost:4200
npm test -- --watch=false --browsers=ChromeHeadless   # bare `npm test` never returns
```

Toolchain traps — the fix here, the reasoning in `spec/conventions/environment.md`:

- Build fails **MSB3027** → a running `dotnet run` holds the exe. Stop it, or `-p:OutDir=<scratch>\`.
- `node` not found → prefix `$env:Path = "C:\Program Files\nodejs;$env:Path"`. Never edit the
  system variable; it is already correct.
- Leave the `global.json` SDK pin alone — SDK 10 is installed and would retarget to `net10.0`.
- The connection string lives in `src/CMS.API/appsettings.json` and nowhere else.

## Rules that hold everywhere

The ones that cost data or a rewrite when missed. Each is the trip-wire only — `→ backend §X` and
`→ frontend §X` name the section of `spec/conventions/{backend,frontend}.md` that carries the
reasoning, the worked example and the cases these lines flatten.

- **Dapper only. No EF, ever.**
- **Read the `CREATE TABLE` before assuming the key shape.** `pkid int IDENTITY` is common but not
  universal — `AppRole` has a string PK, `PublishStatus` a non-IDENTITY `tinyint`. Keys are always
  immutable on edit. → backend §Primary keys
- **Never let the database be the thing that refuses a destructive write.** Project child counts in
  `{Table}Sql.SelectBase`, read the record in the controller, `409` when one is non-zero — error
  547 is not the backstop. **A projected count nothing reads is not a guard**, and a repository
  that deletes its own junction rows first has switched 547 off, so the `409` is all that is left.
  → backend §Deleting a row other tables reference
- **Never PUT a list row straight back.** List and `query` responses carry the n-n key arrays empty
  and the repositories rewrite junction tables from the request, so the write silently clears the
  relations. Re-read with `GET /{table}/{key}` first. → backend §Core rules
- **Every Insert / Update / Delete writes a `RowAudit` row, on the same transaction**, from the
  repository through `IRowAuditWriter` — an update reads the "before" first, or the changed-column
  list is a guess, and a rolled-back change must leave no trail claiming it happened. **Every
  detail and form page renders `RowAuditBadge`**, which takes the table name and the record's
  **pkid** — the surrogate key, even where the operator's key is a string.
  → backend §異動紀錄, frontend §異動紀錄
- **Credentials leave the server in exactly one shape: none.** `AuthSql.SelectCredential` is the
  only query that **returns** `PasswordHash` (`UpgradePasswordHash` names the column, but to write
  it and to guard the write); keep it out of every other projection, response model and JWT
  payload. The client never hashes. **`PasswordHasher.Hash` is the only thing that writes a
  hash** — a salted, iterated PBKDF2 composite that carries its own parameters, so it needs no
  schema change; `Sha256Hex` survives only to verify rows written before it, which
  `AuthController` rewrites on the next successful sign-in. Never store its output.
  → backend §Credentials
- **Every endpoint needs a token; `AuthController` is the only exception.** Authorization is a
  global `FallbackPolicy`, so a new controller is protected by omission — never add
  `[AllowAnonymous]` to reach one. Hiding a menu by role is presentation; the API is what refuses.
  → backend §Authorization
- **A 系統管理 Admin controller carries `[Authorize(Policy = AuthorizationPolicies.Admin)]`.** The
  fallback policy only asks whether *somebody* is signed in, so a controller that administers
  accounts, roles or publish statuses says so itself — and `AppUsersController` additionally
  refuses a caller rewriting their **own** `RoleIds` or resetting their **own** password, because
  holding the role is not licence to hand yourself another one. **Gating the controller is not the
  whole job — check `LookupsController` for the same data**, where `app-users` and `app-roles`
  carry the policy on the *action* because the rest of that controller belongs to every operator;
  it served the account roster to any token holder until it did. The Angular side mirrors all this
  with `adminGuard` so nobody lands on a page that can only 403; the guard is courtesy, the policy
  is the protection. **Gating a page also moves the front door** — a constant `redirectTo` in
  `app.routes.ts` aimed at a now-gated route strands every other operator on a 403 the moment they
  sign in, which is why `''` and `**` go through `landingRedirect`.
  → backend §Authorization, frontend §Route guards
- **`MapInboundClaims` is `false`, and must stay false.** The default inbound map renames `role` to
  its WS-Federation URI while `RoleClaimType` says `role`, so every `RequireRole` policy matches
  nothing and refuses **everyone**, including real administrators — no exception, no log line,
  nothing to grep for. Tidy that line away and the whole Admin policy fails closed in silence.
  → backend §Authorization
- **Revocation means disable or delete the account, not remove the role.** `TokenFreshness` refuses
  a token on three conditions — the `AppUser` row is gone, `IsActive` is 0, or the token predates
  `PasswordUpdatedTime` — reading them as one nullable row per request, where a `null` row *is* the
  "deleted" answer. Role claims are stamped at login and are not re-read, so a removed role lasts
  until the token expires; that is deliberate and pinned by a test. → backend §Deprovisioning
- **An unhandled exception is answered once, and it says nothing.** `ExceptionHandlingMiddleware`
  logs in full server-side and returns one fixed `ProblemDetails` 500 — no stack trace, SQL text or
  connection details. It only ever *catches*, so every status you *return* passes through: it is
  the net under a bug, not a substitute for the delete guard above.
  → backend §Unhandled exceptions
- **The PDF engine is the operator's browser** — a print-only component plus `@media print` driven
  by `window.print()`, no PDF library, no new dependency. **Print margins belong in the page box**
  (`@page` cannot live in a component stylesheet), never in the content, where they exist on page 1
  and nowhere else. Producing the same file *outside* a browser needs a server renderer and is a
  different feature. → frontend §Print / PDF
- **Language split:** UI labels and validation messages are Traditional Chinese, usually paired
  with the English entity name (`角色 AppRole`). Code, identifiers, comments and commit messages
  are English.

## Reference

| Read | Before you |
|------|------------|
| `spec/conventions/backend.md` | touch anything under `src/CMS.API/` |
| `spec/conventions/frontend.md` | touch anything under `src/CMS.NG/` |
| `spec/conventions/testing.md` | write or change a test |
| `spec/conventions/environment.md` | fight the toolchain |
| `spec/code-gen.convention.md` | scaffold an entity — file layout, endpoint shapes, column types, and where `/crud` disagrees with this repo |
| `spec/sample1.spec.md` (Course) | need a worked example — FKs, n-n, date ranges, copy action |
| `spec/sample2.spec.md` (SkillTrain) | need a simpler one — n-n with `DisplayOrder` |
| `spec/feature-spec.template.md` | write a new feature spec |
| `spec/{sub-system}/{Table}.md` | change a built entity (e.g. `spec/admin/PublishStatus.md`) |
| `spec/custom/{Feature}/` | build a feature that ships its own spec and mockups |
| `docs/designs/{feature}.md` | pick up a branch that carries one — it is the approved plan, gate decisions included |
| `spec/ui-sample-*.png` | build a list / view / edit / add page — **style only**, the data is illustrative |

A feature under `spec/custom/` overrides the house page layout where the two disagree — that is
what a custom spec is for. `spec/promotion/FeaturedPromoItem.md` is the worked example; list your
deviations the way it does.

The `/crud` skill scaffolds an entity: schema → spec → stop for confirmation → both sides plus
tests. **Where it conflicts with this file, this file wins** — `spec/code-gen.convention.md` opens
with the specific disagreements (Moq, `p-toolbar`, the badge, the audit writer) and what to do
instead.

## gstack

Browsing goes through **`/browse`** (a gstack skill), always. Do not use the
`mcp__claude-in-chrome__*` tools — not to open a page, not to screenshot one, not as a fallback
when `/browse` is inconvenient. One browser path, so a page that renders for you renders the same
way next session.

Re-run `~/.claude/skills/gstack/setup` after every `git pull` of gstack — on Windows the skills are
file copies, not symlinks, so they do not track the repo. The installed skill list is injected into
every session by the harness; it is not duplicated here, because a copy on disk goes stale.
