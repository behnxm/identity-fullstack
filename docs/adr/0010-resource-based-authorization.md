# ADR 0010: Resource-Based Authorization with Custom OperationAuthorizationRequirements

## Status
Accepted

## Context
Endpoint-level policies (e.g. `RequireAuthorization("notes.manage.own")`) can verify that a user possesses permission to interact with notes in general, but they cannot evaluate whether the user has permission to interact with a *specific, individual note entity*.

Without resource-based authorization, developers typically place ad-hoc checks directly inside endpoint handlers (e.g. `if (note.OwnerId != currentUserId) return Forbid();`). This practice results in duplicated, untested security logic scattered across endpoints, leading to Insecure Direct Object References (IDOR, OWASP A01:2021).

## Decision
I implemented **Resource-Based Authorization** using ASP.NET Core's `IAuthorizationService.AuthorizeAsync(user, resource, requirement)`:
1. Standard operations are defined as `OperationAuthorizationRequirement` instances: `NoteOperations.Read`, `NoteOperations.Update`, `NoteOperations.Delete`.
2. A dedicated `NoteAuthorizationHandler` evaluates operations against the specific `Note` instance:
   - **Owner**: Permitted to execute `Read`, `Update`, and `Delete`.
   - **Moderator**: A caller holding the `notes.moderate` claim and an active MFA claim (`amr: mfa`) is permitted to execute `Read` and `Delete`, but is forbidden from updating another user's note.
3. Every individual note endpoint invokes `authService.AuthorizeAsync` before performing domain modifications.

## Alternatives Rejected
- **Inline Controller Checks (`if (note.OwnerId != userId)`)**: Rejected because scattering authorization logic inside handler bodies causes inconsistent enforcement and prevents unit testing the security policy in isolation.
- **Repository-Level Filtering**: While repository query filters help with list endpoints, they cannot handle complex permission matrices involving moderators, soft deletes, and step-up requirements.

## Consequences
- **Positive**: Centralizes all note access and moderation rules into a single, isolated, fully unit-tested handler (`NoteAuthorizationHandlerTests`).
- **Positive**: Complete defense against IDOR vulnerabilities.
- **Trade-off**: Requires fetching the target entity from the database before the authorization evaluation can execute.
