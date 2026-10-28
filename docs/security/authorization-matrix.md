# Authorization Matrix: Policies, Roles & Operations

## 1. Permission Claims (Single Source of Truth)

In this architecture, roles are strictly collections of permissions. Authorization rules and UI capabilities depend on policies and permission claims, never on role strings.

| Permission Constant | Target Domain | Description |
|---|---|---|
| `profile.manage` | User Profile | Update own display name and account settings |
| `notes.manage.own` | Notes | Create and query personal notes |
| `users.read` | Admin Users | Query user directory, inspect roles, and view user metadata |
| `audit.read` | Security Auditing | Query and inspect the security audit log stream |
| `users.manage` | Admin Operations | Lock accounts, edit role assignments, revoke active sessions |
| `notes.moderate` | Content Moderation | Inspect and delete any note across the platform |

---

## 2. Seeded System Roles

Roles are stored in the Identity database as bundles of permission claims (`type: "permission"`):

```
+--------------------------------------------------------------------------+
| ADMIN ROLE                                                               |
| Bundles: users.manage, notes.moderate                                    |
| + Inherits all SUPPORT permissions                                       |
+--------------------------------------------------------------------------+
                                    |
                                    v
+--------------------------------------------------------------------------+
| SUPPORT ROLE                                                             |
| Bundles: users.read, audit.read                                          |
| + Inherits all USER permissions                                          |
+--------------------------------------------------------------------------+
                                    |
                                    v
+--------------------------------------------------------------------------+
| USER ROLE                                                                |
| Bundles: profile.manage, notes.manage.own                                |
+--------------------------------------------------------------------------+
```

---

## 3. Named ASP.NET Core Authorization Policies

All policies are registered via `AddAuthorizationBuilder()` and evaluated server-side.

| Policy Name | Requirements | Evaluation Details | Failure Reason |
|---|---|---|---|
| `panel.user` | Authenticated + Verified Email + `profile.manage` | Grants access to User Panel layout and routes (`/app`) | `email_not_confirmed` or `missing_permission` |
| `panel.admin` | Authenticated + `MfaRequirement` + `AnyPermissionRequirement` | Grants access to Admin Console layout and routes (`/admin`) | `mfa_required` or `missing_permission` |
| `users.read` | Authenticated + `MfaRequirement` + `users.read` | Reading user profiles and role assignments | `mfa_required` or `missing_permission` |
| `audit.read` | Authenticated + `MfaRequirement` + `audit.read` | Reading security event audit log | `mfa_required` or `missing_permission` |
| `users.manage` | Authenticated + `MfaRequirement` + `users.manage` | Administrative state changes (lock, roles, revoke) | `mfa_required` or `missing_permission` |
| `notes.moderate` | Authenticated + `MfaRequirement` + `notes.moderate` | Global note moderation | `mfa_required` or `missing_permission` |
| `notes.manage.own` | Authenticated + `notes.manage.own` | Querying and managing own notes | `missing_permission` |

---

## 4. Resource-Based Authorization: Note Access

Personal notes enforce resource-based authorization via `NoteAuthorizationHandler` using ASP.NET Core `OperationAuthorizationRequirement`:

| Operation | Requirement | Owner Permitted? | Moderator Permitted? |
|---|---|:---:|:---:|
| `NoteOperations.Read` | Read Note Details | **YES** | **YES** (requires `notes.moderate` + MFA) |
| `NoteOperations.Update` | Modify Note Content | **YES** | **NO** (forbidden; owner only) |
| `NoteOperations.Delete` | Remove Note | **YES** | **YES** (requires `notes.moderate` + MFA) |

---

## 5. RFC 9457 ProblemDetails Authorization Result Handler

A custom `IAuthorizationMiddlewareResultHandler` intercepts authorization challenges and forbidden results to emit standardized, machine-readable ProblemDetails:

- **HTTP 401 Unauthorized (`auth.unauthenticated`)**: Returned when no token is present or the token is expired, accompanied by a `WWW-Authenticate: Bearer` header.
- **HTTP 403 Forbidden (`auth.mfa_required`)**: Returned when authorization failed specifically due to a missing multi-factor authentication claim (`amr: mfa`).
- **HTTP 403 Forbidden (`auth.forbidden`)**: Returned when caller lacks the necessary permission claims, deliberately withholding information on exactly which permission was lacking.
