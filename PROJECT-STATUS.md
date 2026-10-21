# Project Status

- **Access model:** V1 as specified in [`docs/ACCESS-MODEL.md`](docs/ACCESS-MODEL.md): users, three system roles (`site_admin`, `scrapgo_user`, `user`), permission keys and feature toggles. No organizations, applications or modules.
- **Backend:** done (tasks B1–B18 in [`docs/ACCESS-MODEL-TASKS.md`](docs/ACCESS-MODEL-TASKS.md)): `POST /api/session`, `GET /api/me`, invitations, supplier scoping, and the admin APIs for users, roles, features and audit. See [`backend/README.md`](backend/README.md).
- **Frontend:** not yet moved to the new model (tasks F1–F11); its admin screens still call removed endpoints.
