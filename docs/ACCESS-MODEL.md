\# Access Model V1 — Spec



The single source of truth for users, roles, permissions and feature toggles in Wrigley.

It replaces `ORG-APP-MODULE-MODEL.md`. There are \*\*no organizations, applications or modules\*\*.



\## 1. Principles



1\. Authentication is not authorization. A valid GCIP login only proves identity; access comes from Postgres.

2\. People map to roles, roles map to permissions. A user is never given a permission directly.

3\. Permissions are discrete, named `resource.action`, and each is owned by exactly one feature.

4\. Every feature has a toggle: `Off`, `On` or `Targeted` (on only for chosen users or roles).

5\. The backend is the security boundary. The frontend only hides what the user can't use.

6\. No code checks a role name. Code checks permission keys only.

7\. GCP IAM is only for people who deploy and run the platform; it is not the app's role system.



\## 2. User types and roles



| Role key | Name | User type | Who |

|---|---|---|---|

| `site\_admin` | Site Admin | Internal | A few ScrapGo staff. Granted by another Site Admin; the first comes from `bootstrap-site-admin`. |

| `scrapgo\_user` | ScrapGo User | Internal | Every internal user, assigned automatically on first sign-in. |

| `user` | User | External | Buyers, sellers, transporters. Joins by invitation only. |



All three are system roles (`is\_system = true`), not creatable or deletable over HTTP in V1.

A role may only be held by a user of the same type. The database enforces this.



\## 3. Who is internal



A new user is \*\*Internal\*\* only when all of these are true:



1\. The token's `firebase.sign\_in\_provider` is `google.com`.

2\. `email\_verified` is true.

3\. The token's `hd` claim is on `INTERNAL\_HD\_ALLOWLIST`.



`hd` is put in the token by the existing `beforeSignIn` blocking function (`backend/infra/gcip-blocking-function/`), only for Google sign-ins.

An email/password account is never internal.



`INTERNAL\_HD\_ALLOWLIST` must accept both forms:



```json

"INTERNAL\_HD\_ALLOWLIST": \["scrapgo.com"]

```



```

INTERNAL\_HD\_ALLOWLIST=scrapgo.com,other.com   # env var form, set by Terraform on Cloud Run

```



User type is set once, at creation, and never re-evaluated.



\## 4. The access rule



```

can(user, permission) =

&#x20;   user.status == Active

&#x20;AND permission is granted by any of the user's roles

&#x20;AND featureIsOn(permission.feature, user)



featureIsOn(feature, user) =

&#x20;   feature.state == On

&#x20;OR (feature.state == Targeted AND (user is a target OR one of user's roles is a target))

```



\- A toggle never grants access; it only exposes a feature to people whose roles already hold its permissions.

\- Admin features (`users`, `roles`, `features`, `audit`) are locked: `is\_toggleable = false` and always `On`.



\## 5. Database (schema `identity`)



Follow repo conventions: `int` identity keys, snake\_case, `text` enums with `HasConversion<string>()` and a \*\*named\*\* CHECK constraint, one `DbContext` per module.



| Table | Columns (beyond `id`) | Constraints |

|---|---|---|

| `users` | `identity\_platform\_uid`, `email`, `display\_name`, `user\_type` (Internal/External), `status` (Active/Disabled), `sign\_in\_provider`, `last\_sign\_in\_at`, `created\_at`, `updated\_at` | unique `identity\_platform\_uid`; unique `(id, user\_type)` for composite FKs |

| `roles` | `key`, `name`, `description`, `user\_type`, `is\_system` | unique `key`; unique `(id, user\_type)` |

| `user\_roles` | `user\_id`, `role\_id`, `user\_type`, `assigned\_at`, `assigned\_by\_user\_id` | PK `(user\_id, role\_id)`; FK `(user\_id, user\_type)` → users; FK `(role\_id, user\_type)` → roles |

| `features` | `key`, `name`, `description`, `state` (Off/On/Targeted), `is\_toggleable`, `updated\_at`, `updated\_by\_user\_id` | unique `key`; CHECK `is\_toggleable OR state = 'On'` |

| `feature\_targets` | `feature\_id`, `user\_id` (null), `role\_id` (null) | CHECK exactly one of user/role; unique `(feature\_id, user\_id, role\_id)` NULLS NOT DISTINCT |

| `permissions` | `key`, `feature\_id`, `audience` (Internal/External/Any), `name`, `description` | unique `key` |

| `role\_permissions` | `role\_id`, `permission\_id` | PK both |

| `user\_suppliers` | `user\_id`, `user\_type` (always External), `supplier\_record\_id` (int, the Quickbase Suppliers Record ID), `linked\_at`, `linked\_by\_user\_id` | PK `(user\_id, supplier\_record\_id)`; FK `(user\_id, user\_type)` → users; CHECK `user\_type = 'External'` |

| `invitations` | `email`, `role\_id`, `supplier\_record\_ids` (int\[]), `token\_hash` (SHA-256), `status` (Pending/Accepted/Revoked/Expired), `expires\_at`, `invited\_by\_user\_id`, `accepted\_by\_user\_id`, `accepted\_at`, `created\_at` | unique `token\_hash`; partial unique `(email) WHERE status = 'Pending'` |



Audit stays in the existing `audit.audit\_logs` (`IAuditLog<IdentityModule>`).



Rules enforced in the Application layer (each has an allow and a deny test):

\- A permission with audience `Internal` maps only to internal roles; `External` only to external roles.

\- An invitation's role must be an external role.



\## 6. Seed data (by migration)



| Feature (state at launch) | Permission | Audience | site\_admin | scrapgo\_user | user |

|---|---|---|---|---|---|

| `dashboard` (On) | `dashboard.view` | Any | ✓ | ✓ | ✓ |

| `suppliers` (On) | `suppliers.view\_all` | Internal | ✓ | ✓ | |

| `suppliers` (On) | `suppliers.view\_own` | External | | | ✓ |

| `supplier\_edit` (Targeted) | `suppliers.edit\_all` | Internal | ✓ | ✓ | |

| `supplier\_edit` (Targeted) | `suppliers.edit\_own` | External | | | |

| `users` (On, locked) | `users.view` | Internal | ✓ | | |

| `users` (On, locked) | `users.invite` | Internal | ✓ | ✓ | |

| `users` (On, locked) | `users.manage` | Internal | ✓ | | |

| `roles` (On, locked) | `roles.view` | Internal | ✓ | | |

| `roles` (On, locked) | `roles.manage` | Internal | ✓ | | |

| `features` (On, locked) | `features.view` | Internal | ✓ | | |

| `features` (On, locked) | `features.manage` | Internal | ✓ | | |

| `audit` (On, locked) | `audit.view` | Internal | ✓ | | |



`users.manage` = enable/disable, role changes, supplier links. `suppliers.edit\_own` is defined but not granted (external users are view-only in V1).

Permission keys live as constants in `Permissions.cs` (Domain). Admins map keys to roles; they never invent keys.



\## 7. Sign-in and provisioning



Every sign-in ends with `POST /api/session` (optional body `{ "invitationToken": "…" }`).



| Situation | Result |

|---|---|

| User found, Active | Update `last\_sign\_in\_at`; return the access context |

| User found, Disabled | 403 `account\_disabled`; audit `access.denied` |

| Not found, passes the internal rule | Create Internal user + `scrapgo\_user`; audit `user.provisioned` |

| Not found, valid invitation token | Create External user with the invited role and supplier links; mark accepted; audit `invitation.accepted` (one transaction) |

| Not found, anything else | 403 `no\_access`; audit `access.denied`; no user row is created |



No other endpoint creates users. `GET /api/me` returns 403 `no\_access` for an unknown UID.



Invitations: created by `users.invite` (Site Admin and ScrapGo User), external role only, one or more supplier Record IDs, 32 random bytes base64url token, only its SHA-256 stored, 7-day expiry. Acceptance requires the token, `email\_verified`, and the token's email matching the invitation email (case-insensitive). Resending issues a new token.



\## 8. Access context and caching



```csharp

public sealed record AccessContext(

&#x20;   int UserId, UserType UserType, UserStatus Status,

&#x20;   IReadOnlyCollection<string> Roles,

&#x20;   IReadOnlySet<string> Permissions,      // effective: granted AND feature on for this user

&#x20;   IReadOnlySet<string> Features,         // features on for this user

&#x20;   IReadOnlySet<int> SupplierRecordIds);  // empty for internal users

```



\- Loaded by UID, cached in `IMemoryCache` for \*\*30 seconds\*\*, loaded at most once per request.

\- Admin changes evict: a user change evicts that user; a role-permission or feature change evicts everyone.

\- Other Cloud Run instances catch up within 30 seconds. That is the agreed revocation target.



\## 9. API



| Method and route | Requires | Notes |

|---|---|---|

| `POST /api/session` | Valid JWT | Provisioning (section 7) |

| `GET /api/me` | Active user | `{ user, roles, permissions, features }` |

| `GET /api/suppliers` (+ all existing `/api/suppliers/{recordId}/…` reads) | `suppliers.view\_all` or `suppliers.view\_own` | External callers filtered to their Record IDs; out of scope = 404 |

| `GET /api/admin/users`, `GET /api/admin/users/{id}` | `users.view` | |

| `POST /api/admin/users/{id}/disable`, `/enable` | `users.manage` | Cannot disable yourself |

| `PUT /api/admin/users/{id}/roles` | `users.manage` | Same type only; cannot remove the last active Site Admin |

| `PUT /api/admin/users/{id}/suppliers` | `users.manage` | External users only |

| `GET /api/admin/invitations` | `users.view` or `users.invite` | |

| `POST /api/admin/invitations` | `users.invite` | 201 + Location |

| `POST /api/admin/invitations/{id}/resend` | `users.invite` | |

| `DELETE /api/admin/invitations/{id}` | `users.invite` | 204 |

| `GET /api/admin/roles` | `roles.view` | |

| `PUT /api/admin/roles/{id}/permissions` | `roles.manage` | Audience check |

| `GET /api/admin/permissions` | `roles.view` | Grouped by feature |

| `GET /api/admin/features` | `features.view` | |

| `PUT /api/admin/features/{id}` | `features.manage` | 409 for a locked feature |

| `PUT /api/admin/features/{id}/targets` | `features.manage` | |

| `GET /api/admin/audit` | `audit.view` | Paged, filterable |



Errors are ProblemDetails with a `reason` extension (`ProblemResults`). Every 403 writes an `access.denied` audit row.



\## 10. Audit events



`session.started`, `user.provisioned`, `invitation.accepted`, `access.denied`, `user.disabled`, `user.enabled`, `user.roles\_changed`, `user.suppliers\_changed`, `invitation.created`, `invitation.resent`, `invitation.revoked`, `role.permissions\_changed`, `feature.state\_changed`, `feature.targets\_changed`. A change and its audit row commit in the same transaction.



\## 11. Later (not V1)



Buyer / Seller / Transporter external roles (many per user, assigned automatically from actions), transporter profiles, custom roles, more sign-in providers, instant cross-instance revocation.

