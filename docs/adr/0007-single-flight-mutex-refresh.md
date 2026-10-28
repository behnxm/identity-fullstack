# ADR 0007: Single-Flight Mutex Refresh Lock to Eliminate Concurrency Races

## Status
Accepted

## Context
When a user loads a dashboard page, modern SPA frontends typically initiate multiple concurrent data requests simultaneously (e.g. fetching user profile, fetching notes list, fetching unread notifications). If the user's access token expires while on the page, every one of these simultaneous requests would independently detect token expiration and trigger a refresh token exchange.

Because refresh tokens rotate on every exchange, concurrent refresh requests race against each other. The second request would present an already-rotated token, triggering false-positive reuse detection and abruptly terminating the user's session.

## Decision
I implemented an in-memory **Single-Flight Mutex Lock** inside the Nuxt BFF server runtime (`server/utils/tokens.ts`):
1. An in-memory `Map<string, Promise<string>>` tracks in-flight refresh operations keyed by the user's `sessionId`.
2. When a request needs to renew an access token, it checks if a refresh flight is already pending for that session.
3. If pending, the request awaits the existing promise instead of dispatching another network call.
4. If not pending, it creates a new refresh promise, registers it in the map, and executes the single upstream call to `/api/v1/auth/refresh`.
5. Upon completion or failure, the promise is removed from the map in a `finally` block, and all waiting callers receive the exact same fresh access token.

## Alternatives Rejected
- **Client-Side Refresh Queueing**: Rejected because the browser does not own or manage the tokens; delegating token synchronization to client JavaScript violates the zero-trust BFF boundary.
- **Relying Exclusively on Database Grace Windows**: While the backend provides a 30s grace window, allowing dozens of concurrent refreshes creates unnecessary database write load and lock contention.

## Consequences
- **Positive**: Guarantees exactly one network refresh request per expiration event regardless of concurrent UI requests.
- **Positive**: Eliminates race conditions and prevents false-positive token reuse panics.
- **Trade-off**: In multi-instance clustered BFF deployments, node-local maps coordinate requests per instance; combined with the 30-second backend grace window, this provides complete multi-instance safety without needing distributed locks.
