# TODOS

## CMS.NG

### Print padding for pages other than the course sheet

**What:** Give every non-sheet page a print-time inner margin, e.g. `@media print { .app-main:not(:has(.has-sheet)) { padding: 14mm 16mm; } }` in `src/CMS.NG/src/app/app.scss` (or a `body.print-plain` class toggled by the shell), so a Ctrl+P on the courses list or a form does not print flush to the paper edge.

**Why:** The 課程簡介 PDF feature (design: `docs/designs/course-sheet-pdf.md`, eng review decision 15B, `dec-38974b78`) sets the global `@page { margin: 0 }` because a zero page margin is the only CSS lever that stops Chrome/Edge stamping the internal CMS URL onto a customer document. The course sheet supplies its own 14mm/16mm padding; every other page now prints edge to edge. Before that change those pages printed one clipped viewport, so this is still a net improvement, but the missing margin is a visible side effect the first time someone prints a list.

**Context:** The print rules live in `app.scss` (`@media print { .app-shell { display: block; height: auto } ... }`) and `styles.scss` (`@page`). The sheet's padding is on `course-sheet.scss` `:host` under `@media print`, so a global `.app-main` padding must exclude the page that hosts the sheet (`CourseDetail` sets a `has-sheet` host class when a course is loaded) or the two paddings stack. Verify on a real print preview of `/courses` and a form page; the QA test plan for the feature already lists "Load `/courses` → Ctrl+P" as a critical path.

**Effort:** S
**Priority:** P3
**Depends on:** The course-sheet PDF feature (branch `feature-course-pdf`) merged; a real multi-page print preview seen once.

**Update (2026-09-09, /autoplan Phase 2 D13):** the cause is being removed inside the feature — `@page { margin: 0 }` is now scoped to `body.print-sheet` (toggled in `beforeprint`/`afterprint`), and every other page keeps `@media print { .app-main { padding: 14mm 16mm } }`. Keep this entry as the record and to verify the other pages after the feature lands; close it once a print preview of `/courses` shows correct margins.

**Closed (2026-09-09, /qa):** a real print preview of `/courses` shows the full list paginated at
14/16 mm, not one clipped viewport. What remains there is a different problem, filed as its own entry
below (the table's columns are clipped horizontally). The margin mechanism also changed during that
QA pass: see the entry below and `docs/designs/course-sheet-pdf.md`.

**Update (2026-09-09, implemented):** shipped, with one correction to D13 — a class cannot scope `@page` (there is no descendant selector in a page context, eng review E2), so the mechanism is a **named page**: `@page sheet` claimed by `body.print-sheet app-course-sheet { page: sheet }`, while unnamed pages use `@page { margin: 14mm 16mm }`. Nothing is left to build here; what remains is one look at a real print preview of `/courses` and a form page, which is on the T5 QA list. Close the entry after that.

**Correction (2026-09-09, /review):** the line above originally said `.app-main` also carried `padding: 14mm 16mm` under print, "dropping to 0 only under the sheet's page, so the two never stack". They did stack, on every page but the sheet's: the page box gave `/courses` 14/16 mm and the block padding added 14/16 mm more to page 1 only, because padding does not clone onto the next page fragment. That is the same first-page-deep, second-page-shallow shape the sheet's own margin work exists to remove. The padding is gone; margins now live in the page boxes alone, which is the rule CLAUDE.md states. The print preview of `/courses` on the T5 list is what confirms it.

### Bulk course-sheet export from the courses list

**What:** Let an operator select several courses on `/courses` and produce one PDF containing a 課程簡介 sheet per course.

**Why:** Sales sends more than one course per deal; today that is one navigation and one print dialog per course. Raised as expansion candidate E2 in the /autoplan CEO phase and deferred there.

**Context:** The `CourseSheet` component is a leaf with inputs only, so N sheets in one document is a template loop, not a rewrite. The blocker is data: the list endpoint returns `certificationPkids` empty (house rule: only `GET /{table}/{key}` fills the n-n arrays), so a bulk export needs N detail fetches in the browser, or a new endpoint. Note from the CEO phase (A6): anything that generates the PDF *outside* an operator's browser (emailing it, a buyer-facing URL, a scheduled catalog) needs a server renderer such as Playwright on IIS — the current browser-print approach does not carry over.

**Effort:** M
**Priority:** P3
**Depends on:** The single-course sheet shipped and its field list signed off by an operator; someone actually asking for multi-course sends.

## CMS.API

### A durable sink for the 課程簡介 export log, and a retention line to go with it

**What:** Configure a log sink that survives a restart (Windows EventLog provider, or a rolling file
via Serilog) and state how long its entries are kept, so `POST /api/courses/{id}/sheet` can answer
"was this used, and by whom" days later rather than minutes later.

**Why:** Final Gate decision C3 added that endpoint to answer *which sheet did we send in March* and
*is this button used at all*. It emits one structured `Information` line — and this application
configures log **levels** only: no Serilog, no file or EventLog provider, and under IIS the ASP.NET
Core Module's stdout log is off by default. So today the line reaches whatever console the host has
and nothing retains it, which the endpoint's own summary and the 異動紀錄 section of
`spec/conventions/backend.md` both now say out loud. The endpoint and its tests are in place; only
the sink is missing.

**Context:** The line names a person (operator `userId` + 使用者名稱), so this is a retention and
privacy decision as much as a plumbing one — decide the period before turning a sink on, and write it
in `backend.md` next to the description of what the line contains. Note also what the record *cannot*
say: the operator picks Save or Cancel inside the browser's print dialog, invisible to the page, so it
records a print **request**. If "what did we send" ever needs a real answer, that is a stored artifact
and a server renderer, not a log line.

**Effort:** S (EventLog) / M (Serilog + retention policy)
**Priority:** P3
**Depends on:** Somebody actually asking the usage question; a retention period agreed.

## CMS.NG

### A Playwright print check for the 課程簡介 sheet

**What:** One end-to-end test that prints the sheet with `page.pdf()` and extracts the text, asserting
that the Traditional Chinese comes back as real text (not an image), that no excluded field appears,
and that the page count and margins are what they should be.

**Why:** Karma cannot see print media, so the 30-odd unit paths around this feature cover the
lifecycle, the filename, the warnings and the field allow-list — and **none** of the feature's visible
output. Margins, page-2 layout, the browser's header/footer stamp, font selection and page breaks are
manual QA (T5) today, which makes them a recurring cost after every Chrome or Angular upgrade rather
than a one-off — and the 2026-09-09 pass proved the point by finding a clipped page 2 that 624 green
unit specs had nothing to say about.

**Context:** Recorded as eng review E15 and deliberately deferred: it is new test infrastructure the
repo has none of (`npm test` is Karma only), and this machine's Chromium bootstrap is broken, so it
would be a detour inside a one-file feature. The assertions to write are in the design doc's Success
Criteria; the field allow-list to assert against is in `spec/course/Course.md`.

**Effort:** M
**Priority:** P2
**Depends on:** A working Chromium bootstrap; someone deciding Playwright is the repo's E2E tool.

### The courses list prints only its first five columns

**What:** Give `/courses` a print stylesheet so a Ctrl+P produces the whole table: unclip the
horizontal scroller under `@media print`, let the columns wrap or shrink to fit A4 landscape, and hide
the scrollbar the PDF currently draws.

**Why:** Found by /qa on 2026-09-09 (ISSUE-003 in `.gstack/qa-reports/qa-report-localhost-4200-2026-09-09.md`).
Margins and pagination are right now, but only 主代碼 · 顯示順序 · 簡介代碼 · 科目代碼 · 課程名稱 reach
the paper — 原廠, 課程群組, 上架狀態, 上架/下架日期, 時數 and 定價 are cut off, and 課程名稱 is clipped
mid-word. An operator printing the list to check it against something gets a document missing the
columns they were checking.

**Context:** Not a regression — before this branch the page printed one clipped viewport, and the
design doc puts "print tuning for other pages (e.g. the courses list table)" under NOT in scope. The
scroller is PrimeNG's `p-table` wrapper; the fix is a `@media print` block in `course-list.scss`
plus, probably, `@page { size: A4 landscape }` scoped to a named page the way the 課程簡介 sheet
claims its own.

**Effort:** S
**Priority:** P3
**Depends on:** Someone actually printing the list. Evidence: `.gstack/qa-reports/screenshots/20-list-print.jpg`.

### Sample ten courses' long-text copy before the field list is signed off

**What:** Read 教材 / 課程大綱 / 考試說明 across ten real courses and decide what belongs on a
customer sheet.

**Why:** Two findings from the /qa pass, both data rather than code. Course 66's 課程目標 carries
「總價值超過NT$70,000元」, 「原價NT$6,850」 and 「原價USD 950」 — so a sheet whose whole design decision
C4 was "no price on the document" prints prices anyway, because an operator typed them into a
long-text column. And its 考試／認證說明 holds inline markup with no block structure, so it renders as
one underlined run-on line that reads like a broken link.

**Context:** This is design-doc item A5, now with evidence. It belongs with the T6 operator sign-off
(due 2026-09-13), not with the component: the sheet renders faithfully what the column holds.

**Effort:** S (an afternoon of reading, with sales)
**Priority:** P2
**Depends on:** The operator conversation that also closes Open Questions 1 and 3.

## CMS.API — from the 2026-09-09 /cso security audit

Report: `.gstack/security-reports/2026-09-09-163855.json`. The two CRITICAL and one HIGH finding it
raised are fixed. What follows is what the audit found and deliberately did **not** report as a
finding, because each is a real weakness that is currently unreachable — every one becomes a
genuine finding the moment this stops being a loopback-only deployment.

### Swagger is served before authentication runs

**What:** Guard `app.UseSwagger()` / `app.UseSwaggerUI()` on the environment, or move them behind
the authentication middleware.

**Why:** They are registered at `src/CMS.API/Program.cs:121`, ahead of `app.UseAuthentication()` at
line 130. Swagger's middleware terminates the request when the path matches, so it never reaches
the authorization middleware and the global `FallbackPolicy` never applies: `/swagger/v1/swagger.json`
hands an unauthenticated caller the complete map of 61 endpoints, their routes and their DTO shapes.

**Context:** Suppressed from the audit report because `launchSettings.json` binds
`http://localhost:5000` and nothing else, so today there is no unauthenticated caller who can reach
it. The Swagger UI is also how the API is worked on, so this is not "delete it" — it is "not in
production", which is `if (app.Environment.IsDevelopment())`, the shape the template ships with.
Note that `TestApiFactory` runs as `Staging`.

**Effort:** S
**Priority:** P3
**Depends on:** A deployment target that is not loopback.

### The SQL connection is unencrypted

**What:** Drop `Encrypt=False` (and, with a real certificate, `TrustServerCertificate=True`) from
the connection string in `src/CMS.API/appsettings.json:10`.

**Why:** The JWT signing key is read from `SysConfig` on **every authenticated request** — that is
deliberate, so rotating the row rotates validation — and password hashes are read on every login.
With `Encrypt=False` both cross the SQL wire in cleartext. Anyone who can see that traffic can mint
tokens for any account.

**Context:** Suppressed from the audit report because `Server=.\SQLEXPRESS` is a local instance and
there is no network hop to observe. It is a one-line change whose cost is a certificate the local
instance does not currently have, which is why it is filed rather than done.

**Effort:** S (the string) / M (the certificate)
**Priority:** P2, and P0 the day the database moves off the app host.
**Depends on:** A SQL Server with a trusted certificate.

### `POST /api/auth/login` has no throttling or lockout

**What:** Count failed attempts per UserId (and per source address) and refuse for a growing
interval; `AppUser` has no column for it, so this is a cache or a small table, not a schema change.

**Why:** Nothing limits guessing. The audit's password-hashing finding is now fixed — stored hashes
are salted PBKDF2 at 210,000 iterations — which raises the cost of an *offline* attack enormously,
but an online one is bounded only by how fast the API answers, and `PasswordPolicy` allows an
8-character password.

**Context:** Excluded from the audit report by the /cso rule that drops rate-limiting findings, not
because it is unimportant: it is the other half of the brute-force story and the natural companion
to the hashing change. Whatever is built must keep the single generic 401 — a lockout that answers
differently from a wrong password would undo the account-enumeration guarantee `AuthController`
exists to keep.

**Effort:** M
**Priority:** P2
**Depends on:** Nothing.

### Every new account gets the same password, and nothing forces a change

**What:** Have `POST /api/app-users` and `POST /api/app-users/{id}/reset-password` issue a one-time
value instead of the shared SysConfig `defaultPassword`, and make the account change it at next
sign-in.

**Why:** One static value is the credential for every account until its operator changes it, and
`AppUser` records no "must change" state, so nothing ever makes them. The audit's Finding #2 —
any authenticated user resetting any account to that value — is fixed by the Admin policy plus the
self-reset guard, but the shared default itself is untouched: an administrator can still park any
account on a password every other administrator knows.

**Context:** `AppUsersController.HashDefaultPasswordAsync` is the one place the default is read.
Forcing the change needs somewhere to record it; `PasswordUpdatedTime` is already read by
`TokenFreshness` and would need care, so this is design work rather than a patch.

**Effort:** M
**Priority:** P2
**Depends on:** A decision on where "must change password" lives, given the read-only schema.
