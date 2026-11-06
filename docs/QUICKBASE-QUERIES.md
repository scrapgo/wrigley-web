# Quickbase Queries by Endpoint

The exact Quickbase query behind every API endpoint.

**Keep this current.** When you add or change an endpoint, or change a Quickbase query (table, `select`, `where`, `sortBy`, paging), update this file in the same change. See [Keeping this document current](#keeping-this-document-current).

- [How to read this](#how-to-read-this)
- [Tables](#tables)
- [Supplier endpoints](#supplier-endpoints)
  - [`GET /api/suppliers`](#get-apisuppliers)
  - [`GET /api/suppliers/{recordId}`](#get-apisuppliersrecordid)
  - [`GET /api/suppliers/{recordId}/target-pricing-progress-rail`](#get-apisuppliersrecordidtarget-pricing-progress-rail)
  - [`GET /api/suppliers/{recordId}/in-stock-items`](#get-apisuppliersrecordidin-stock-items)
  - [`GET /api/suppliers/{recordId}/contacts`](#get-apisuppliersrecordidcontacts)
  - [`GET /api/suppliers/{recordId}/call-notes-history`](#get-apisuppliersrecordidcall-notes-history)
  - [`GET /api/suppliers/{recordId}/linked-its-dispatch-records`](#get-apisuppliersrecordidlinked-its-dispatch-records)
  - [`GET /api/suppliers/{recordId}/yard-capabilities`](#get-apisuppliersrecordidyard-capabilities)
  - [`GET /api/suppliers/{recordId}/other-prospect-information`](#get-apisuppliersrecordidother-prospect-information)
  - [`GET /api/suppliers/{recordId}/call-prospect-status`](#get-apisuppliersrecordidcall-prospect-status)
- [Endpoints with no Quickbase query](#endpoints-with-no-quickbase-query)
- [Keeping this document current](#keeping-this-document-current)

## How to read this

**Every query is a read: `POST https://api.quickbase.com/v1/records/query`.**
- The body has the fields `from`, `select`, `where`, `sortBy` and `options`. A field is left out when it isn't set.
- `QuickbaseHttpClient.RecordsQueryBody.Create` builds the body from a `QuickbaseQuery` (`backend/src/Shared/ScrapGo.Core.Shared.Kernel/Quickbase/QuickbaseQuery.cs`).
- The API never writes to Quickbase.

**Every query goes through `IQuickbaseQueryService`. Nothing calls Quickbase directly.**
- **Who may query:** it refuses callers who aren't signed in or aren't active.
- **Caching:** it caches each response in `quickbase.query_caches`. The cache key is a hash of the realm plus the query, so the order of `select` matters.
- **Lifetime:** a cached response is served for `Quickbase:QueryCache:Ttl` (15 minutes by default).
- **Failures:** if a refresh fails, the expired response is served instead, marked `StaleCache`.
- Each response's `freshness` (`Quickbase`, `Cache`, `StaleCache` or `None`) says where its data came from.

**Supplier scope.**
- `suppliers.view_all` sees every supplier.
- `suppliers.view_own` sees only the caller's linked Record IDs:
  - The list query gets `({3.EX.'<id>'}OR…)AND` in front.
  - A record endpoint asked for a supplier outside the scope answers 404 `supplier_not_found` before Quickbase is asked.
- **No supplier view permission at all** gets 403 `missing_permission`.

**Mapping.** Field ids live in the `*Table.cs` classes in `backend/src/Modules/Suppliers/ScrapGo.Core.Modules.Suppliers.Infrastructure/Quickbase/`. `QuickbaseSupplierSource` maps each row to a DTO.
- Rows without a Record ID# (field 3) are dropped.
- Dropdown text Quickbase sends that isn't a known value comes back as `null`, and a warning is logged.

**Running a query yourself.** The examples use supplier Record ID **`17511`**. Use your own Quickbase user token, and never commit it:

```bash
curl -s https://api.quickbase.com/v1/records/query \
  -H "QB-Realm-Hostname: <realm>.quickbase.com" \
  -H "Authorization: QB-USER-TOKEN <your-user-token>" \
  -H "Content-Type: application/json" \
  -d '{ "from": "bqrcgnatz", "select": [3, 8], "where": "{3.EX.'\''17511'\''}" }'
```

**Query syntax.**
- `{fid.EX.'v'}` means "equals", `{fid.XEX.'v'}` "does not equal", and `{fid.CT.'v'}` "contains".
- In a value, `\` escapes a single quote or a backslash (`SuppliersTable.EscapeValue`).

## Tables

| Table | Table id | Code | Linked to a supplier by |
| --- | --- | --- | --- |
| Suppliers | `bqrcgnatz` | `SuppliersTable` | Field 3 (Record ID#) is the supplier |
| In-Stock Items | `bscpxjrw3` | `InStockItemsTable` | Field 15, Related Supplier |
| Contacts | `br8imsxs2` | `ContactsTable` | Field 29, Related Supplier |
| Call Notes | `br8tgw3tp` | `CallNotesTable` | Field 19, Related Supplier |
| ITS Dispatch Records | `bspsb9kpm` | `DispatchRecordsTable` | Field 366, Related Supplier |

## Supplier endpoints

All of these are in `PlatformSuppliersController` (`/api/suppliers`) and require `suppliers.view_all` or `suppliers.view_own`.
- **Errors:**
  - 403 `missing_permission`
  - 404 `supplier_not_found`
  - 502 `quickbase_unavailable` when Quickbase fails and no cached response exists.
- **Record endpoints:** for a missing supplier, the single-record ones return 404. The list ones return an empty list.

### `GET /api/suppliers`

Suppliers with a name, sorted by name, one page at a time. Each item is a full supplier record, with the same fields as [`GET /api/suppliers/{recordId}`](#get-apisuppliersrecordid).

- **Source:** `QuickbaseSupplierSource.ListAsync`. The `where` comes from `SuppliersTable.NamedLike(search, recordIds)`.
- **Parameters:** `search` (optional, trimmed), `skip` (default 0), `top` (1–1000, default 100).

**Query** with no search and `?skip=0&top=25`:

```json
{
  "from": "bqrcgnatz",
  "select": [3, 8, 9, 10, 11, 64, 12, 355, 123, 320, 301, 74, 28, 133, 116, 346, 129, 214, 321],
  "where": "{8.XEX.''}",
  "sortBy": [{ "fieldId": 8, "order": "ASC" }],
  "options": { "skip": 0, "top": 25 }
}
```

How `where` changes:

| Case | `where` |
| --- | --- |
| No search | `{8.XEX.''}` |
| `search=lake` | `{8.XEX.''}AND{8.CT.'lake'}` |
| `suppliers.view_own`, linked to 17511 and 17602 | `({3.EX.'17511'}OR{3.EX.'17602'})AND{8.XEX.''}` |
| `suppliers.view_own` plus search | `({3.EX.'17511'}OR{3.EX.'17602'})AND{8.XEX.''}AND{8.CT.'lake'}` |

- **Scoped Record IDs** are sorted ascending, so the same scope always produces the same query and shares a cache entry.
- **Empty scope:** a `suppliers.view_own` caller with no linked suppliers gets an empty page (`freshness.source` `None`), and Quickbase isn't called.
- **Response:** `{ items, skip, top, totalRecords, freshness }`.
  - `totalRecords` is Quickbase's `metadata.totalRecords`. If Quickbase leaves it out, it is `skip + items.length`.
  - The fields in each item are in the table under the next endpoint.

### `GET /api/suppliers/{recordId}`

One supplier's details.

- **Source:** `QuickbaseSupplierSource.FindByRecordIdAsync`. It selects `SuppliersTable.DetailFields`.

```json
{
  "from": "bqrcgnatz",
  "select": [3, 8, 9, 10, 11, 64, 12, 355, 123, 320, 301, 74, 28, 133, 116, 346, 129, 214, 321],
  "where": "{3.EX.'17511'}"
}
```

| Field | Quickbase name (code constant) | Response property | Notes |
| --- | --- | --- | --- |
| 3 | Record ID# (`RecordId`) | `recordId` | |
| 8 | Account (`Account`) | `account` | The supplier name |
| 9 | Street Address (`StreetAddress`) | `streetAddress` | |
| 10 | City (`City`) | `city` | |
| 11 | State (`State`) | `state` | |
| 64 | Country (`Country`) | `country` | |
| 12 | Zip Code (`ZipCode`) | `zipCode` | |
| 355 | Main Contact Phone (`MainContactPhone`) | `mainContactPhone` | |
| 123 | Main Contact Names (`MainContactNames`) | `mainContactNames` | A list of text |
| 320 | Payment Terms (`PaymentTerms`) | `paymentTerms` | Dropdown text, e.g. `"Net 5"` (`PaymentTermsCatalog`) |
| 301 | Main Email (`MainEmail`) | `mainEmail` | |
| 74 | Lead Assigned To (`LeadAssignedTo`) | `leadAssignedTo` | A user: `{ id, email, name }` |
| 28 | Relevant Consumer Distances (`RelevantConsumerDistances`) | `relevantConsumerDistances` | |
| 133 | In-Stock Item Records (`InStockItemRecords`) | `inStockItemRecords` | |
| 116 | Total Activities (`TotalActivities`) | `totalActivities` | |
| 346 | Target Consumer Price (`TargetConsumerPrice`) | `targetConsumerPrice` | |
| 129 | Delivered Last 90 Days (`DeliveredLast90Days`) | `deliveredLast90Days` | |
| 214 | Delivered Before 90 Days (`DeliveredBefore90Days`) | `deliveredBefore90Days` | |
| 321 | Dead Freight (`DeadFreight`) | `deadFreight` | A checkbox: checked is `"Not Exempt"`, unchecked `"Exempt"` |

- **Response:** `{ supplier, freshness }`.
- **No row:** 404 `supplier_not_found`.

### `GET /api/suppliers/{recordId}/target-pricing-progress-rail`

- **Source:** `QuickbaseSupplierSource.FindTargetPricingProgressRailAsync`. It selects `SuppliersTable.TargetPricingProgressRailFields`.

```json
{
  "from": "bqrcgnatz",
  "select": [3, 352, 342, 336, 337, 339, 340, 341, 346, 345, 347, 348, 349, 358, 363],
  "where": "{3.EX.'17511'}"
}
```

| Field | Code constant | Response property |
| --- | --- | --- |
| 3 | `RecordId` | `recordId` |
| 352 | `TargetMaterial` | `targetMaterial` |
| 342 | `TargetBreakEven` | `targetBreakEven` |
| 336 | `TargetOffer` | `targetOffer` |
| 337 | `TargetUom` | `targetUom` |
| 339 | `TrucksPerWeek` | `trucksPerWeek` |
| 340 | `TargetFreightPerUom` | `targetFreightPerUom` |
| 341 | `TargetFreightCost` | `targetFreightCost` |
| 346 | `TargetConsumerPrice` | `targetConsumerPrice` |
| 345 | `PriceInNetTons` | `priceInNetTons` |
| 347 | `PriceInLbs` | `priceInLbs` |
| 348 | `PriceInCwt` | `priceInCwt` |
| 349 | `PriceInGrossTons` | `priceInGrossTons` |
| 358 | `TargetPoNumber` | `targetPoNumber` |
| 363 | `PriceChangeFromPrior` | `priceChangeFromPrior` |

- **Response:** `{ targetPricingProgressRail, freshness }`.
- **No row:** 404.

### `GET /api/suppliers/{recordId}/in-stock-items`

The supplier's in-stock items, used by Materials Pricing.

- **Source:** `QuickbaseSupplierSource.ListInStockItemsAsync`. It selects `InStockItemsTable.Fields`.

```json
{
  "from": "bscpxjrw3",
  "select": [3, 15, 6, 10, 10, 7, 8, 14, 18, 20, 19],
  "where": "{15.EX.'17511'}"
}
```

| Field | Code constant | Response property | Notes |
| --- | --- | --- | --- |
| 3 | `RecordId` | `recordId` | |
| 15 | `RelatedSupplier` | `relatedSupplier` | |
| 6 | `MaterialType` | `materialType` | Dropdown (`InStockItemDropdowns.MaterialTypes`) |
| 10 | `Packaging` | `packaging` and `prep` | One field, selected twice and returned under both names, as in the original portal query |
| 7 | `Weight` | `weight` | |
| 8 | `UnitOfMeasure` | `unitOfMeasure` | Dropdown (`InStockItemDropdowns.UnitsOfMeasure`) |
| 14 | `TargetOffer` | `targetOffer` | |
| 18 | `TruckType` | `truckType` | Dropdown (`InStockItemDropdowns.TruckTypes`) |
| 20 | `TrucksPerWeek` | `trucksPerWeek` | |
| 19 | `Volume` | `volume` | |

- **Response:** `{ supplierRecordId, items, freshness }`.
- **Sort order:** Quickbase's order. There is no `sortBy`.
- **None:** an empty list, never 404.

### `GET /api/suppliers/{recordId}/contacts`

- **Source:** `QuickbaseSupplierSource.ListContactsAsync`. It selects `ContactsTable.Fields`.

```json
{
  "from": "br8imsxs2",
  "select": [3, 29, 20, 18, 25, 22, 15, 17, 21, 46],
  "where": "{29.EX.'17511'}"
}
```

| Field | Code constant | Response property |
| --- | --- | --- |
| 3 | `RecordId` | `recordId` |
| 29 | `RelatedSupplier` | `relatedSupplier` |
| 20 | `LastName` | `lastName` |
| 18 | `FirstName` | `firstName` |
| 25 | `Title` | `title` |
| 22 | `Phone` | `phone` |
| 15 | `Cell` | `cell` |
| 17 | `Email` | `email` |
| 21 | `Notes` | `notes` |
| 46 | `IsMain` ("Main?" checkbox) | `isMain` |

- **Response:** `{ supplierRecordId, contacts, freshness }`.
- **Sort order:** sorted in the API, not in Quickbase.
  1. The main contact first.
  2. Then last name, then first name, ignoring case.
  3. Then Record ID.
- **None:** an empty list, never 404.

### `GET /api/suppliers/{recordId}/call-notes-history`

- **Source:** `QuickbaseSupplierSource.ListCallNotesAsync`. It selects `CallNotesTable.Fields`.

```json
{
  "from": "br8tgw3tp",
  "select": [3, 1, 17, 19],
  "where": "{19.EX.'17511'}"
}
```

| Field | Code constant | Response property |
| --- | --- | --- |
| 3 | `RecordId` | `recordId` |
| 1 | `DateCreated` (built in) | `dateCreated` |
| 17 | `Notes` | `notes` |
| 19 | `RelatedSupplier` | `relatedSupplier` |

- **Response:** `{ supplierRecordId, notes, freshness }`.
- **Sort order:** sorted in the API, newest `dateCreated` first.
  - Notes with no date go last.
  - Then Record ID, highest first.
- **None:** an empty list, never 404.

### `GET /api/suppliers/{recordId}/linked-its-dispatch-records`

- **Source:** `QuickbaseSupplierSource.ListDispatchRecordsAsync`. It selects `DispatchRecordsTable.Fields`.

```json
{
  "from": "bspsb9kpm",
  "select": [3, 6, 241, 432, 433, 28, 508, 378, 14, 15, 8],
  "where": "{366.EX.'17511'}"
}
```

| Field | Code constant | Response property | Notes |
| --- | --- | --- | --- |
| 3 | `RecordId` | `recordId` | |
| 6 | `LoadNumber` | `loadNumber` | Added to the original portal query, which left it out |
| 241 | `Consumer` | `consumer` | Returned as "Consumer - City, State"; empty parts are left out (`DispatchRecordsTable.FormatConsumer`) |
| 432 | `ConsumerCity` | `consumerCity` | |
| 433 | `ConsumerState` | `consumerState` | |
| 28 | `CarrierDriver` | `carrierDriver` | |
| 508 | `Material` | `material` | |
| 378 | `Weight` | `weight` | |
| 14 | `ShipDate` | `shipDate` | |
| 15 | `DeliveryDate` | `deliveryDate` | |
| 8 | `Status` | `status` | Dropdown (`DispatchStatuses`) |

- **Response:** `{ supplierRecordId, dispatchRecords, freshness }`.
- **Sort order:** sorted in the API, newest `shipDate` first.
  - Records with no ship date go last.
  - Then Record ID, highest first.
- **None:** an empty list, never 404.

### `GET /api/suppliers/{recordId}/yard-capabilities`

- **Source:** `QuickbaseSupplierSource.FindYardCapabilitiesAsync`. It selects `SuppliersTable.YardCapabilitiesFields`.
- **Field types:** every field except 3 is a checkbox.

```json
{
  "from": "bqrcgnatz",
  "select": [3, 65, 186, 78, 182, 225, 204, 205, 185, 359, 183, 187, 184, 230],
  "where": "{3.EX.'17511'}"
}
```

| Field | Code constant | Response property |
| --- | --- | --- |
| 3 | `RecordId` | `recordId` |
| 65 | `CrusherOnSite` | `crusherOnSite` |
| 186 | `LoggerOnSite` | `loggerOnSite` |
| 78 | `LoadFlatbeds` | `loadFlatbeds` |
| 182 | `LoadDumps` | `loadDumps` |
| 225 | `MobileCrusher` | `mobileCrusher` |
| 204 | `CanExport` | `canExport` |
| 205 | `HasGaylordBoxes` | `hasGaylordBoxes` |
| 185 | `BalerOnSite` | `balerOnSite` |
| 359 | `HasScale` | `hasScale` |
| 183 | `LoadVanTrailers` | `loadVanTrailers` |
| 187 | `HasLoadWrap` | `hasLoadWrap` |
| 184 | `UsesOwnTrucks` | `usesOwnTrucks` |
| 230 | `RailAccess` | `railAccess` |

- **Response:** `{ yardCapabilities, freshness }`.
- **No row:** 404.

### `GET /api/suppliers/{recordId}/other-prospect-information`

Other Prospect Information (the "Documents & Other" section).

- **Source:** `QuickbaseSupplierSource.FindOtherProspectInformationAsync`.
- **Two queries,** because Quickbase can't join tables.
  - The second query runs only if the first one finds the supplier.
  - An unknown supplier is 404 after one query.

**Query 1:** the supplier (`SuppliersTable.OtherProspectInformationFields`).

```json
{
  "from": "bqrcgnatz",
  "select": [3, 77, 152, 175, 157, 215],
  "where": "{3.EX.'17511'}"
}
```

| Field | Code constant | Response property |
| --- | --- | --- |
| 3 | `RecordId` | `recordId` |
| 77 | `LeadSource` | `leadSource` |
| 152 | `LastReassignedSupplier` | `lastReassignedSupplier` |
| 175 | `LastModifiedBy` | `lastModifiedBy` |
| 157 | `RelatedSupplierOffice` | `relatedSupplierOffice` |
| 215 | `OutOfBusiness` (checkbox) | `outOfBusiness` |

**Query 2:** the latest activity, which is the newest call note (`CallNotesTable.LatestActivityFields`).

```json
{
  "from": "br8tgw3tp",
  "select": [3, 19, 57, 1],
  "where": "{19.EX.'17511'}",
  "sortBy": [{ "fieldId": 1, "order": "DESC" }],
  "options": { "skip": 0, "top": 1 }
}
```

| Field | Code constant | Response property (in `latestActivity`) |
| --- | --- | --- |
| 3 | `RecordId` | `recordId` |
| 19 | `RelatedSupplier` | `supplierId` |
| 57 | `SupplierAssignedTo` | `supplierAssignedTo` |
| 1 | `DateCreated` | `lastReassigned` |

- **Response:** `{ otherProspectInformation, freshness }`.
- **No call notes:** `latestActivity` is `null`.
- **Freshness:** taken from the less fresh of the two queries.
  - The source is the worse one, ranked `StaleCache`, then `Cache`, then `Quickbase`.
  - `fetchedAt` is the earlier of the two.

### `GET /api/suppliers/{recordId}/call-prospect-status`

- **Source:** `QuickbaseSupplierSource.FindCallProspectStatusAsync`. It selects `SuppliersTable.CallProspectStatusFields`.

```json
{
  "from": "bqrcgnatz",
  "select": [3, 197, 192, 193, 236, 181, 238, 97],
  "where": "{3.EX.'17511'}"
}
```

| Field | Code constant | Response property | Notes |
| --- | --- | --- | --- |
| 3 | `RecordId` | `recordId` | |
| 197 | `ContactWithDecisionMakerMade` | `contactWithDecisionMakerMade` | |
| 192 | `ProspectStatus` | `prospectStatus` | |
| 193 | `LastCallResult` | `lastCallResult` | Dropdown (`LastCallResults`) |
| 236 | `SupplierObjections` | `supplierObjection` | Dropdown (`SupplierObjections`) |
| 181 | `CallBackDate` | `callBackDate` | |
| 238 | `ObjectionExplained` | `objectionExplained` | |
| 97 | `CallNotes` | `callNotes` | The legacy integration's field 136 is wrong |

- **Response:** `{ callProspectStatus, freshness }`.
- **No row:** 404.

## Endpoints with no Quickbase query

These endpoints never call Quickbase.

**Supplier reference data.** These are static catalogs in code, and need `suppliers.view_all` or `suppliers.view_own`. Each option's text is the exact text Quickbase stores.
- `GET /api/suppliers/payment-terms` (`PaymentTermsController`)
- `GET /api/suppliers/dead-freight` (`DeadFreightController`)
- `SupplierDropdownsController`:
  - `GET /api/suppliers/last-call-results`
  - `GET /api/suppliers/supplier-objections`
  - `GET /api/suppliers/in-stock-items/material-types`
  - `GET /api/suppliers/in-stock-items/units-of-measure`
  - `GET /api/suppliers/in-stock-items/preparations`
  - `GET /api/suppliers/in-stock-items/truck-types`
  - `GET /api/suppliers/linked-its-dispatch-records/statuses`

**Identity and administration.** These use Postgres only.
- `POST /api/session`, `GET /api/me`
- `/api/admin/users`:
  - `GET`, `GET {id}`
  - `POST {id}/disable`, `POST {id}/enable`
  - `PUT {id}/roles`, `PUT {id}/suppliers`
- `/api/admin/invitations`:
  - `GET`, `GET form`, `GET {id}`
  - `POST`, `POST {id}/resend`
  - `DELETE {id}`
- `/api/admin/roles`: `GET`, `PUT {id}/permissions`
- `/api/admin/permissions`: `GET`
- `/api/admin/features`: `GET`, `PUT {id}`, `PUT {id}/targets`
- `/api/admin/audit`: `GET`

Linking suppliers to a user (`PUT /api/admin/users/{id}/suppliers`) and inviting an External user store supplier Record IDs only, without checking them against Quickbase. The invitation form's supplier picker loads its suppliers from `GET /api/suppliers`.

## Keeping this document current

When you add or change an endpoint, or change any Quickbase query, update this file in the same commit:

1. **New endpoints:**
   - If the endpoint queries Quickbase, add a section under its module, in controller order. Give:
     - the route, the source method and the exact JSON body
     - a field table: field, code constant and response property
     - any sorting or mapping done in the API
     - empty and 404 behaviour.
   - If it doesn't, add it to [Endpoints with no Quickbase query](#endpoints-with-no-quickbase-query).
2. **Changed queries:**
   - Update the JSON body and the field table.
   - Keep `select` in the order the code sends it, because the order is part of the cache key.
3. **New tables:** add a row to [Tables](#tables).
4. **New field mappings:** add the field's constant to its `*Table.cs` class rather than writing a bare number in a query, so this document and the code use the same names.
5. **Changed query shape:** if you change how `QuickbaseQuery` is serialized (not just its values), bump `QueryKey.Version` so old cache entries aren't reused.
