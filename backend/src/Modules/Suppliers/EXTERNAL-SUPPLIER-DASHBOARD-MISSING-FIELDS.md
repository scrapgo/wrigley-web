## Dashboard Tab Data

The main Dashboard tab (`activeTab === 'dashboard'`) and the Payments tab's stat cards now read `GET /api/suppliers/{recordId}/dashboard-stats`, worked out from the supplier's delivered ITS Dispatch loads (`bspsb9kpm`):

| Field | Needed For | Backend Endpoint | Status |
|-------|-----------|-----------------|--------|
| Total Paid YTD (currency) | "Total Paid (YTD)" stat card | `GET /api/suppliers/{recordId}/dashboard-stats` | ✅ `totalPaidYtd` (Payment Amount 386, Payment Date 572 this year) |
| Pending Payment amount | "Pending Payment" stat card | `GET /api/suppliers/{recordId}/dashboard-stats` | ✅ `pendingPaymentAmount` / `pendingPaymentCount` (Payment Date after today or empty) |
| Loads (Last 90 Days) | "Loads (Last 90 Days)" stat card | `GET /api/suppliers/{recordId}/dashboard-stats` | ✅ `loadsLast90Days` (Del Date 15) |
| Current Offer | Dashboard header and stat card | `GET /api/suppliers/{recordId}/dashboard-stats` | ✅ `currentOffer`: target consumer price if > 0, else the last load's Buy Price (512); "—" when neither |
| Delivered Before 90 Days | Not currently displayed | `GET /api/suppliers/{recordId}/dashboard-stats` | ✅ `deliveredBefore90Days`, returned but not shown yet |

## Payment Data

The Payments tab (`activeTab === 'payments'`). ITS Dispatch records only *scheduled* payments (no paid flag, check number or paid amount), so a load is Paid when its Payment Date (572) is on or before today (`SupplierPaymentService.ClassifyPayment`).

| Field | Needed For | Backend Endpoint | Status |
|-------|-----------|-----------------|--------|
| Amount Paid per load | Payment History table "Amount Paid" column | `GET /api/suppliers/{recordId}/linked-its-dispatch-records` | ✅ Backend `paymentAmount` (386). ❌ **Frontend wiring needed:** the column renders `load.weight` |
| Payment Date per load | Payment History table "Payment Date" column | `GET /api/suppliers/{recordId}/linked-its-dispatch-records` | ✅ Backend `paymentDate` (572), `payDateOverride` (593), `paymentStatus` (Paid / Scheduled / Unscheduled / NotApplicable). ❌ **Frontend wiring needed:** the column is a hard-coded "—" |
| Total Paid YTD (currency) | Payments tab "Total Paid (YTD)" stat card | `GET /api/suppliers/{recordId}/dashboard-stats` | ✅ `totalPaidYtd`, already wired |
| Pending Payment amount | Payments tab "Pending Payment" stat card | `GET /api/suppliers/{recordId}/dashboard-stats` | ✅ `pendingPaymentAmount` / `pendingPaymentCount`, already wired |

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

1. ✅ Payment amount, payment date and payment status are on `SupplierDispatchRecordDto`; the Payment History columns still need frontend wiring.

2. ✅ Total Paid (YTD) and Pending Payment come from `dashboard-stats` (one rule, `SupplierPaymentService.Summarize`), not the supplier DTO.

3. **A dedicated payments endpoint** (`GET /api/suppliers/{recordId}/payments`) can be added on the same service when needed.

## Frontend Files Affected

- `frontend/src/app/components/suppliers/supplier-dashboard-panel.tsx` — Payments tab content and stat cards
- `frontend/src/app/lib/supplier-api.ts` — DTO interfaces and API client functions
- `frontend/src/app/hooks/useSupplierQueries.ts` — React Query hooks
