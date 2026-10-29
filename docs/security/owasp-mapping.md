# Security Compliance: ASVS v4.0.3 & OWASP Top 10

## 1. OWASP Top 10 (2021) Mapping

| Risk ID | Category | Project Implementation & Verification |
|---|---|---|
| **A01:2021** | **Broken Access Control** | • Roles are strictly permission bundles; `[Authorize(Roles=...)]` is forbidden and verified by architecture tests.<br>• Resource-based authorization on notes enforces owner-only updates.<br>• BFF route guards evaluate server policies (`panel.admin`, `panel.user`).<br>• Internal API unexposed to public internet. |
| **A02:2021** | **Cryptographic Failures** | • Asymmetric ES256 (ECDSA P-256) signing; no shared secrets.<br>• Dual-key rotation with JWKS endpoint (`/.well-known/jwks.json`).<br>• Refresh tokens stored exclusively as SHA-256 hashes.<br>• Zero secrets in git; `.env` generated with cryptographically random keys. |
| **A03:2021** | **Injection** | • Parameterized SQL via Entity Framework Core and Npgsql.<br>• Strict DTO validation using DataAnnotations and Zod.<br>• Multi-layered CSRF defense with custom headers and Fetch Metadata. |
| **A04:2021** | **Insecure Design** | • Zero-trust Backend-for-Frontend (BFF) pattern.<br>• Browser never sees or handles access or refresh tokens.<br>• Anti-enumeration safeguards across registration, login, and password reset.<br>• Threat model with STRIDE analysis for every trust boundary. |
| **A05:2021** | **Security Misconfiguration** | • Hardened `compose.yaml` with non-root containers and read-only root filesystems.<br>• API runs on `aspnet:10.0-noble-chiseled` (zero shell, non-root user `app`).<br>• Strict Content Security Policy (CSP), HSTS, `X-Frame-Options: DENY`, `nosniff`. |
| **A07:2021** | **Identification & Authentication Failures** | • NIST 800-63B password policy (min 12 chars, no truncation, no composition rules).<br>• Partitioned sliding-window rate limiting on auth endpoints.<br>• RFC 6238 TOTP with step-up AMR claims and single-use recovery codes.<br>• Rotating refresh tokens with grace window and reuse detection. |
| **A08:2021** | **Software & Data Integrity Failures** | • Pinned dependencies in `Directory.Packages.props` and `package-lock.json`.<br>• NuGet vulnerability auditing (`dotnet list package --vulnerable --include-transitive`) and `npm audit` integrated into CI pipeline.<br>• Signed JWT tokens verified with standard ECDSA parameters and `typ: at+jwt`. |
| **A09:2021** | **Security Logging & Monitoring Failures** | • Dedicated `SecurityEvents` database table capturing all auth lifecycle events.<br>• Structured JSON metadata with actor, target, client IP, and user agent.<br>• Real-time security audit log viewer with event filtering. |

---

## 2. OWASP ASVS v4.0.3 Alignment Matrix

### V2: Authentication
- **2.1.1 (Password Length)**: Passwords require at least 12 characters and allow up to 128 characters.
- **2.1.2 (Character Types)**: All printable Unicode characters including whitespace and emojis are permitted.
- **2.1.7 (Password Truncation)**: Passwords are never silently truncated before hashing.
- **2.2.1 (Anti-Enumeration)**: Forgot password, resend confirmation, and registration responses do not disclose whether an account exists.
- **2.8.1 (Two-Factor Authentication)**: Multi-factor authentication implemented via RFC 6238 TOTP (HMAC-SHA1, 30s timestep, 6 digits).
- **2.8.7 (Recovery Codes)**: 10 cryptographically random, single-use recovery codes issued upon enrollment and stored as SHA-256 hashes.

### V3: Session Management
- **3.2.1 (Session Binding)**: Nuxt BFF binds session tokens into an encrypted, sealed cookie.
- **3.4.1 (Cookie Flags)**: Session cookies configured with `HttpOnly`, `Secure`, `SameSite=Lax`, and `__Host-` prefix in production.
- **3.5.1 (Single-Flight Refresh & Rotation)**: Refresh tokens are single-use, rotating on every exchange with automatic reuse detection.
- **3.7.1 (Revocation)**: Explicit logout, remote session revocation, and password changes invalidate active sessions immediately.

### V4: Access Control
- **4.1.1 (Least Privilege)**: Permissions represent atomic operations (`profile.manage`, `users.manage`, etc.); roles are merely convenient claim bundles.
- **4.1.2 (Server-Side Enforcement)**: Every API endpoint enforces authorization policies independently of the client UI state.
- **4.1.3 (Resource-Based Authorization)**: Ownership and moderation privileges on `Note` entities are verified per-instance via `IAuthorizationService.AuthorizeAsync`.

### V14: Configuration
- **14.2.1 (Hardened Containers)**: Distroless/chiseled images eliminate shell binaries (`sh`, `bash`) and package managers from the runtime attack surface.
- **14.4.1 (Security Headers)**: Strict CSP, HSTS, `X-Content-Type-Options: nosniff`, and `X-Frame-Options: DENY` enforced across all responses.
