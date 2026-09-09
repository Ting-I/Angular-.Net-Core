# Frontend conventions (`src/CMS.NG`)

Read before touching anything under `src/CMS.NG/`. The short version lives in `CLAUDE.md`; this
file is the detail.

## Layout

```
src/app/core/models/      # interfaces mirroring the API models
src/app/core/services/    # one service per entity + lookup.service.ts
src/app/core/utils/       # date.util.ts and friends
src/app/features/{table-plural}/{table}-list|-detail|-form/
```

- Standalone components only; routes are lazy `loadComponent` in `app.routes.ts`, `/new` before
  `/:id`.
- The list / detail / form triple is the default, not a requirement. A feature whose custom spec
  calls for something else builds what the spec says — `featured-promo-items` has a list and an
  inline form and no detail page at all. Record the deviation in the generated feature spec.

## Writes

- **Never PUT a list row straight back.** The list and `query` endpoints return the n-n key
  arrays empty — only `GET /{table}/{key}` populates them — and the repositories rewrite their
  junction tables from whatever the request carries, so a write built from a list row silently
  clears the relations. Re-read the record first. `course-list.ts` `saveCell` is the worked
  example; `CourseRepository.SyncJunctionsAsync` is the code that does the clearing.
- **Every successful write tells the operator.** `messageService.add` with the fixed verb as
  `summary` (`已儲存` / `已刪除` / `已複製`) and the record's **own identifying text** as
  `detail` — never generic wording. `<p-toast />` stays a bare tag; nothing sets `life` or `key`.
  Where the write leaves the screen looking identical, the toast is not enough on its own — see
  **Inline cell editing** below for the highlight that has to go with it.

## List pages

- Persist filter / sort / page to session storage under `{table}-list-filters` / `-sort` / `-page`.
  A page with nothing to sort or paginate persists only the keys it actually has.
- Filter UI is a `p-drawer`; `p-select` inside it always needs `appendTo="body"`.
- The 搜尋條件 button's `[badge]` shows the **number** of applied filters, not a `!` marker. Count
  a tri-state `p-select` as active whenever it is neither `null` nor `undefined` — a `false`
  selection is a real filter, so falsy checks silently drop it.
- `p-confirmDialog` has **no `escape` input** in PrimeNG 20; it always renders `message` through
  `[innerHTML]`. Markup in the message works, so HTML-escape any record text you interpolate.

### Inline cell editing

`course-list` is the pattern; `spec/course/Course.md` → **Inline editing on the list** has the full
column table. A second entity that wants it should lift the pieces out rather than copy them.

- **Not `pEditableColumn`.** PrimeNG's directive opens the cell from its own `click` host listener
  with no input to change that, and single click belongs to sorting, paging and the row actions.
  A `(dblclick)` on the `<td>` plus an `@if` swapping display for editor is the shape instead.
- A module-level `Record<EditableField, …>` is the single source of the editable set: editor kind,
  the label the messages use, `maxLength`, decimal places and the SQL upper bound. `startEdit`
  refuses any field that is not a key of it, so the read-only guard does not live in the template
  alone. Keys and FK lookup labels stay out of it — the Edit form owns the relations.
- Blur persists, `Enter` commits the text and number editors, `Escape` drops the draft. An
  unchanged value closes without calling the API.
- **Validation failure keeps the cell open** with the message rendered under the editor; a **save
  failure closes it**, which is the revert — never write the row optimistically and there is
  nothing to roll back.
- **A successful in-place save has to announce itself.** The response replaces the row and the
  editor closes in the same tick, so the cell ends up showing the text the editor was already
  showing — the save completes with nothing on screen changing. Toast `已儲存` **and** hold a brief
  highlight on the cell: the toast says it was written, the highlight says which cell. Clear the
  highlight when any editor opens, or a cell saved twice in a row flashes only the first time.
- **Show the round trip.** Disable the editor and overlay a `pi-spin` spinner while the request is
  on the wire, and make Escape inert for that window — a save already sent cannot be cancelled.
- **`p-select` and `p-datepicker` move focus into their panel when it opens**, which fires the
  editor's blur. Guard `commit()` with a flag set from `(onShow)` and `(onHide)` / `(onClose)`, or
  the row saves the moment the operator opens the picker.
- The save is `GET /{table}/{key}` then `PUT` — see **Writes** above for why the re-read is not
  optional.

## Forms

- Reactive Forms, with `forkJoin` for parallel lookup + record loads. Skip the `forkJoin` when the
  entity has no FK lookups to fetch alongside the record.
- A form rendered inside another component rather than on its own route takes its record and its
  position through `input()` and reports back through `output()` — see
  `featured-promo-item-form`, which the list renders inside the grid row it is editing.
- **A page with an action bar pins it.** `course-form` and `course-list` both do: `.page-header`
  wrapped in a sticky `.sticky-toolbar` carrying the page background, the gap down to the content
  below, and a bleed out over `.app-main`'s padding. The mechanics live in one place,
  `src/styles/_sticky-toolbar.scss`, as a mixin each page `@include`s — a mixin rather than a
  global class because the bar zeroes the `margin-bottom` of the `.page-header` it wraps, and a
  global rule loses that specificity tie to the component stylesheet that declares `.page-header`. Two things bite here, and both look fine in a screenshot of an
  unscrolled page:
  - `.app-main` is a scrollport either way (`overflow-x: auto` computes `overflow-y` to `auto`),
    and a sticky child of one that never scrolls never sticks. It scrolls because `.app-shell` is
    `height: 100dvh`.
  - A scroll container clips at its padding box but pins sticky children to its **content** box,
    so the padding between them shows the page scrolling past **above** the pinned bar. Cancel it
    with `margin-top: calc(-1 * var(--app-main-padding))`, the same value as `padding-top`, and
    `top` set to the negated value.
  Assert both against a real scrolling element **with padding**, not just on the computed style —
  a spec that only reads back `position: sticky` passes on a bar that pins in the wrong place.
- An FK the operator knows by a code rather than a key is entered as text and resolved before the
  save: `p-autoComplete` fed by a search lookup, plus an exact by-code lookup whose `404` means
  "no such code" and blocks the save. Pre-fill only the text fields that are still empty, so an
  edit never has its wording overwritten.

## Dates

`core/utils/date.util.ts` — use these rather than hand-rolling:

- `toIso` / `fromIso` convert `Date` ↔ `yyyy-MM-dd` in **local** components. Never
  `toISOString()`: it converts to UTC first, which lands a UTC+8 operator on the previous day.
- `addYears`, `addDays`, `startOfWeek`. `startOfWeek` returns the Monday, so it rotates
  JavaScript's Sunday-as-0 numbering; Sunday belongs to the week that started six days earlier.

## QR codes

Go through `core/utils/qr-code.util.ts` — `qrPngDataUrl` / `downloadDataUrl` wrap
`qrcode-generator`, which only yields a module matrix and a GIF. The util draws the canvas, so
the `<img>` and the saved file are the same PNG bytes. `course-detail` is the worked example.

## Auth

- The session lives in **session storage**, never local storage, under `auth-profile`: the whole
  of what the API returned (`userId`, `userName`, `accessToken`). It dies with the tab, so a shared
  machine does not hand the next person a live token. `AuthService` owns the key; nothing else
  reads or writes it.
- **The roles come out of the token, not a second API call.** `AuthService.roles()` decodes the
  JWT payload's `role` claim, which serializes as a bare string for one role and an array for
  several — both shapes have to be accepted.
- `authInterceptor` attaches `Authorization: Bearer <token>` to requests whose URL starts with
  `environment.apiUrl`, and only those — the token belongs to this API. A `401` coming back clears
  the session and returns to `/login`; the login call's own `401` is exempt, or the redirect would
  wipe the 帳號或密碼錯誤 the Login page is about to show.
- `authGuard` is attached once as `canActivateChild` on the empty-path parent in `app.routes.ts`,
  not repeated per route, so a route added later is guarded by default. It returns a `UrlTree`
  rather than navigating. `/login` sits outside that parent and is the only public route.
- **Hiding a menu is presentation, not protection.** The API decides who may call what; the
  sidebar's `requiresRole` only keeps an unusable menu off the screen.
- 個人資料 My Profile (`/profile`, `features/auth/profile`) is the operator's own account: 使用者代碼
  and 角色 rendered read-only, 使用者名稱 editable. It reads all three from `AuthService` — the key
  and the name from the stored session, the roles from the token, per the rule above — and saves
  through `AuthService.updateProfile`, which `PUT`s `{ userName }` alone. That folds the new name
  back into the session so the top bar follows without a re-login; the token is left as it was, so
  its now-stale `userName` claim is one more reason nothing may read a name from the claims. The
  page sits under the guarded parent with no role gate, and the top bar's `.topbar-link` is the way
  in.
- 變更密碼 Change Password is a **second `<form>`** on the same page, not another section of the
  first: forms do not nest, and each write has its own submit button, so saving a name cannot send
  a password and vice versa. It posts `{ currentPassword, newPassword, confirmNewPassword }` to
  `POST /api/auth/change-password` through `AuthService.changePassword`.
  - **A successful change ends the session and returns to 登入.** The API has already stopped
    honouring the stored token by then — `TokenFreshness` refuses anything signed before
    `AppUser.PasswordUpdatedTime`, see `spec/conventions/backend.md` — so the client is tidying up
    after a token that is already dead, not doing the revoking. Keeping it would only turn the
    next click into a 401. `changePassword` drops it in a `tap`, the way `updateProfile` folds the
    new name in from the same side: doing it in the service rather than the page means no future
    caller can forget. Navigation stays the caller's business, exactly as it is for `logout`. A
    **failure** leaves the session alone — nothing changed, and the operator is mid-retry.
  - **The Profile page does not toast the success.** It is torn down by the navigation, so the
    message would flash and vanish. It navigates to `LOGIN_ROUTE` with
    `{ [LOGIN_REASON_PARAM]: PASSWORD_CHANGED_REASON }` instead, and `Login` renders
    密碼已變更，請使用新密碼重新登入。 from that parameter — otherwise the operator sees an
    unexplained sign-out. Both constants live in `core/guards/auth.guard.ts` beside `LOGIN_ROUTE`,
    which the guard and the interceptor already share; `Login` reads the parameter once at
    construction and clears the notice on the next submit so it cannot sit above a fresh
    帳號或密碼錯誤.
  - **The browser never hashes anything.** Three plaintext fields go up, `204` and an empty body
    come back. A `PasswordHash` appears nowhere in `core/models/auth.model.ts`, and the fields are
    cleared the moment the API answers.
  - **Passwords are sent verbatim, never trimmed.** Leading or trailing whitespace is part of a
    password; trimming it the way 使用者名稱 is trimmed would hash something the operator did not
    type.
  - `passwordComplexity` in `profile.ts` mirrors the server's `PasswordPolicy` — 8 characters and
    3 of the 4 classes, with "symbol" meaning anything that is not upper, lower or a digit, so a
    space or a 中文字 counts. `PASSWORD_RULE_MESSAGE` is word for word what the API answers with,
    which is what lets the same sentence serve as the hint under 新密碼 before the rule is broken
    and as the error once it is. Keep the two copies reading alike; the client one is convenience,
    and the API is what actually refuses.
  - The mismatch is a **group** validator, not a control one: neither field is wrong on its own,
    and it stays quiet until the confirmation has been filled in rather than shouting at somebody
    halfway through typing it.
  - A rejection shows the API's `ProblemDetails` `title` as the toast detail — 目前密碼錯誤, the
    complexity rule, or the mismatch — rather than a locally composed message, so the two sides
    cannot drift apart. The fields are left as they are for the retry, and the operator stays on
    the page. Only the failure toasts — this is the one write on the page whose success is
    announced somewhere else entirely, because the success is also a sign-out.
- **The name in the top bar is session state, not a per-page read** — it comes from the `auth-profile`
  entry written at login and is never re-fetched. So *any* page that renames the signed-in operator
  has to say so, or the shell shows the login-time name until the next sign-in. 使用者 AppUser is
  the case that already exists: an administrator editing their own row writes `PUT /api/app-users`,
  which `AuthService` knows nothing about, so `AppUserForm.save()` calls
  `AuthService.syncUserName(userId, userName)` — a no-op for every other account, matching the key
  case-insensitively as the API does. Add the same call to any future path that writes `UserName`.
- In a spec, seed `sessionStorage` **before** the first injection — `AuthService` reads storage
  when it is constructed. `@core/testing/fake-jwt` builds a structurally valid unsigned token with
  whatever claims the test needs.

## Wiring

- Path aliases: `@env`, `@env/*`, `@app/*`, `@core/*`, `@features/*`, `@layout/*`.
- **No dev-server proxy.** The API base URL comes from `@env`; `environment.development.ts` is
  swapped in by the `development` build configuration's `fileReplacements`.

## Sidebar

The nav lives in the root `App` component (`src/app/app.ts` `navGroups`, rendered by `app.html`),
not a separate layout component. The shell — sidebar, top bar and `.app-main` — is only rendered
for a signed-in operator; signed out, `App` renders a bare `<router-outlet />` and the Login page
owns the viewport. The top bar is a grid row of `.app-shell` rather than a child of `.app-main`, so
it cannot scroll away and cannot disturb a page that pins its own toolbar inside `.app-main`.

Eight groups exist to match the UI mockup; three carry items:

| Group | Items |
|---|---|
| `系統管理 Admin` (`requiresRole: 'Admin'`) | `角色 AppRole`, `發布狀態 PublishStatus`, `使用者 AppUser` |
| `課程管理 Course` | `原廠 Partner`, `課程群組 CourseGroup`, `課程 Course` |
| `首頁管理 Home` | `上稿作業 FeaturedPromoItem` |

The other five have empty `items` arrays as placeholders. When you add a feature, add its entry to
the right group and extend `app.spec.ts` accordingly. `expandedGroups` defaults to `系統管理 Admin`
alone, so a spec that asserts on another group's items must `toggleGroup` it open first — and must
sign in as a user holding `Admin`, or that group is not rendered at all and nothing is expanded.

A group carrying `requiresRole` is dropped for a user whose token does not hold that role. Only
`系統管理 Admin` uses it today.

## 異動紀錄 — the history badge

`RowAuditBadge` (`core/components/row-audit-badge/`) renders one record's audit trail: a small
button labelled 異動紀錄 History with the most recent change beside it, opening a `p-dialog` that
lists the whole trail. **Every detail page and every form page carries one**, so add it when you
add a page — a record whose history is invisible is a record nobody can answer a question about.

- **It is the one component outside `features/`.** Every page renders it and it belongs to no
  entity, so it lives in `core/` beside the other cross-cutting pieces rather than under whichever
  feature happened to need it first. There is no `shared/` directory and this did not earn one.
- **It takes `tableName` and `pkid`, not the route.** `tableName` is the **database** table name —
  `Course`, `FeaturedPromoItem` — because that is what the writer stored. `pkid` is the surrogate
  key **even for a record the operator knows by a string**: 角色 AppRole and 使用者 AppUser are keyed
  on RoleId and UserId, but the trail is written against the pkid every table also carries. Passing
  the string key shows an empty history and looks like a record that has never been touched.
- **It goes at the start of the `.page-header`**, right after the `<h1>` — the house equivalent of
  the skill's `p-toolbar` `#start` slot, since there is no `p-toolbar` here. The header is
  `justify-content: space-between`, so the component's own `:host { margin-right: auto }` is what
  keeps it beside the title with the action buttons still at the end. That rule lives in the badge,
  not in each page, which is what makes it drop-in.
- **The fetch is an `effect` over the inputs, not `ngOnInit`.** A detail page knows its pkid only
  once the record has loaded and a form in 新增 mode never has one, so a key that arrives late still
  fetches and a key that never arrives never does. A null pkid makes no request at all.
- **Three states, and they are not the same state.** A trail, 尚無異動紀錄 No history for a record
  with none, and 異動紀錄無法載入 Unavailable when the fetch failed — the last must not read as the
  second, because "no history" is a claim about the record rather than about the request.
- **A page spec now has a second request to answer.** The badge fetches on every detail and form
  page, so those specs flush it in `afterEach` before `httpMock.verify()`:
  `httpMock.match((req) => req.url.endsWith('/rowaudit')).forEach((req) => req.flush([]))`. The
  badge's own behaviour is pinned in `row-audit-badge.spec.ts` and nowhere else.
