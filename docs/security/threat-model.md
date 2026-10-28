# Threat Model: STRIDE Security Analysis

## 1. Overview and Trust Boundaries

This document details the threat model for the Identity Fullstack reference implementation. The architecture uses a zero-trust Backend-for-Frontend (BFF) topology where the browser communicates exclusively with the Nuxt 4 BFF server, and the internal ASP.NET Core 10 API is completely unexposed to the public internet.

```
+-------------------------------------------------------------------------------+
| TRUST BOUNDARY 1: Public Internet                                            |
| [ Browser ] <=== HTTPS, Sealed httpOnly Session Cookie ===> [ Nuxt 4 BFF ]   |
+-------------------------------------------------------------------------------+
                                                                 |
+-------------------------------------------------------------------------------+
| TRUST BOUNDARY 2: Internal Docker Network                      |
| [ Nuxt 4 BFF ] <=== Mutual Bearer JWT, Trusted Forwarding ===> [ .NET 10 API ]|
+-------------------------------------------------------------------------------+
                                                                 |
                                       +-------------------------+-------------------------+
                                       |                                                   |
+-------------------------------------------------------------+ +-------------------------------------------------------------+
| TRUST BOUNDARY 3: Storage Network                           | | TRUST BOUNDARY 4: Mail Dispatch Pipeline                    |
| [ .NET 10 API ] <=== Parameterized Npgsql ===> [ PostgreSQL ] | | [ .NET 10 API ] <=== Internal SMTP Channel ===> [ Mailpit ] |
+-------------------------------------------------------------+ +-------------------------------------------------------------+
```

---

## 2. Asset Classification and Sensitivity

| Asset | Storage Location | Sensitivity | Security Control |
|---|---|---|---|
| **User Passwords** | PostgreSQL `AspNetUsers` | Critical | Argon2id / PBKDF2 with HMAC-SHA256, work factor 100,000+ iterations |
| **ECDSA P-256 Private Key** | Docker secrets volume / Key Vault | Critical | Read-only filesystem, never exported, in-memory `ECDsaSecurityKey` |
| **TOTP Shared Keys** | PostgreSQL `AspNetUsers` | Critical | Protected with ASP.NET Core Data Protection DPAPI / AES-256-GCM |
| **Refresh Tokens** | PostgreSQL `UserSessions` | High | Stored exclusively as SHA-256 cryptographic hashes; raw token returned once |
| **Session Cookie** | Browser Cookie Jar | High | `__Host-` prefix, `httpOnly`, `Secure`, `SameSite=Lax`, encrypted payload |
| **JWT Access Tokens** | Nuxt BFF Server Memory | Medium | Ephemeral (15m lifetime), ES256 asymmetric signature, never sent to browser |
| **Security Audit Logs** | PostgreSQL `SecurityEvents` | High | Append-only, structured JSON metadata, tamper-evident audit trail |

---

## 3. STRIDE Threat Analysis by Trust Boundary

### Boundary 1: Browser <-> Nuxt 4 BFF

#### Spoofing
- **Threat**: Attacker crafts illegitimate requests pretending to be an authenticated user.
- **Mitigation**:
  - Encrypted, sealed session cookie managed by `nuxt-auth-utils` using high-entropy secret.
  - Session cookie uses `SameSite=Lax`, `Secure`, and `HttpOnly` flags. In production, prefixed with `__Host-` to enforce origin locking and prevent path manipulation.

#### Tampering
- **Threat**: Attacker tampers with request bodies, session state, or route parameters.
- **Mitigation**:
  - Iron encryption seals session state with AES-256-CBC and HMAC-SHA256 authentication.
  - Zod schemas validate all inbound BFF request payloads before parsing or upstream forwarding.

#### Repudiation
- **Threat**: User claims they did not perform an action (login, password reset, session revocation).
- **Mitigation**:
  - The API captures client IP, user agent, timestamp, actor ID, and metadata into `SecurityEvents` for every critical action.

#### Information Disclosure
- **Threat**: Token leakage via browser history, Referer headers, or cross-site scripting (XSS).
- **Mitigation**:
  - **Zero Browser Tokens**: Neither access tokens (JWTs) nor refresh tokens are ever sent to the browser.
  - **URL Fragment Secrecy**: Email verification and password reset tokens are delivered via URL hash fragments (`#token=...&userId=...`). RFC 3986 specifies that fragments are never transmitted to HTTP servers or included in HTTP Referer headers.
  - **Strict Content Security Policy (CSP)**: `default-src 'self'; script-src 'self'; object-src 'none'; frame-ancestors 'none'`.

#### Denial of Service (DoS)
- **Threat**: Brute-force attacks against login, registration, or password reset endpoints.
- **Mitigation**:
  - ASP.NET Core rate limiting partitions by client IP and normalized email.
  - Fixed-window (10 req/min) on auth endpoints, token bucket on API proxy.
  - 100 KB payload size cap enforced in BFF proxy middleware.

#### Elevation of Privilege
- **Threat**: Standard user invokes admin API endpoints or navigates to the Admin Console.
- **Mitigation**:
  - Global route middleware in Nuxt evaluates permissions against server-returned policies.
  - Backend API enforces `IAuthorizationService` policies (`panel.admin`, `users.manage`, etc.) on every endpoint regardless of UI state.
  - Missing permissions return RFC 9457 ProblemDetails with HTTP 403 Forbidden.

---

### Boundary 2: Nuxt 4 BFF <-> ASP.NET Core 10 API

#### Spoofing & Network Interception
- **Threat**: External clients attempt direct access to internal API endpoints.
- **Mitigation**:
  - API port 8080 is attached exclusively to Docker `internal-net` and never published to the host.
  - The API trusts forwarded client IP and user agent headers (`X-Forwarded-For`) only from the configured BFF CIDR block (`Security:BffNetwork`).

#### Token Reuse & Replay
- **Threat**: Stolen or intercepted refresh token used to obtain unauthorized access tokens.
- **Mitigation**:
  - Refresh tokens are cryptographically rotated on every use.
  - 30-second grace window allows legitimate concurrent client requests to complete.
  - Use of an expired token outside the grace window triggers automatic **reuse detection**: all active sessions for that user account are instantly revoked and a `SecurityEventTypes.TokenReuseDetected` event is logged.
  - BFF single-flight mutex (`refreshFlights`) prevents concurrent token refresh requests from racing.

---

### Boundary 3: Storage Network (API <-> PostgreSQL)

#### SQL Injection
- **Threat**: Attacker injects malicious SQL into user inputs or query parameters.
- **Mitigation**:
  - Entity Framework Core with compiled LINQ queries and parameterized queries via Npgsql.
  - Zero raw string interpolation in SQL queries throughout the entire codebase.

#### Credential Compromise
- **Threat**: Database credentials leaked in git or source control.
- **Mitigation**:
  - Passwords and secrets generated dynamically by `scripts/init.sh` and stored in git-ignored `.env`.
  - Automated unit test audits enforce that no connection strings or credentials exist in source.

---

## 4. Cross-Site Request Forgery (CSRF) Deep Dive

Because session cookies use `SameSite=Lax`, top-level GET navigations could theoretically include cookies, while state-changing requests require multi-layered defense-in-depth:

```
State-Changing Request (POST/PUT/DELETE)
                 |
                 v
   Is method safe (GET/HEAD/OPTIONS)?  ---- YES ----> [ Allow Request ]
                 | NO
                 v
   Is path an /api/ route?              ---- NO  ----> [ Allow Request ]
                 | YES
                 v
   Sec-Fetch-Site is 'cross-site'?      ---- YES ----> [ 403 CSRF Rejected ]
                 | NO
                 v
   X-Requested-With === 'XMLHttpRequest'? -- NO  ----> [ 403 Missing Header ]
                 | YES
                 v
   Origin / Referer host matches Host?  ---- NO  ----> [ 403 Origin Mismatch ]
                 | YES
                 v
          [ Allow Request ]
```
