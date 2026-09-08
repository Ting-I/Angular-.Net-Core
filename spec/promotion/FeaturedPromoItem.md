# Build Spec for FeaturedPromoItem

- database schema: `.\database\promotion.sql` (the `CREATE TABLE` at lines 8–26, the unique index
  `IX_FeaturedPromoItem_UniqueDateLocSlot`, and the two outbound FK constraints at lines 168–176)
- source spec: `.\spec\custom\FeaturedPromoItem\FeaturedPromoItem.spec.md` plus the three
  `ui-*.spec.png` mockups in the same folder. **That custom spec wins over the house list/detail/form
  layout** — this feature is a week grid with inline editing, not a paginated table with separate
  routes. Every deviation from `code-gen.convention.md` is listed under **Deviations**.

`FeaturedPromoItem` is the home-page "上稿作業" schedule: for each training centre and each day, up
to three promotions are pinned in ordered slots. The operator works one Monday..Sunday week at a
time, one centre (tab) at a time, and keys each item by the promotion's `PromoCode` rather than by
its pkid.

---

## Summary

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **int IDENTITY(1,1)** — server-generated, immutable after create |
| Foreign Keys | `TrainingCenter_pkid` → `TrainingCenter.pkid` (smallint, NOT NULL), `Promotion_pkid` → `Promotion2.pkid` (int, NOT NULL). Both `FK_*` constraints are plain `WITH CHECK`, no cascade |
| Unique | `IX_FeaturedPromoItem_UniqueDateLocSlot` on (`ScheduleOn`, `TrainingCenter_pkid`, `Slot`) — one item per cell |
| Required Fields | every column: `ScheduleOn`, `TrainingCenter_pkid`, `Slot`, `Promotion_pkid`, `Topic`, `Description` |
| N-N Relationships | none |
| Primary-Foreign Links | none — nothing in the schema references `FeaturedPromoItem`, so DELETE needs no 409 guard |
| Query Filters | `trainingCenterPkid` (the active tab); `weekOf` (any date — server widens to Monday..Sunday) |
| Default Sort | `ORDER BY fpi.ScheduleOn ASC, fpi.TrainingCenter_pkid ASC, fpi.Slot ASC` |

---

## Localization

### Chinese Table Name

- FeaturedPromoItem: 上稿作業
- Description: 首頁精選活動排程，每日三個版位

### Chinese Column Names

- pkid: 主代碼
- ScheduleOn: 上稿日期
- TrainingCenter_pkid: 訓練中心
- Slot: 版位
- Promotion_pkid: 活動（顯示 活動代碼 PromoCode）
- Topic: 主題
- Description: 說明

Sidebar entry: `上稿作業 FeaturedPromoItem` under `首頁管理 Home` (the group already in the mockup;
the request said "首頁 Home", which is taken as that group rather than a new one).

---

## Required Fields

All six data columns are NOT NULL and are required on the request DTO:

- `ScheduleOn` — date
- `TrainingCenter_pkid` — smallint, `[Range(1, short.MaxValue)]`
- `Slot` — tinyint, `[Range(1, 3)]` (`FeaturedPromoItem.MinSlot` / `MaxSlot`)
- `Promotion_pkid` — int, `[Range(1, int.MaxValue)]`
- `Topic` — nvarchar(100)
- `Description` — nvarchar(300)

`pkid` is the ordinary `int IDENTITY` case: omitted on create, carried in the body on update.

---

## Foreign Keys

| Column | Target | Nav object | Lookup |
|--------|--------|------------|--------|
| `TrainingCenter_pkid` | `TrainingCenter.pkid` | `TrainingCenterLookup { Pkid, Name, AppKey, IsDefault }` | `GET /api/lookups/training-centers` — ordered `DisplayOrder, Name`; drives the tabs |
| `Promotion_pkid` | `Promotion2.pkid` | `PromotionLookup { Pkid, PromoCode, Topic, Description }` | `GET /api/lookups/promotions?keyword=` (autocomplete, `LIKE`, `TOP 20`, `PromoCode DESC`) and `GET /api/lookups/promotions/{promoCode}` (exact, 404 when missing) |

Both are INNER JOINs in `FeaturedPromoItemSql.SelectBase`, multi-mapped with `SplitOn = "Pkid,Pkid"`
exactly as `CourseSql` does.

The operator never sees `Promotion_pkid`. They type or pick a `PromoCode`; the form resolves it to a
pkid through the exact lookup before saving. `Topic` and `Description` are carried on the lookup so
a fresh item can be pre-filled from the promotion — only **empty** text fields are filled, existing
wording on an edit is never overwritten.

---

## Query

`FeaturedPromoItemQuery { short? TrainingCenterPkid; DateOnly? WeekOf }`

- `TrainingCenterPkid` → `fpi.TrainingCenter_pkid = @TrainingCenterPkid`
- `WeekOf` → `fpi.ScheduleOn >= @WeekStart AND fpi.ScheduleOn <= @WeekEnd`, where
  `FeaturedPromoItemSql.WeekOf(date)` returns the Monday..Sunday pair containing the date. .NET
  numbers Sunday as 0, so the offset is rotated: `((int)DayOfWeek + 6) % 7`. Both bounds inclusive —
  `ScheduleOn` is a `date`, so no end-of-day handling.

The client sends the Monday it is displaying, but any day in the week yields the same range, and
the tests cover every weekday plus month/year boundaries.

---

## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET    | `/api/featured-promo-items` | All records |
| POST   | `/api/featured-promo-items/query` | Filtered search (centre + week) |
| GET    | `/api/featured-promo-items/{id:int}` | Single record |
| POST   | `/api/featured-promo-items` | Create — **409** when the (day, centre, slot) cell is taken |
| PUT    | `/api/featured-promo-items` | Update (pkid in body) — 404 when missing, **409** on a cell clash with another row |
| DELETE | `/api/featured-promo-items/{id:int}` | Delete — 204 / 404 only |
| POST   | `/api/featured-promo-items/{id:int}/move-up` | The "-" action: slot n → n-1 on the same day and centre |
| POST   | `/api/featured-promo-items/{id:int}/move-down` | The "+" action: slot n → n+1 |
| GET    | `/api/lookups/training-centers` | Tab list |
| GET    | `/api/lookups/promotions?keyword=` | Autocomplete feed |
| GET    | `/api/lookups/promotions/{promoCode}` | Exact PromoCode → promotion (404 when unknown) |

### Slot uniqueness

The unique index would surface a duplicate as SQL error 547-style failure → 500. Instead the
controller calls `SlotTakenAsync(scheduleOn, centre, slot, excludePkid)` before INSERT and UPDATE
and returns `409 版位已被使用`. On update the row's own pkid is excluded so re-saving into the same
cell is not a conflict.

### Move (swap)

`MoveToSlotAsync(pkid, targetSlot)` runs in one transaction:

1. read the row's (ScheduleOn, TrainingCenter_pkid, Slot);
2. find the neighbour occupying `targetSlot` on the same day and centre, if any;
3. with a neighbour: park it on slot **0**, move the row to the target, move the neighbour to the
   vacated slot. Slot 0 is a value the UI never assigns, and the three-step dance is forced by the
   unique index — two rows can never hold the same slot even mid-statement;
4. without a neighbour: a single UPDATE.

The controller rejects a move off either end with `400 版位無法再移動` before touching the
repository, and returns the re-read row on success.

---

## Backend Models

```
Models/FeaturedPromoItem.cs          # + TrainingCenter / Promotion nav objects; MinSlot/MaxSlot consts
Models/FeaturedPromoItemRequest.cs   # all six columns with data annotations
Models/FeaturedPromoItemQuery.cs     # TrainingCenterPkid?, WeekOf?
Models/TrainingCenterLookup.cs
Models/PromotionLookup.cs
Repositories/FeaturedPromoItemSql.cs # SelectBase, SplitOn, DefaultOrderBy, WeekOf(), BuildWhere()
Repositories/IFeaturedPromoItemRepository.cs
Repositories/FeaturedPromoItemRepository.cs
Controllers/FeaturedPromoItemsController.cs
```

`ILookupRepository` / `LookupRepository` / `LookupsController` gained the three lookup methods.
`Program.cs` registers `IFeaturedPromoItemRepository`.

---

## Frontend

### Route

| Path | Component |
|------|-----------|
| `/featured-promo-items` | `FeaturedPromoItemList` — the week grid; editing is inline |

There is **no** `/new`, `/:id`, or `/:id/edit` route — see Deviations.

### Models

`core/models/featured-promo-item.model.ts` (`FeaturedPromoItem`, `FeaturedPromoItemRequest`,
`FeaturedPromoItemQuery`, `MIN_SLOT`, `MAX_SLOT`, `SLOTS`), `training-center-lookup.model.ts`,
`promotion-lookup.model.ts`. `scheduleOn` travels as `yyyy-MM-dd`.

### Services

`FeaturedPromoItemService` — `getAll / query / getById / create / update / delete / moveUp / moveDown`.
`LookupService` — `getTrainingCenters()`, `searchPromotions(keyword)`, `getPromotionByCode(code)`
(`encodeURIComponent`, since PromoCode is a string key).

`core/utils/date.util.ts` gained `addDays()` and `startOfWeek()` (Monday, local time).

### List (`featured-promo-item-list`)

- `p-tabs` across the top, one `p-tab` per training centre; the active tab is the
  `trainingCenterPkid` filter. Opens on the centre with `IsDefault`, else the first.
- Week navigator `« 3/16 – 3/22 »` stepping ±7 days, plus a 本週 button. The `weekOf` sent to the
  API is always the Monday.
- Seven `section.day` blocks headed `M/d (一)`…`(日)`, each a three-row table: slot number,
  action buttons, PromoCode, Topic, Description. Empty slots render as blank rows.
- Actions per row: `+` (move down) / `-` (move up), disabled at slot 3 / slot 1 and on empty slots;
  Edit (opens the inline editor — new item when the slot is empty); Copy and Delete on filled slots;
  Paste on empty slots while the clipboard holds a copied row.
- Copy is a page-local clipboard signal `{ promotionPkid, promoCode, topic, description }`. Paste
  opens the editor pre-filled — nothing is saved until 儲存.
- Delete goes through `p-confirmDialog` naming the day, slot and (HTML-escaped) PromoCode.
- Session storage: `featured-promo-item-list-filters` = `{ trainingCenterPkid, weekOf }`. No
  `-sort` / `-page` keys — there is nothing to sort or page.

### Form (`featured-promo-item-form`)

A standalone component rendered **inside the grid row** by the list (`@if (isEditing(...))`), not a
routed page. Inputs `item` (edit mode when set) and `draft` (`{ scheduleOn, trainingCenterPkid,
slot, promoCode?, promotionPkid?, topic?, description? }`); outputs `saved`, `cancelled`.

- Controls: `promoCode` (required, ≤30, `p-autoComplete` fed by `searchPromotions`), `topic`
  (required, ≤100), `description` (required, ≤300). The position (day / centre / slot) is fixed by
  the cell and is not editable in the form.
- Resolution: a selected suggestion, the 查詢 button, or 儲存 itself resolves the code to a
  `PromotionLookup`. A code already resolved (edit mode, pasted row, prior selection) is reused;
  otherwise `getPromotionByCode` runs first and a 404 marks the control `unknownPromoCode` and
  aborts the save. Only empty `topic` / `description` are pre-filled from the promotion.
- Save: POST with `pkid: 0` and the draft position, or PUT with the item's key and position.
  409 → 「此版位已有資料」 toast; the editor stays open.

---

## Tests

### Backend (`CMS.API.Tests`) — 61 new, 417 total

- `Controllers/FeaturedPromoItemsControllerTests.cs` — list ordering and nav objects; query with
  the centre filter, the one-week filter (Sunday-before and Monday-after excluded, every weekday
  selects the same week), and both combined; get 200/404; create 201 / ignores pkid / 409 on a
  taken cell / same slot on another centre; update 200 / same cell OK / 409 / 404; delete 204/404;
  move-up and move-down swap, empty-target move, 400 at both ends, same-day-and-centre only, 404.
- `Repositories/FeaturedPromoItemSqlTests.cs` — projection, joins, split-on, order; `WeekOf` for
  every weekday, Sunday-as-last-day, next Monday, month/year crossing; `BuildWhere` empty/null,
  centre, week bounds, both.
- `Controllers/LookupsControllerTests.cs` — training centres with `IsDefault`; promotion search
  (keyword, newest first, no keyword); PromoCode lookup found / trimmed / 404.
- `Fakes/FakeFeaturedPromoItemRepository.cs` mirrors the contract including the swap; the lookup
  fake gained the three methods.

### Frontend (`CMS.NG`) — 59 new, 389 total

- `featured-promo-item.service.spec.ts` — every method's URL and verb, including `move-up` /
  `move-down`.
- `lookup.service.spec.ts` — the three new lookups, including the `keyword` param and the encoded
  PromoCode route.
- `date.util.spec.ts` — `addDays`, `startOfWeek` (Monday, Sunday-as-last-day, local midnight).
- `featured-promo-item-list.spec.ts` — restore/default state and the query body; tabs, seven days ×
  three slots, cell placement; tab and week navigation with persistence; editor open (filled/empty),
  saved reload, cancel, closes on navigation; copy/paste; move routes, button enablement, failure;
  delete confirmation text with escaping, delete reload, failure.
- `featured-promo-item-form.spec.ts` — new mode (validation, autocomplete feed, selection pre-fill,
  查詢 resolve/unknown, save resolves then POSTs, unknown code blocks save, 409 toast, cancel);
  paste (pre-fill, saves without a lookup); edit mode (patch, PUT with key, re-resolve on changed
  code).
- `app.spec.ts` — the new item under `首頁管理 Home`.

---

## Deviations from `code-gen.convention.md`

| Convention | Here | Why |
|------------|------|-----|
| List = paginated, sortable `p-table` with a `p-drawer` filter and a 搜尋條件 badge | Tabs + week navigator + fixed 7×3 grid; no drawer, no badge, no paginator | The custom spec's mockups define the page. The filters (centre, week) *are* the tabs and navigator |
| Separate `-detail` and `-form` routes (`/new`, `/:id`, `/:id/edit`) | One route; the form is an inline child component rendered in the grid row; no detail page | Mockups `ui-update` / `ui-new` show the editor opening in place. A detail page would show nothing the row does not already |
| Session keys `-filters` / `-sort` / `-page` | `-filters` only | Nothing to sort or page |
| FK chosen from a `p-select` | PromoCode typed into a `p-autoComplete` and resolved to `Promotion_pkid` via the exact lookup | Spec: "Enter Promotion2.PromoCode, lookup then set Promotion_pkid" |
| Only `[Required]` / `[StringLength]` annotations | `[Range(1, 3)]` on `Slot`, `[Range(1, …)]` on both FK pkids | Slot is bounded by the UI and the swap logic; a 0 pkid would otherwise surface as a 500 from the FK |
| Copy action = server-side `POST /{id}/copy` (as Course) | Copy/Paste is client-side: Copy captures the row, Paste opens the editor pre-filled | The mockup's Paste lands in a *different* cell chosen by the operator, and nothing should be written until they press Save |
| `/crud` skill asks for Moq, `RowAuditWriter`, sticky `p-toolbar` | none of the three | Moq and `RowAuditWriter` do not exist in this repo. A pinned action bar arrived later, as `.sticky-toolbar` around a `.page-header` rather than a `p-toolbar` (`spec/conventions/frontend.md`); this page predates it |

Other decisions worth knowing:

- **Slot 0 as the swap parking value** is an invariant: nothing else may ever write slot 0.
- The `Promotion_pkid` FK is *not* re-validated server-side beyond `[Range]`; the client resolves the
  code first, so a stale pkid would surface as SQL error 547 → 500. Acceptable for an internal tool;
  add an existence check in `Create`/`Update` if that ever bites.
- `SearchPromotions` orders by `PromoCode DESC` because codes start with a date stamp, which puts
  the newest promotions at the top of the autocomplete panel.
