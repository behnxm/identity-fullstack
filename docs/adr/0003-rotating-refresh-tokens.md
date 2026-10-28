# ADR 0003: Rotating Refresh Tokens with Single-Use Cryptographic Grace Window

## Status
Accepted

## Context
Long-lived, static refresh tokens pose severe security liabilities: if an attacker obtains a refresh token once, they can mint valid access tokens indefinitely until the token reaches its absolute expiration.

To mitigate this, OAuth 2.0 recommends refresh token rotation (issuing a new refresh token on every exchange and invalidating the prior one). However, aggressive single-use rotation introduces fragile failure modes under real-world network conditions: packet loss, mobile connection switches, or overlapping parallel requests can cause the client to lose the newest token or submit the previous token twice, leading to user lockouts.

## Decision
I designed a **rotating refresh token engine with a 30-second cryptographic grace window**:
1. Every successful exchange generates a new 256-bit cryptographically secure token, records its SHA-256 hash in PostgreSQL, and updates the session.
2. The immediately superseded token's hash is retained in `PreviousRefreshTokenHash` with an expiration timestamp set to $t_{now} + 30\text{s}$.
3. If a legitimate client retries using the previous token within this 30-second window, the server returns the newly issued token without failing.
4. Any presentation of an invalidated token *outside* the grace window is recognized as an adversary replaying captured tokens, triggering immediate reuse detection.

## Alternatives Rejected
- **Static Refresh Tokens**: Rejected due to perpetual compromise window.
- **Strict Single-Use Without Grace Window**: Rejected because network drops and parallel browser tab refreshes frequently result in false-positive session terminations and disrupted user experiences.

## Consequences
- **Positive**: Guarantees forward secrecy while providing resilience against packet loss and temporary network disconnects.
- **Positive**: Refresh tokens are stored strictly as SHA-256 hashes, meaning database leaks do not expose live credentials.
- **Trade-off**: Requires maintaining `PreviousRefreshTokenHash` and `PreviousTokenExpiresAt` state on session records.
