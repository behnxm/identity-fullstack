# Technical Interview Notes: Architecture, Cryptography & Security

This guide contains 18 deep-dive technical interview questions and comprehensive answers based directly on this repository's implementation. I wrote these notes as an engineering reference to explain the architectural rationale, trade-offs, and security mechanics of every subsystem.

---

### Q1: Why did you implement the Backend-for-Frontend (BFF) pattern instead of letting the browser handle JWTs directly?
**Answer**:
In traditional Single Page Applications, storing JWT access tokens and refresh tokens in the browser (`localStorage`, `sessionStorage`, or JavaScript memory) exposes them to credential theft via Cross-Site Scripting (XSS). If malicious script executes in the browser context, it can read the stored tokens and transmit them to an adversary's command-and-control server, granting persistent access to the API.

I implemented the **Backend-for-Frontend (BFF)** pattern using Nuxt 4 server routes (`web/server/api/`). The browser never sees or handles raw JWT access tokens or refresh tokens. Instead:
- The Nuxt server receives the tokens upon authentication and seals them inside an encrypted, `httpOnly`, `SameSite=Lax`, `Secure` session cookie (`__Host-id_session`).
- When the browser navigates or requests data, it calls the Nuxt BFF.
- The Nuxt BFF validates the session cookie, extracts the access token, attaches it as a `Bearer` token, and proxies the request to the ASP.NET Core API over an internal, unexposed network.
- The ASP.NET Core API is completely hidden from the public internet and has zero CORS configuration.

---

### Q2: How does your system prevent token theft via Cross-Site Scripting (XSS)?
**Answer**:
I adopted a defense-in-depth posture:
1. **Zero Browser Tokens**: The browser JavaScript runtime cannot steal tokens because tokens are never delivered to the client. The session cookie is marked `HttpOnly`, making it physically inaccessible via `document.cookie`.
2. **Cookie Prefixing (`__Host-`)**: In production, the session cookie uses the `__Host-` prefix. This requires the cookie to be set with `Secure`, from an HTTPS origin, with no domain attribute (origin-only), and path `/`. This prevents subdomains from overwriting or injecting rogue cookies (`cookie tossing`).
3. **Strict Content Security Policy (CSP)**: In `web/nuxt.config.ts`, `nuxt-security` enforces:
   `default-src 'self'; script-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'none'`.
   This prevents unauthorized script injection and clickjacking.

---

### Q3: Walk me through how your refresh token rotation and 30-second grace window work.
**Answer**:
In `api/src/Identity.Api/Security/Sessions/SessionService.cs`:
1. **High Entropy**: Each refresh token is 256 bits of cryptographically secure random bytes (`RandomNumberGenerator.GetBytes(32)`).
2. **One-Way Hash Storage**: The raw token is returned to the BFF once. The database stores only its SHA-256 hash in `UserSessions.CurrentRefreshTokenHash`.
3. **Sliding Rotation**: When exchanged at `/api/v1/auth/refresh`, the server verifies the SHA-256 hash, issues a brand new refresh token, and shifts the old hash into `PreviousRefreshTokenHash`.
4. **Grace Window**: The server sets `PreviousTokenExpiresAt = now + 30 seconds`. If a legitimate client submits the previous token within this 30-second window (due to mobile network switching, packet loss, or overlapping requests), the server returns the existing new token without failing or locking the user out.
5. **Compromise Threshold**: If the previous token is presented *after* the 30-second grace window has expired, the request is flagged as an adversary replaying captured tokens, triggering immediate token reuse detection.

---

### Q4: What happens when a refresh token reuse attack is detected?
**Answer**:
When an expired or rotated refresh token is submitted outside the 30-second grace window, `SessionService.RotateRefreshTokenAsync` detects that an already-consumed token is being replayed.

Rather than simply rejecting the single request, the system assumes that the token family has been compromised by an attacker:
1. **Total Session Revocation**: The server immediately terminates **every active session** associated with that user across all devices (`RevokeAllUserSessionsAsync`).
2. **Audit Event Dispatch**: An immutable high-priority audit entry of type `auth.token_reuse_detected` is recorded with the client IP, user agent, timestamp, and target user ID.
3. **Immediate Invalidation**: The in-memory `HybridCache` session entry is purged.
4. **HTTP 401**: Both the attacker and the victim receive an HTTP 401 Unauthorized ProblemDetails response with error code `auth.token_reuse_detected`. The legitimate user is forced to re-authenticate with their password and 2FA, completely severing the attacker's unauthorized persistence.

---

### Q5: Why did you choose ES256 (ECDSA P-256) instead of RS256 or HS256?
**Answer**:
I evaluated all three options against zero-trust requirements:
- **Why not HS256 (Symmetric)**: Symmetric signing requires every downstream verifying service to hold the exact same secret key used to sign the tokens. If a microservice is compromised, the attacker can forge tokens for the entire system.
- **Why not RS256 (RSA)**: RSA 2048/4096 signatures are 256 to 512 bytes long, significantly increasing HTTP header size. Furthermore, RSA key generation is computationally expensive.
- **Why ES256 (ECDSA P-256)**:
  - **Zero Shared Secrets**: The API signs tokens using an ECDSA private key; verifiers need only the public key fetched from `/.well-known/jwks.json`.
  - **Compact Size**: An ECDSA P-256 signature is exactly 64 bytes (the $r$ and $s$ coordinates of 32 bytes each), yielding minimal header overhead.
  - **High Performance**: Modern server hardware provides hardware acceleration for elliptic curve scalar multiplication, outperforming RSA under high request volumes.

---

### Q6: How does your dual-key rollover work without causing downtime or invalidating existing sessions?
**Answer**:
In `api/src/Identity.Api/Security/Tokens/TokenService.cs`:
1. Keys are identified by unique Key IDs (`kid`).
2. The key manager supports an **Active Key** (used for minting new access tokens) and zero or more **Retiring/Retired Keys** (used strictly for validation during transition periods).
3. The public JWKS endpoint (`/.well-known/jwks.json`) publishes all valid public keys.
4. When a new key is rotated in:
   - The new key becomes active and signs all newly minted tokens.
   - The previous key is moved to retiring status for a duration exceeding the maximum access token lifetime (e.g. 24 hours).
   - Validating middleware accepts tokens signed by either key until existing 15-minute access tokens expire naturally.
   - Once all tokens signed with the retiring key have expired, the retiring key is safely purged.

---

### Q7: How do you defend against Cross-Site Request Forgery (CSRF) when using cookies in the BFF?
**Answer**:
In `web/server/middleware/csrf.ts`, I built a multi-layered defense-in-depth pipeline:
1. **Safe Methods Exemption**: Requests with methods `GET`, `HEAD`, and `OPTIONS` bypass checks because they are defined by HTTP RFCs as safe and idempotent.
2. **Fetch Metadata**: Modern browsers send `Sec-Fetch-Site`. If a request targeting `/api/*` has `Sec-Fetch-Site: cross-site` or `none`, the middleware immediately aborts with HTTP 403 Forbidden (`csrf.rejected`).
3. **Custom Anti-CSRF Header**: All state-changing requests (`POST`, `PUT`, `PATCH`, `DELETE`) must include `X-Requested-With: XMLHttpRequest`. Standard HTML `<form>` submissions cannot attach custom headers; only programmatic JavaScript (`fetch`) can, which browsers subject to CORS preflight checks.
4. **Origin / Referer Verification**: If `Origin` is present, its host must match the request's `Host` header. If absent, the `Referer` host is verified.

---

### Q8: Why are roles treated as mere permission bundles rather than checking roles directly in code?
**Answer**:
Hardcoding role checks (`[Authorize(Roles = "Admin,Support")]`) creates brittle, monolithic systems:
- Changing business rules requires modifying and recompiling endpoint controllers across the codebase.
- Roles do not express fine-grained capabilities or multi-factor authentication requirements.

In this repository:
1. **Permissions are Atomic**: We define string constants (`profile.manage`, `notes.manage.own`, `users.read`, `audit.read`, `users.manage`, `notes.moderate`).
2. **Roles are Database Bundles**: Roles exist solely to assign sets of permission claims to users in the database.
3. **Tokens Carry Permissions**: When issuing access tokens, the user's role-claims are flattened into atomic `permission` claims in the JWT payload.
4. **Endpoints Enforce Policies**: Endpoints require policies (`Policies.UsersManage`), never role names.
5. **Architecture Unit Test**: An automated NetArchTest test (`api/tests/Identity.ArchitectureTests/ArchitectureTests.cs`) scans all compiled assemblies to assert that `[Authorize(Roles = ...)]` is NEVER used.

---

### Q9: How does resource-based authorization work in this repository, and why is it superior to inline endpoint checks?
**Answer**:
Endpoint policies verify broad user capabilities, but cannot assess ownership of a specific data record. Placing ad-hoc checks like `if (note.OwnerId != currentUserId) return Forbid();` inside endpoint actions leads to duplicated, untested code and Insecure Direct Object References (IDOR).

I implemented resource-based authorization via ASP.NET Core's `IAuthorizationService`:
1. Operations are defined as `OperationAuthorizationRequirement` objects: `NoteOperations.Read`, `NoteOperations.Update`, `NoteOperations.Delete`.
2. A dedicated `NoteAuthorizationHandler` centralizes all access rules for `Note` instances:
   - **Owner**: Permitted for `Read`, `Update`, and `Delete`.
   - **Moderator**: A caller possessing `notes.moderate` and an active MFA claim (`amr: mfa`) is permitted for `Read` and `Delete`, but is forbidden from updating another user's note.
3. Endpoints invoke `authService.AuthorizeAsync(caller, note, NoteOperations.Update)`.
4. The security rules are completely decoupled from endpoint logic and covered by dedicated unit tests (`NoteAuthorizationHandlerTests.cs`).

---

### Q10: Explain how the frontend determines which panel (User vs Admin) to show without duplicating server logic.
**Answer**:
Duplicating permission trees in frontend JavaScript leads to logic drift and security discrepancies.
1. The backend exposes `GET /api/v1/me/policies`.
2. The endpoint evaluates EVERY registered authorization policy for the caller using the authoritative server-side `IAuthorizationService`.
3. It returns an exact map:
   ```json
   {
     "panel.admin": { "allowed": true, "reason": null },
     "panel.user": { "allowed": true, "reason": null },
     "users.manage": { "allowed": false, "reason": "missing_permission" }
   }
   ```
4. Nuxt BFF caches this map in the user session upon login or refresh.
5. The `useCapabilities()` composable provides reactive `can(policyName)` and `reason(policyName)` helpers.
6. The panel landing logic evaluates:
   - If `panel.admin.allowed == true`: Navigate to `/admin`.
   - Else if `panel.admin.reason == 'mfa_required'`: Navigate to `/app/security?required_mfa=1` with an enrollment banner.
   - Else if `panel.user.allowed == true`: Navigate to `/app`.
   - Else: Navigate to `/403`.

---

### Q11: How did you implement RFC 6238 TOTP, and how do you protect against code replay?
**Answer**:
1. **RFC 6238 Standard**:
   - Generates a cryptographically random 160-bit base32 shared key.
   - Verification calculates `HMAC-SHA1(sharedKey, timeStep)` where `timeStep = unixTimestamp / 30`.
   - Dynamic truncation extracts the 6-digit code. The algorithm checks the current window and allows a $\pm 1$ window drift ($[-30\text{s}, +30\text{s}]$) to accommodate clock skew.
2. **Replay Protection**:
   - In `AppUser`, the entity tracks `LastTotpStep`.
   - When a code is verified, the server checks if `user.LastTotpStep == currentStep`.
   - If matched, the code has already been consumed within this 30-second window, and the request is rejected with HTTP 400 Bad Request.
   - Upon successful verification, `user.LastTotpStep` is updated to `currentStep`.

---

### Q12: What is an AMR claim, and how does your system enforce step-up authentication for admin access?
**Answer**:
**AMR (Authentication Methods References)** is an RFC 8176 standard claim specifying the identifiers for authentication methods used in the authentication ceremony.
- Upon password login, the minted JWT contains `amr: ["pwd"]`.
- Upon successful TOTP or recovery code verification, the stepped-up token contains `amr: ["pwd", "mfa"]`.
- The `MfaRequirement` custom authorization handler inspects the principal's `amr` claims. If `"mfa"` is absent, authorization fails.
- When an endpoint protected by `panel.admin` or `users.manage` fails due to `MfaRequirement`, the custom `AuthorizationMiddlewareResultHandler` returns HTTP 403 Forbidden with problem details code `auth.mfa_required`.
- The frontend detects `auth.mfa_required` and automatically routes the user to the 2FA enrollment wizard.

---

### Q13: Why are email confirmation and password reset tokens passed via URL hash fragments instead of query parameters?
**Answer**:
When email confirmation links use query parameters (`/confirm-email?token=xyz`):
1. The browser sends the query string to the web server, logging the sensitive one-time token in plain-text inside HTTP server access logs, reverse proxy logs, and CDN logs.
2. If the confirmation page loads external assets (CDNs, analytics, stylesheets), the full URL including the token is leaked in the HTTP `Referer` header to third-party domains.

By using **URL Hash Fragments** (`/confirm-email?userId=...#token=xyz`):
- Per RFC 3986, **URL hash fragments are strictly client-side**: browsers NEVER transmit fragments to the server in HTTP request headers.
- Browsers never include fragments in the `Referer` header.
- On page load, client-side Vue JavaScript reads `window.location.hash`, extracts the token in memory, and submits it in the JSON body of a secure POST request to `/api/auth/confirm-email`.

---

### Q14: How does your system prevent user account enumeration on registration, login, and forgot-password endpoints?
**Answer**:
In accordance with OWASP ASVS V2.2.1:
1. **Forgot Password**: Returns HTTP 200 OK with identical response bodies whether the email exists or not: *"If an account matches that email, reset instructions have been sent."*
2. **Registration**: If an existing email is submitted, the server returns HTTP 200/202 and sends an email to the existing address notifying them that a duplicate registration was attempted, rather than leaking a 409 Conflict.
3. **Timing Normalization**: Password verification on non-existent accounts executes a dummy Argon2id/PBKDF2 hash computation to prevent attackers from measuring cryptographic timing differences between existing and non-existing usernames.

---

### Q15: How does the single-flight mutex in the Nuxt BFF prevent race conditions during token refresh?
**Answer**:
When an SPA opens multiple tabs or fires parallel requests (e.g. 5 concurrent API calls on page load) and the access token has expired:
- Without synchronization, all 5 requests concurrently call the refresh endpoint.
- Because refresh tokens rotate on every use, request #1 succeeds and rotates the token; requests #2 through #5 present the now-rotated token, triggering the reuse detection panic and revoking the entire user session.

The Nuxt BFF solves this in `server/utils/tokens.ts`:
- An in-memory map `refreshFlights = new Map<string, Promise<string>>()` tracks active refresh operations by `sessionId`.
- When request #1 initiates a refresh, it stores the pending promise in `refreshFlights`.
- Requests #2 through #5 detect the existing promise in `refreshFlights` and await it.
- When request #1 resolves with the new token, the promise completes, cleans up the map in a `finally` block, and delivers the exact same refreshed token to all 5 awaiting requests.

---

### Q16: Explain your rate limiting strategy and why dual-key partitioning (IP + Username) is necessary.
**Answer**:
In `api/src/Identity.Api/Security/RateLimiting/`:
- **Why IP Partitioning Alone Fails**: In a distributed credential stuffing attack, botnets distribute requests across thousands of rotating residential IPs. An IP-only rate limiter never triggers because each IP sends only 1 or 2 requests.
- **Why Username Partitioning Alone Fails**: In a password spray attack, an attacker uses a single IP to attempt one password against millions of different usernames. A username-only rate limiter never triggers because each username is only tested once.
- **Dual-Key Solution**:
  1. Auth endpoints enforce a **Sliding Window** rate limiter partitioned by both `Client IP` AND `Normalized Email`.
  2. If an IP sends too many requests across different accounts $\rightarrow$ throttled (429).
  3. If a single account is targeted across multiple IPs $\rightarrow$ throttled (429).
  4. Client IPs are resolved from `X-Forwarded-For` only after verifying that the connection originates from the trusted BFF network CIDR block (`Security:BffNetwork`).

---

### Q17: Why did you choose Ubuntu Chiseled container images and non-root execution for the .NET API?
**Answer**:
Standard base images include bash, curl, package managers, and run as root (UID 0), giving attackers post-exploitation primitives if an application vulnerability exists.

I selected `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`:
1. **Zero Shell**: Chiseled images have no shell (`/bin/sh`, `/bin/bash`), no package manager (`apt`), and no CLI utilities (`curl`, `wget`). An attacker achieving code execution cannot download second-stage malware or spawn reverse shells.
2. **Non-Root**: Executes as the unprivileged user `app` (UID 1654), preventing container escape attacks via kernel exploits that require root privileges.
3. **Read-Only Root Filesystem**: In `compose.yaml`, `read_only: true` prevents malware from writing to disk.

---

### Q18: How does .NET 10 HybridCache protect against cache stampedes during JWKS and session lookups?
**Answer**:
Under heavy load, when a high-traffic cache item expires (such as the active ECDSA public key or session revocation entry), hundreds of concurrent threads simultaneously observe the cache miss and simultaneously query the database or Key Vault. This is known as a **cache stampede** or **thundering herd**.

.NET 10's `HybridCache` eliminates stampedes through built-in **lock striping (coalescing)**:
- When a cache miss occurs, `HybridCache.GetOrCreateAsync` locks on the specific cache key.
- Exactly ONE thread executes the factory delegate to query PostgreSQL.
- All other concurrent callers pause and await the result of that single factory execution.
- Furthermore, `HybridCache` maintains an in-process L1 memory cache backed by an optional out-of-process L2 distributed cache, delivering microsecond response times without cross-cluster cache drift.
