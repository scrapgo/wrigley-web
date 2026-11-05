## Missing Dashboard Tab Data

The main Dashboard tab (`activeTab === 'dashboard'`) shows blanks and zeros for fields that either don't exist or have zero values in Quickbase:

| Field | Needed For | Backend Endpoint | Status |
|-------|-----------|-----------------|--------|
| Total Paid YTD (currency) | Dashboard "Total Paid (YTD)" stat card | `GET /api/suppliers/{recordId}` | ❌ Missing — only `deliveredLast90Days` (count) is available, not currency |
| Pending Payment amount | Dashboard "Pending Payment" stat card | None | ❌ Missing — no endpoint or field exists |
| Loads (Last 90 Days) | Dashboard "Loads (Last 90 Days)" stat card | `GET /api/suppliers/{recordId}` | ⚠️ Available as count (`deliveredLast90Days`) but shows 0 when null |
| Current Offer | Dashboard header "Current Offer" | `GET /api/suppliers/{recordId}` | ⚠️ Available (`targetConsumerPrice`) but shows `$0.00` when value is 0 in Quickbase |
| Delivered Before 90 Days | Not currently displayed | `GET /api/suppliers/{recordId}` | ⚠️ Available (`deliveredBefore90Days`) but not used in dashboard |

## Missing Payment Data

The Payments tab (`activeTab === 'payments'`) requires the following fields that do not exist in the backend:

| Field | Needed For | Backend Endpoint | Status |
|-------|-----------|-----------------|--------|
| Amount Paid per load | Payment History table "Amount Paid" column | `GET /api/suppliers/{recordId}/linked-its-dispatch-records` | ❌ Missing — dispatch record DTO has no amount field |
| Payment Date per load | Payment History table "Payment Date" column | `GET /api/suppliers/{recordId}/linked-its-dispatch-records` | ❌ Missing — dispatch record DTO has no payment date field |
| Total Paid YTD (currency) | Payments tab "Total Paid (YTD)" stat card | `GET /api/suppliers/{recordId}` | ❌ Missing — only `deliveredLast90Days` (count) is available, not currency |
| Pending Payment amount | Payments tab "Pending Payment" stat card | None | ❌ Missing — no endpoint or field exists |

## Missing Deal Submission

The Submit a Deal tab (`activeTab === 'submit-deal'`) requires a backend endpoint that does not exist:

| Endpoint | Needed For | Status |
|----------|-----------|--------|
| `POST /api/suppliers/{recordId}/deals` or similar | Submit deal opportunity form | ❌ Missing — no POST endpoints exist in Suppliers module |

The frontend form collects: material type, estimated quantity, packaging, ready-to-ship date, asking price (optional), and additional notes. Currently shows a mock success state after 1.5 seconds — no data is actually sent to the backend.

## Missing Pickup Request

The Request Pickup tab (`activeTab === 'request-pickup'`) requires a backend endpoint that does not exist:

| Endpoint | Needed For | Status |
|----------|-----------|--------|
| `POST /api/suppliers/{recordId}/pickups` or similar | Submit pickup request form | ❌ Missing — no POST endpoints exist in Suppliers module |

The frontend form collects: material for pickup, truck type needed, preferred pickup date, preferred time, estimated load weight, number of trucks, pickup address, and special instructions. Currently shows a mock success state after 1.5 seconds — no data is actually sent to the backend.

## Missing Messages

The Messages tab (`activeTab === 'messages'`) now shows a "Coming Soon" placeholder instead of mock data. No backend endpoints are needed until messaging is implemented:

| Endpoint | Needed For | Status |
|----------|-----------|--------|
| `GET /api/suppliers/{recordId}/messages` | Fetch message thread | ❌ Not yet needed |
| `POST /api/suppliers/{recordId}/messages` | Send a message | ❌ Not yet needed |

The frontend shows a "Messages coming soon" card — no data is sent to or received from the backend.

## Currently Available Data

The following fields ARE available from the backend and are used by the dashboard:

| Field | Endpoint | Used In |
|-------|----------|---------|
| `targetConsumerPrice` | `GET /api/suppliers/{recordId}` | Dashboard header "Current Offer", Payments tab |
| `paymentTerms` | `GET /api/suppliers/{recordId}` | Dashboard header subtitle, Payments tab "Payment Terms" |
| `totalActivities` | `GET /api/suppliers/{recordId}` | Dashboard "Total Loads (YTD)", Payments tab subtitle |
| `deliveredLast90Days` | `GET /api/suppliers/{recordId}` | Dashboard "Loads (Last 90 Days)", Payments "Total Paid (YTD)" (as count proxy) |
| `deliveredBefore90Days` | `GET /api/suppliers/{recordId}` | Not currently displayed |
| `account` (supplier name) | `GET /api/suppliers/{recordId}` | Dashboard header, Load History tab header |
| `city`, `state`, `streetAddress`, `zipCode` | `GET /api/suppliers/{recordId}` | Dashboard header address |
| `leadAssignedTo` (name, email) | `GET /api/suppliers/{recordId}` | Dashboard "Your ScrapGo Rep" |
| `mainEmail` | `GET /api/suppliers/{recordId}` | Dashboard "Your ScrapGo Rep" fallback |
| Dispatch records: `loadNumber`, `material`, `weight`, `shipDate`, `deliveryDate`, `status`, `consumer` | `GET /api/suppliers/{recordId}/linked-its-dispatch-records` | Dashboard "Recent Loads", Payments "Payment History", Load History tab |

**Load History tab** uses the same dispatch records data as the Payments tab — no new backend fields needed.

## Recommended Backend Changes

1. **Add payment amount and payment date fields to the dispatch record DTO** (`SupplierDispatchRecordDto`) so the Payment History table can display real values.

2. **Add a `totalPaidYtd` (currency) field to the supplier DTO** (`SupplierDto`) for the "Total Paid (YTD)" stat card.

3. **Add a `pendingPayment` (currency) field to the supplier DTO** for the "Pending Payment" stat card.

4. **Consider a dedicated payments endpoint** (`GET /api/suppliers/{recordId}/payments`) if payment data lives in a separate Quickbase table.

## Frontend Files Affected

- `frontend/src/app/components/suppliers/supplier-dashboard-panel.tsx` — Payments tab content and stat cards
- `frontend/src/app/lib/supplier-api.ts` — DTO interfaces and API client functions
- `frontend/src/app/hooks/useSupplierQueries.ts` — React Query hooks
