# External Supplier Dashboard — Missing Fields

Tracks what the frontend supplier portal (`/supplier/dashboard/$recordId`) still can't show, and what it reads today.

## Still Missing: Deal Submission

The Submit a Deal tab (`activeTab === 'submit-deal'`) requires a backend endpoint that does not exist:

| Endpoint | Needed For | Status |
|----------|-----------|--------|
| `POST /api/suppliers/{recordId}/deals` or similar | Submit deal opportunity form | ❌ Missing — no POST endpoints exist in Suppliers module |

The frontend form collects: material type, estimated quantity, packaging, ready-to-ship date, asking price (optional), and additional notes. Currently shows a mock success state after 1.5 seconds — no data is actually sent to the backend.

## Still Missing: Pickup Request

The Request Pickup tab (`activeTab === 'request-pickup'`) requires a backend endpoint that does not exist:

| Endpoint | Needed For | Status |
|----------|-----------|--------|
| `POST /api/suppliers/{recordId}/pickups` or similar | Submit pickup request form | ❌ Missing — no POST endpoints exist in Suppliers module |

The frontend form collects: material for pickup, truck type needed, preferred pickup date, preferred time, estimated load weight, number of trucks, pickup address, and special instructions. Currently shows a mock success state after 1.5 seconds — no data is actually sent to the backend.

## Still Missing: Messages

The Messages tab (`activeTab === 'messages'`) shows a "Coming Soon" placeholder. No backend endpoints are needed until messaging is implemented:

| Endpoint | Needed For | Status |
|----------|-----------|--------|
| `GET /api/suppliers/{recordId}/messages` | Fetch message thread | ❌ Not yet needed |
| `POST /api/suppliers/{recordId}/messages` | Send a message | ❌ Not yet needed |

## Wired Since the Last Revision

These were previously missing and are now implemented end to end.

### Dashboard and Payments stat cards

Both read `GET /api/suppliers/{recordId}/dashboard-stats`, worked out from the supplier's delivered ITS Dispatch loads (`bspsb9kpm`):

| Field | Card | Status |
|-------|------|--------|
| `totalPaidYtd` / `paidYtdCount` | "Total Paid (YTD)" — value and "N loads settled" subtitle | ✅ `paidYtdCount` was computed by `SupplierPaymentService.Summarize` but dropped at the DTO boundary; added to `SupplierDashboardStatsDto` |
| `pendingPaymentAmount` / `pendingPaymentCount` | "Pending Payment" | ✅ |
| `loadsLast90Days` / `deliveredLast90DaysAmount` | "Loads (Last 90 Days)" — count and dollar total | ✅ |
| `currentOffer` / `currentOfferSource` | Header and "Current Offer" card; falls back to the last load's Buy Price (512) when the target price is 0, so the header no longer shows `$0.00` | ✅ |
| `lastDeliveredDate` / `lastLoadNumber` | Payments "Last Payment" card | ✅ newly surfaced |
| `asOf` | Payments "Payment Terms" subtitle — replaces the invented "Paid Fridays" | ✅ |
| `deliveredBefore90Days` / `deliveredBefore90DaysAmount` | "Loads (Before 90 Days)" — count and dollar total | ✅ newly surfaced |

"Total Loads (YTD)" now reads `paidYtdCount` rather than `supplier.totalActivities`, so the count and the dollar figure beside it describe the same loads.

### Payment History table

`GET /api/suppliers/{recordId}/linked-its-dispatch-records` grew from 11 fields to 40. ITS Dispatch records only *scheduled* payments (no paid flag, check number or paid amount), so a load is `Paid` when its Payment Date (572) is on or before today in the business time zone (`SupplierPaymentService.ClassifyPayment`).

| Column | Was | Now |
|--------|-----|-----|
| Amount Paid | `formatCurrency(load.weight ?? 0)` — the load's **weight** rendered as dollars | `load.paymentAmount` (386), "—" when null |
| Payment Date | a hard-coded em dash | `load.paymentDate` (572) |
| Payment Status | did not exist | `load.paymentStatus` badge: Paid / Scheduled / Unscheduled / NotApplicable |

The response also carries `buyPrice`, `salePrice`, `grossProfit`, `totalCommissions`, `scaleTicketNumber`, `shipperPoNumbers`, `equipmentType`, `carrierFee`, the gross/tare/net weights, dead-freight fields and more — all typed in `SupplierDispatchRecord` and available for columns not yet built.

### Status filtering

Both dispatch-records and dashboard-stats accept a repeatable `?status=` (exact status text, OR-ed; dashboard-stats defaults to Delivered). The client functions and hooks now thread an optional `statuses` argument, and `GET /api/suppliers/linked-its-dispatch-records/status-filters` is wired via `listDispatchStatusFilters()` / `useDispatchStatusFilters()` — the correct source for a picker, since it excludes the Test statuses that `/statuses` includes.

A status-filter chip group now renders above the Dashboard, Payments, and Load History tables, letting the caller pick which load statuses are counted/shown.

## Internal-Only Endpoints (not for the supplier portal)

`POST /api/suppliers/reports/missing-invoices?date=` and `POST /api/suppliers/reports/remittance?date=` trigger retired Quickbase-style reports through n8n webhooks. They require `users.manage`, so they must **not** appear on the external supplier portal.

They **are** wired on the admin side:

- **Frontend:** `ReportsPanel` at `/admin?tab=reports`, gated by `users.manage` via the `reports` nav entry. Two cards (Missing Invoice Report with optional date, Remittance Report with required date) trigger the webhooks and show success/error toasts.
- **Hooks:** `useTriggerMissingInvoicesReport()` / `useTriggerRemittanceReport()` in `useAdminQueries.ts`, calling `triggerMissingInvoicesReport()` / `triggerRemittanceReport()` in `supplier-api.ts`.
- **Backend:** `ReportsController` with `RequirePermission(ReportPermissions.Trigger)` where `ReportPermissions.Trigger = "users.manage"`.

Deliberately not exposed on the supplier portal — external users cannot see or trigger them.

## Recommended Backend Changes

1. Nothing outstanding for the stat cards or Payment History — both are fully wired.
2. **Deal submission and pickup request endpoints** — the two forms still post nowhere.
3. **A dedicated payments endpoint** (`GET /api/suppliers/{recordId}/payments`) can be added on `SupplierPaymentService` when a real payment source (check number, paid amount) exists, replacing `ClassifyPayment`'s date heuristic.
4. **Messaging endpoints** when the Messages tab leaves "Coming Soon".

## Frontend Files Affected

- `frontend/src/app/components/suppliers/supplier-dashboard-panel.tsx` — stat cards, Payment History columns, payment status badge
- `frontend/src/app/lib/supplier-api.ts` — `SupplierDispatchRecord` (40 fields), `SupplierDashboardStats`, `PaymentStatus`, status params, `listDispatchStatusFilters()`
- `frontend/src/app/hooks/useSupplierQueries.ts` — `statuses` args, `useDispatchStatusFilters()`
