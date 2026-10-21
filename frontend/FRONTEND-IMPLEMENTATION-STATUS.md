# Frontend Implementation Status

- **Access model:** follows [`../docs/ACCESS-MODEL.md`](../docs/ACCESS-MODEL.md) (tasks F1–F11 in [`../docs/ACCESS-MODEL-TASKS.md`](../docs/ACCESS-MODEL-TASKS.md)): sign-in through `POST /api/session` and `GET /api/me`, refresh-token sessions, permission-gated menu and routes, `/no-access` and `/invite/accept`.
- **Admin:** Users, Invitations, Roles, Features and Audit log tabs under `/admin`.
- **Not built yet:** supplier editing (the `supplier_edit` feature has no UI to show or hide), and real email delivery for invitations (admins copy the link).
