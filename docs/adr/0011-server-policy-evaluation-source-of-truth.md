# ADR 0011: Server Policy Evaluation API as Single Source of Truth

## Status
Accepted

## Context
In fullstack applications, frontend UIs often duplicate backend authorization logic: they parse user roles or permission arrays in JavaScript and write conditional rendering rules (e.g., `if (user.roles.includes('Admin') && user.has2FA)`).

This duplication inevitably causes logic drift: when a backend policy evolves (for example, requiring email verification or changing MFA rules for support staff), the frontend logic gets out of sync, displaying broken UI elements or exposing navigation links to unauthorized routes.

## Decision
I established the **Backend API as the Single Source of Truth for Policy Evaluation**:
1. The backend exposes `GET /api/v1/me/policies`.
2. When called, the endpoint iterates through EVERY registered named policy and evaluates it against the current caller using the actual ASP.NET Core `IAuthorizationService`.
3. The response returns an authorization dictionary with explicit outcomes and reasons:
   ```json
   {
     "panel.admin": { "allowed": false, "reason": "mfa_required" },
     "panel.user": { "allowed": true, "reason": null },
     "users.read": { "allowed": true, "reason": null }
   }
   ```
4. The Nuxt 4 frontend stores this map in the user session upon login/refresh and provides a simple reactive composable `useCapabilities()` with `can('policy.name')` and `reason('policy.name')`.
5. Frontend route guards and UI components never duplicate permission rules; they evaluate strictly against the server's authoritative decision.

## Alternatives Rejected
- **Duplicating Permission Logic in Frontend**: Rejected due to high maintenance burden, logic drift, and security regressions.
- **Passing Raw Claims and Rebuilding Policy Trees in Nuxt**: Rejected because re-implementing ASP.NET Core `AuthorizationHandler` logic in TypeScript violates DRY and creates two separate security engines.

## Consequences
- **Positive**: Single point of truth. Changing an authorization rule in C# immediately updates the frontend UI behavior upon the next policy sync.
- **Positive**: Provides clear, actionable reasons for denied states (e.g., distinguishing between missing permissions vs. unconfirmed email vs. un-enrolled MFA).
- **Trade-off**: Requires a lightweight policy evaluation call (`GET /me/policies`) during session establishment and step-up transitions.
