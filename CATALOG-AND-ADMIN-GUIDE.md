# Organizations, applications and modules: how-to

This guide covers two jobs:

- **Part 1:** setting up organizations, applications, modules and access **on the site**, with a ready-made test scenario.
- **Part 2:** adding a new application or module to the **catalog in code**, which is the only way applications and modules come into existence.

The design behind it is in [`ORG-APP-MODULE-MODEL.md`](ORG-APP-MODULE-MODEL.md).

## The model in one minute

```
Catalog (code)            Organization (site)                    People (site)
──────────────            ───────────────────                    ─────────────
Application ──────────▶   assigned to an organization ──────▶   application roles granted
  └─ Module   ──────────▶   enabled for that organization ───▶     to members
      └─ Permissions          (licensed)                           (deny by default)
```

| Who | Does what | Where on the site |
| --- | --- | --- |
| **Platform admin** (eric@scrapgo.com, signed in **with Google**) | Creates and deactivates organizations; assigns applications; enables modules; appoints each application's first administrator | Access & Roles → **Organizations** (Applications button), **Applications** tab |
| **Organization admin** | Renames the organization; adds and removes members; manages organization roles | Organizations → **Manage** |
| **Application admin** (holds "{App} Administrator" in that organization) | Grants and revokes that application's roles, with optional expiry | Organizations → **Manage** → **Applications** card |

Rules worth remembering:

- Assigning an application or enabling a module **grants nobody anything**.
- Organization admins **cannot** grant application access. Only application admins and platform admins can.
- A **disabled module** gives no access, whatever role someone holds. Their grants are kept.
- **Removing an application** from an organization revokes every grant for it there, and assigning it again doesn't bring them back.
- **Deactivating an organization** blocks all access to it. Nothing is deleted, and reactivating restores it.

---

## Part 1: Doing it on the site

### Before you start (once)

1. **Apply the catalog migration** so the Downstream application exists in the database. Run this from `backend/`, with the Cloud SQL proxy running and `$C` set as for earlier migrations:
   ```bash
   dotnet ef database update --project src/Modules/Identity/ScrapGo.Core.Modules.Identity.Infrastructure --context IdentityDbContext --connection "$C"
   ```
   This applies `AddDownstreamApplication`, which adds:
   - application **Downstream**
   - modules **Pricing**, **Opportunities**, **Loads & Freight** and **Suppliers**
   - eight permissions (`Downstream.<Module>.Read` and `.Write`)
   - two role templates: **Downstream Administrator** and **Downstream Viewer**
2. **Restart the API** (`dotnet run --project src/ScrapGo.Core.Api`), then the portal (`npm run dev` in `frontend/`).
3. **Sign in as eric@scrapgo.com with "Sign in with Google".** With email and password you won't get platform access; an amber notice says so.
4. **Have a second person sign in once**, e.g. lloyd@scrapgo.com. Users only exist after their first sign-in.

### Test data

| Thing | Value |
| --- | --- |
| Organization 1 | **Midwest Metals**, first administrator: **Eric** |
| Organization 2 | **Great Lakes Recycling**, first administrator: **Lloyd** |
| Application | **Downstream** (from the catalog) |
| Midwest Metals modules | All four on: **Pricing**, **Opportunities**, **Loads & Freight**, **Suppliers** |
| Midwest Metals access | Eric: **Downstream Administrator**. Lloyd: **Downstream Viewer**, expiring in 30 days |

### Step 1: Look up user ids

Access & Roles → **Users**. Note the ids of eric@scrapgo.com and lloyd@scrapgo.com. The id is in the user's page URL (`/admin/users/<id>`).

### Step 2: Create the organizations

Access & Roles → **Organizations** → **New organization**:

| Name | First administrator |
| --- | --- |
| `Midwest Metals` | Eric's user id |
| `Great Lakes Recycling` | Lloyd's user id |

- The first administrator becomes that organization's **OrganizationAdministrator** and a member.
- You can enter an **email address** instead of an id:
  - If that person has signed in before, they become administrator immediately, the same as by id.
  - If not, the site creates an **invitation** and shows its token once. Copy it and send it to them.
  - They then sign in with that email (Google sign-in counts as verified), open **Settings → Accept an invitation**, and paste the token. A link of the form `/invitations?token=<token>` pre-fills it.
- Until an invitation is accepted, the organization has **no members**, so nobody can be appointed to its applications yet.

### Step 3: Assign the application and enable modules

On the **Midwest Metals** row, click **Applications**:

1. Tick **Downstream**. It shows **Assigned**, and its modules appear.
2. Tick all four modules.
   - An application admin can only grant permissions they hold themselves, and a module that's off gives no permissions.
   - So every module in a role must be on before that role can be granted.

### Step 4: Appoint the application administrator

In the same dialog, under **Appoint an application administrator**:

1. **Member user id:** Eric's id. He's a member because he's Midwest Metals' first administrator.
2. **Role:** **Downstream Administrator** (selected by default).
3. Click **Grant**.

Close the dialog.

### Step 5: Add Lloyd to Midwest Metals

On the Midwest Metals row, click **Manage**. It appears for organizations you're a member of. Then, in **Members**, add Lloyd by user id.

### Step 6: Grant access as the application administrator

Still on the Midwest Metals page, scroll to **Applications** → **Downstream**:

1. **Member:** lloyd@scrapgo.com
2. **Role:** **Downstream Viewer**
3. **Expires:** a date 30 days from now
4. Click **Grant**.

**Who has access** now lists:

| Member | Role | Effective permissions |
| --- | --- | --- |
| eric@scrapgo.com | Downstream Administrator | every `Downstream.*` permission, plus `Application.ManageAccess` |
| lloyd@scrapgo.com | Downstream Viewer · until <date> | `Downstream.Pricing.Read`, `Downstream.Opportunities.Read`, `Downstream.Loads.Read`, `Downstream.Suppliers.Read` |

### Step 7: Things to try

| Try | Expected |
| --- | --- |
| Organizations → Applications → untick **Suppliers** | `Downstream.Suppliers.Read` disappears from Lloyd's effective permissions, but his grant is unchanged |
| Tick **Suppliers** again | It comes back |
| Revoke the only **Downstream Administrator** | Refused: an application always keeps at least one administrator |
| Sign in as Lloyd, open Great Lakes Recycling | He's its organization admin, but it has no applications until a platform admin assigns one |
| Organizations → **Deactivate** Midwest Metals | Members lose access to it. **Reactivate** restores it |
| Applications → untick **Downstream** → **Remove application** | Every Downstream grant in Midwest Metals is revoked. Ticking it again restores none |
| Applications tab → **Retire** Opportunities | Retired everywhere. **Reactivate** brings it back |

### Fixing mistakes

These are on each organization's row in Access & Roles → **Organizations** (platform admins):

| Mistake | Fix |
| --- | --- |
| Wrong name | **Edit** → change the name → **Save**. The slug is regenerated from the new name. It's refused if another organization already has that name. |
| Wrong first administrator, or an invitation nobody will accept | **Edit** → **Set administrator** (user id or email of someone who has signed in) → **Set**. They become member and OrganizationAdministrator, and pending invitations are cancelled. To remove the wrong person, the new administrator removes them under **Manage** → Members. |
| The organization shouldn't exist | **Deactivate**, then **Delete**, typing its name to confirm. This permanently removes it with its members, roles, grants, applications and invitations. The audit history is kept, with an `organization_deleted` entry. |

### Clean up

- **Deactivate** each test organization, then **Delete** it.

---

## Part 2: Adding applications and modules in code

The catalog is code, not data: `[RequirePermission]` names permissions at compile time. The site can only **retire or reactivate** catalog entries.

Downstream ([`DownstreamApplication.cs`](backend/src/Modules/Identity/ScrapGo.Core.Modules.Identity.Domain/Applications/DownstreamApplication.cs)) is the worked example.

### Rules

- **Ids are permanent.** Never reuse, renumber or delete an application, module or permission id.
  - Application and module ids must stay **below 900**; tests use 900 and up.
- **Permissions are positional.** A permission's id is its 1-based position in `Permissions.All`. Only **append**, never insert or reorder.
- **Names are global**, `{App}.{Module}.{Action}`, e.g. `Downstream.Loads.Write`. The `{App}` part must equal the application's `Name`.
- **Every application needs an "{App} Administrator" template** holding `Application.ManageAccess` plus all of its module permissions. That's the role a platform admin appoints.
- To withdraw something, **retire** it on the site. Never delete it in code.

### A. Add a new application

Example: **Price Optimizer**, with modules Quotes and Forecasts.

1. **Define it.** Create `backend/src/Modules/Identity/ScrapGo.Core.Modules.Identity.Domain/Applications/PriceOptimizerApplication.cs`, copying the shape of `DownstreamApplication.cs`:
   ```csharp
   public static class PriceOptimizerApplication
   {
       public const int Id = 2;                       // next free application id
       public const int QuotesModuleId = 201;         // next free module ids
       public const int ForecastsModuleId = 202;

       public const string QuotesRead = "PriceOptimizer.Quotes.Read";
       public const string QuotesWrite = "PriceOptimizer.Quotes.Write";
       public const string ForecastsRead = "PriceOptimizer.Forecasts.Read";

       public const string AdministratorRole = "PriceOptimizer Administrator";

       public static readonly IReadOnlyList<string> Permissions = [QuotesRead, QuotesWrite, ForecastsRead];

       public static readonly ApplicationDefinition Definition = new(
           Id, "price-optimizer", "PriceOptimizer",
           [
               new ModuleDefinition(QuotesModuleId, "quotes", "Quotes", [QuotesRead, QuotesWrite]),
               new ModuleDefinition(ForecastsModuleId, "forecasts", "Forecasts", [ForecastsRead]),
           ],
           [
               new RoleTemplateDefinition(AdministratorRole, "Administers PriceOptimizer access.",
                   [Authorization.Permissions.ApplicationManageAccess, .. Permissions]),
           ]);
   }
   ```
   `Name` must match the `{App}` prefix of the permission names; that's why it's `PriceOptimizer`, with no space.
2. **Register it.** In `ApplicationCatalog.cs`, add it to `All`:
   ```csharp
   public static readonly IReadOnlyList<ApplicationDefinition> All =
   [
       DownstreamApplication.Definition,
       PriceOptimizerApplication.Definition,
   ];
   ```
3. **Append its permissions** at the **end** of `Permissions.All` in `Domain/Authorization/Permissions.cs`:
   ```csharp
       .. DownstreamApplication.Permissions,
       .. PriceOptimizerApplication.Permissions,   // ids 33–35
   ];
   ```
4. **Generate the migration** (from `backend/`). `--configuration Release` avoids clashing with a running local API:
   ```bash
   dotnet ef migrations add AddPriceOptimizerApplication --configuration Release \
     --project src/Modules/Identity/ScrapGo.Core.Modules.Identity.Infrastructure \
     --startup-project src/ScrapGo.Core.Api --context IdentityDbContext --output-dir Persistence/Migrations
   ```
   This generates the application, module and permission rows.
5. **Add the role templates to that migration by hand.** Role ids are generated by the database, so EF can't seed them. Copy the `migrationBuilder.Sql(...)` block from `20261007022849_AddDownstreamApplication.cs` and change:
   - the application id
   - the template names and descriptions
   - each template's permission names

   Also add the matching `DELETE` to `Down`.
6. **Test** (needs Docker):
   ```bash
   dotnet test tests/Modules/ScrapGo.Core.Modules.Identity.IntegrationTests
   ```
   `The_code_catalog_is_consistent` checks ids, names and templates. Add a test like `The_downstream_role_templates_are_seeded_as_defined` for the new templates.
7. **Ship it:**
   - Apply the migration to the cloud database (the `database update` command in Part 1).
   - Deploy the API image (`gcloud builds submit` with a new tag, then `gcloud run deploy wrigley-api`).
   - It then appears on the **Applications** tab, and a platform admin assigns it to organizations (Part 1, steps 3–4).

### B. Add a module to an existing application

1. In the application's file, add a module id constant (next free, e.g. `105`), its permission constants, and a `new ModuleDefinition(...)` in `Definition`.
2. Add the new permissions to the **end** of that application's `Permissions` list. Because `Permissions.All` spreads that list, they land at the end of the catalog only if this application is the **last** one in `Permissions.All`.
   - If it isn't, append the new permissions as separate entries at the very end of `Permissions.All` instead.
3. If the "{App} Administrator" template uses `.. Permissions`, it includes the new permissions automatically in code. **The database template doesn't update itself:** in the migration, add SQL that inserts the new `role_permissions` rows for "{App} Administrator", and for any other template that should get them.
4. Generate the migration, test and ship as in A, steps 4–7.
5. On the site, the new module starts **off** in every organization. A platform admin enables it per organization.

### C. Add a permission to an existing module

Same as B, minus the module: add the constant, put it in the module's list and at the **end** of the catalog, then add SQL in the migration to grant it to the templates that should have it.

### D. Use a permission in the API

Protect an endpoint on an application-scoped route. The organization must have the application, the module must be enabled, and the caller must hold the permission there:

```csharp
[HttpGet("/api/organizations/{organizationId:int}/applications/{applicationId:int}/loads")]
[RequirePermission(DownstreamApplication.LoadsRead)]
public async Task<IActionResult> ListLoads(int organizationId, int applicationId, CancellationToken ct) => ...
```

Name the route segments exactly `organizationId` and `applicationId`, or the membership and application checks don't run.

### E. Withdraw something

Never delete from code. On the site:
- **Applications** tab → **Retire** the application or module. It then gives no access anywhere.
- **Reactivate** reverses it.
