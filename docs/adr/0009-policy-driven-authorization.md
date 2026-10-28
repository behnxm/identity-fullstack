# ADR 0009: Policy-Driven Authorization with Roles as Mere Permission Bundles

## Status
Accepted

## Context
Many ASP.NET Core applications implement Role-Based Access Control (RBAC) using role checks directly on controllers or endpoints: `[Authorize(Roles = "Admin,Support")]`. Over time, this anti-pattern leads to:
1. **Role Proliferation & Brittleness**: Adding a new role (e.g. `Auditor`, `BillingManager`) requires hunting down and modifying hundreds of hardcoded `[Authorize]` attributes across endpoints and UI templates.
2. **Coarse-Grained Entitlements**: Users are granted broad, monolithic roles rather than the specific atomic privileges they need for their duties.
3. **Inflexible Security Posture**: Enforcing step-up authentication (e.g. multi-factor authentication) on specific operations cannot be expressed through role strings alone.

## Decision
I designed a strict **Policy-Driven Authorization Model with Atomic Permissions**:
1. **Permissions as Single Source of Truth**: Atomic string constants define specific capabilities: `profile.manage`, `notes.manage.own`, `users.read`, `audit.read`, `users.manage`, `notes.moderate`.
2. **Roles as Permission Bundles**: Roles (`User`, `Support`, `Admin`) exist solely to bundle permission claims in the database. When access tokens are minted, the user's role-claims are flattened into individual `permission` claims inside the JWT.
3. **Policies Govern All Access**: Endpoints and UI gates depend exclusively on named authorization policies registered via `AddAuthorizationBuilder()` (e.g. `panel.user`, `panel.admin`, `users.manage`).
4. **Automated Enforcement**: An automated NetArchTest architecture unit test scans all controllers and minimal API definitions to ensure `[Authorize(Roles = "...")]` is NEVER used in the codebase.

## Alternatives Rejected
- **Direct Role Checks (`Roles = "Admin"`)**: Rejected due to extreme brittleness, inability to express multi-factor requirements, and violation of the principle of least privilege.
- **External ABAC/XACML Policy Engines**: Rejected because ASP.NET Core's built-in `IAuthorizationService` and requirement handlers provide native, high-performance policy evaluation without external runtime dependencies.

## Consequences
- **Positive**: Adding new roles or adjusting permission bundles requires only database updates without touching any C# endpoint code or UI templates.
- **Positive**: Complex requirements (such as requiring MFA for administrative endpoints) compose cleanly into policies.
- **Positive**: Enforces separation of concerns between business capability naming and role administration.
