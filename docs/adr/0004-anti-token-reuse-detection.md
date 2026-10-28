# ADR 0004: Anti-Token-Reuse Automatic Session Invalidation

## Status
Accepted

## Context
When refresh token rotation is in place, an attacker who steals a refresh token will attempt to exchange it for a new access token. If the legitimate user also exchanges their token, the system encounters a token that has already been consumed.

A naive response is to return HTTP 400 Bad Request and reject the replayed token. However, doing so allows the attacker (if they refreshed first) to continue using the compromised session while the legitimate user is locked out without explanation.

## Decision
I implemented **Automatic Session Family Invalidation on Token Reuse**:
1. When a refresh token is presented that has already been rotated and its 30-second grace window has elapsed, the authorization engine recognizes a **compromise event**.
2. The system immediately revokes **every active session** associated with that user account across all devices.
3. The invalidation is executed atomically in PostgreSQL and cleared from the in-memory cache.
4. A high-severity security event (`auth.token_reuse_detected`) is recorded in the audit log containing the client IP, user agent, and timestamp.
5. Both the legitimate user and the attacker are forced to re-authenticate from scratch.

## Alternatives Rejected
- **Reject Replay Only**: Rejected because the attacker retains access if they were the first to refresh the stolen token.
- **Revoke Only the Current Session**: Rejected because a compromised device or network attacker likely has access to multiple sessions or cached tokens.

## Consequences
- **Positive**: Protects the victim by guaranteeing that an attacker cannot maintain persistence after token theft.
- **Positive**: Generates high-confidence security signals for SOC and intrusion detection systems.
- **Trade-off**: The legitimate user must re-enter their credentials and 2FA, but this is a necessary and justified security cost when a token has been compromised.
