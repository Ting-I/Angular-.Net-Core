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

## Wiring

- Path aliases: `@env`, `@env/*`, `@app/*`, `@core/*`, `@features/*`, `@layout/*`.
- **No dev-server proxy.** The API base URL comes from `@env`; `environment.development.ts` is
  swapped in by the `development` build configuration's `fileReplacements`.

## Sidebar

The nav lives in the root `App` component (`src/app/app.ts` `navGroups`, rendered by `app.html`),
not a separate layout component. Eight groups exist to match the UI mockup; three carry items:

| Group | Items |
|---|---|
| `系統管理 Admin` | `角色 AppRole`, `發布狀態 PublishStatus`, `使用者 AppUser` |
| `課程管理 Course` | `原廠 Partner`, `課程群組 CourseGroup`, `課程 Course` |
| `首頁管理 Home` | `上稿作業 FeaturedPromoItem` |

The other five have empty `items` arrays as placeholders. When you add a feature, add its entry to
the right group and extend `app.spec.ts` accordingly. `expandedGroups` defaults to `系統管理 Admin`
alone, so a spec that asserts on another group's items must `toggleGroup` it open first.
