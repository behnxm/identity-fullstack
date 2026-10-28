# Identity Fullstack: Zero-Trust Authentication & Authorization Reference Implementation

[![CI Pipeline](https://github.com/behnamnouri/identity-fullstack/actions/workflows/ci.yml/badge.svg)](https://github.com/behnamnouri/identity-fullstack/actions/workflows/ci.yml)
[![.NET 10 LTS](https://img.shields.io/badge/.NET-10.0%20LTS-purple.svg)](https://dotnet.microsoft.com/)
[![Nuxt 4](https://img.shields.io/badge/Nuxt-4.x-00DC82.svg)](https://nuxt.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A gold-standard, production-quality, interview-ready reference implementation of **authentication and authorization with ASP.NET Core 10 Identity and JWTs** (.NET 10 / C# 14) and a **Nuxt 4 + Nuxt UI v4** frontend utilizing the **Backend-for-Frontend (BFF)** pattern.

In this architecture, the browser **NEVER** sees or touches an access token (JWT) or refresh token. Sensitive tokens remain sealed within encrypted, `httpOnly` session cookies on the Nuxt server. After login, access to two completely different panels—a **User Panel** and an **Admin Console**—is governed by server-evaluated policies, never by hardcoded role strings.

---

## Architecture & Topology

```
+-----------------------------------------------------------------------------------------+
|                                    PUBLIC INTERNET                                      |
|                                                                                         |
|       [ Browser Client ]                                                                |
|               |                                                                         |
|               | HTTPS + __Host-id_session (Sealed, httpOnly, SameSite=Lax, Secure)      |
|               v                                                                         |
|     +-------------------+                                                               |
|     |  Nuxt 4 SSR / BFF |  <--- Only published service (:3000)                         |
|     +-------------------+                                                               |
+---------------|-------------------------------------------------------------------------+
                |
+---------------|-------------------------------------------------------------------------+
|               |  INTERNAL DOCKER NETWORK (internal-net, no host ports exposed)          |
|               v                                                                         |
|     +-------------------+              +----------------------+                         |
|     |  .NET 10 API      | -----------> | Mailpit SMTP (:1025) |                         |
|     |  (Chiseled Noble) |              +----------------------+                         |
|     +-------------------+                                                               |
|               |                                                                         |
|               | Parameterized Queries                                                   |
|               v                                                                         |
|     +-------------------+                                                               |
|     | PostgreSQL 17     |                                                               |
|     +-------------------+                                                               |
+-----------------------------------------------------------------------------------------+
```

---

## Core Security Engineering Decisions

| Feature | Implementation | Architectural Rationale |
|---|---|---|
| **Zero Browser Tokens** | Sealed `httpOnly` Session Cookie via `nuxt-auth-utils` | Eliminates token theft via Cross-Site Scripting (XSS). |
| **Asymmetric Token Signing** | ES256 (ECDSA P-256 with SHA-256) | Zero shared secrets; compact 64-byte signatures; JWKS verification endpoint (`/.well-known/jwks.json`). |
| **Token Type Discrimination** | RFC 9068 (`typ: at+jwt`) | Explicit token typing prevents cross-JWT token confusion attacks. |
| **Sliding Refresh Tokens** | 256-bit cryptographically secure entropy | Opaque SHA-256 hash storage; token rotated on every exchange. |
| **Concurrency Grace Window** | 30-second cryptographic window | Previous token hash retained for 30s to withstand network packet loss and mobile reconnects. |
| **Automatic Reuse Invalidation** | Atomic Session Invalidation | Presentation of a rotated token outside the 30s window immediately terminates **all active user sessions**. |
| **BFF Single-Flight Mutex** | In-Memory Promise Deduplication | Coalesces concurrent client refresh requests into a single network flight, eliminating race conditions. |
| **Multi-Layered Anti-CSRF** | `X-Requested-With`, `Sec-Fetch-Site`, Origin | Rejects cross-site state mutations without requiring stateful anti-CSRF synchronizer tokens. |
| **Roles as Permission Bundles** | Atomic permissions; no `[Authorize(Roles=...)]` | Guarded by automated NetArchTest architecture unit tests. |
| **Resource-Based Authorization** | `IAuthorizationService.AuthorizeAsync` | Custom `NoteAuthorizationHandler` guards entities against Insecure Direct Object References (IDOR). |
| **Server Policy Source of Truth** | `GET /api/v1/me/policies` | The API evaluates all named policies for the caller, serving as the single authoritative decision engine for the UI. |
| **RFC 6238 Multi-Factor Auth** | TOTP with Step-Up AMR Claims | 6-digit codes with 30s timestep; replay tracking on `LastTotpStep`; 10 single-use hashed recovery codes. |
| **Anti-Enumeration Safeguards** | Uniform responses & simulated timing | Prevents account harvesting on registration, login, resend-confirmation, and password reset. |
| **URL Fragment Token Secrecy** | `#token=...&userId=...` | RFC 3986 fragments are never transmitted to HTTP servers, preventing token leakage in server logs and `Referer` headers. |
| **Partitioned Rate Limiting** | Sliding window on IP + Normalized Email | Protects against distributed credential stuffing and targeted account password spraying. |
| **Chiseled Non-Root Containers** | `aspnet:10.0-noble-chiseled` | Zero shell (`sh`/`bash`), zero package manager, non-root `app` user (UID 1654), read-only root filesystem. |
| **Security Audit Trail** | Dedicated `SecurityEvents` table | Tamper-evident, structured JSON metadata capturing authentication and session lifecycle events. |
| **HybridCache L1/L2 Caching** | .NET 10 `HybridCache` with Lock-Striping | Microsecond in-process L1 cache with stampede protection for JWKS lookups and session revocation checks. |

---

## Authorization & Policy Matrix

### Permissions & Role Bundles

| Role | Bundled Permissions | Inherited Capabilities |
|---|---|---|
| **User** | `profile.manage`, `notes.manage.own` | Manage own profile and personal notes |
| **Support** | Above + `users.read`, `audit.read` | Read-only inspection of users and security audit events |
| **Admin** | Above + `users.manage`, `notes.moderate` | Account lockout, role updates, session revocation, global note moderation |

### Server-Evaluated Named Policies

```
                                  [ Authenticated User ]
                                            |
                         +------------------+------------------+
                         |                                     |
                         v                                     v
                 [ panel.user ]                                [ panel.admin ]
                         |                                     |
         +---------------+---------------+                     +---------------+---------------+
         |                               |                     |                               |
         v                               v                     v                               v
[ profile.manage ]              [ notes.manage.own ]   [ users.read, audit.read ]     [ users.manage, notes.moderate ]
(User Profile)                  (Personal Notes)       (Support Persona)              (Admin Persona)
```

- **`panel.user`**: Requires authenticated user + confirmed email + `profile.manage`.
- **`panel.admin`**: Requires authenticated user + active MFA (`amr: mfa`) + ANY admin permission (`users.read`, `audit.read`, `users.manage`, `notes.moderate`).
- **Support Persona vs. Admin Persona**: A Support user accesses the Admin Console in a **reduced view** (Users Directory is read-only; action buttons are hidden; Notes Moderation link is hidden). An Admin possesses full write and moderation capabilities.
- **Forced MFA Enrollment Flow**: If a user holds admin permissions but lacks active MFA, `panel.admin` returns `reason: "mfa_required"`. The route guard automatically redirects them to `/app/security?required_mfa=1` with an explanatory banner to complete TOTP enrollment before accessing the Admin Console.

---

## Deliberate Non-Goals & Scope Discipline

To keep the reference architecture focused, audit-ready, and verifiable, the following features are deliberate non-goals:

| Non-Goal | Architectural Rationale | Integration Point |
|---|---|---|
| **Social Logins (Google, GitHub)** | Introduces external IdP dependencies and token mapping complexities that distract from first-party Identity and JWT internals. | Register external auth schemes in `Program.cs` via `services.AddAuthentication().AddGoogle(...)`. |
| **Passkeys / WebAuthn** | Requires hardware authenticator registration and browser FIDO2 APIs, shifting focus away from RFC 6238 TOTP mechanics. | Add `Fido2NetLib` to `Identity.Api` and register WebAuthn credential stores. |
| **Email Change Flow** | Requires dual-verification rituals (verifying both old and new addresses) that expand the domain footprint. | Implement `POST /me/email/request-change` and `POST /me/email/confirm-change`. |
| **CAPTCHA (Cloudflare Turnstile)** | Requires external network calls and commercial API keys during local and CI test runs. | Add a Nuxt server validation hook in `web/server/api/auth/register.post.ts`. |
| **External OpenID Connect Provider** | This service issues first-party access tokens for its own BFF; it does not serve as a third-party OAuth2/OIDC identity provider. | Use OpenIddict if federating external third-party applications is required. |
| **Account Deletion (Self-Service)** | Requires soft-delete, data export (GDPR), and cascade cleanup workflows beyond the core security scope. | Add `POST /me/delete-account` with password confirmation and audit event recording. |

---

## Quick Start & Local Development

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Node.js 22 LTS](https://nodejs.org/) & npm
- PostgreSQL 17 (or Docker)

### 1. Initialize Environment & Cryptographic Keys
Run the initialization script to generate the ECDSA P-256 key pair and random credentials in `.env`:

```bash
# Linux / macOS / WSL
./scripts/init.sh

# Windows (PowerShell)
./scripts/init.ps1
```

### 2. Run with Docker Compose
```bash
docker compose up -d
```
- **Nuxt 4 Web Application**: [http://localhost:3000](http://localhost:3000)
- **Mailpit Web UI (Email Inspection)**: [http://localhost:8025](http://localhost:8025)
- **API JWKS Endpoint**: `http://localhost:3000/api/proxy/../.well-known/jwks.json` (Internal only)

### 3. Run Bare-Metal (Development Mode)
```bash
# Terminal 1: Start Backend API
dotnet run --project api/src/Identity.Api

# Terminal 2: Start Nuxt 4 BFF
npm run dev --prefix web
```

---

## Seeded Demo Personas

All seeded accounts share the initial development password: `Password123!`

| Persona | Email | Assigned Role | Capabilities | Landing Route |
|---|---|---|---|---|
| **Admin** | `admin@identity.local` | `Admin` | Full administrative control, user management, global notes moderation | `/admin` |
| **Support** | `support@identity.local` | `Support` | Read-only user directory inspection, security audit log access | `/admin` (Reduced) |
| **User** | `user@identity.local` | `User` | Personal profile management, personal notes CRUD | `/app` |

---

## Verification & Automated Test Suites

### 1. .NET 10 Test Suite (73/73 Passing)
```bash
dotnet test api/Identity.slnx --logger "console;verbosity=normal"
```
- **`Identity.UnitTests` (32 tests)**: Token generation, ECDSA P-256 signing, TOTP verification, password validation, rate limiting, and result handlers.
- **`Identity.ArchitectureTests` (3 tests)**: Automated NetArchTest enforcing that `[Authorize(Roles = ...)]` is never used and boundary layers are respected.
- **`Identity.IntegrationTests` (38 tests)**: Full database integration tests covering registration, token rotation, grace windows, reuse detection, and session revocation.

### 2. Nuxt 4 BFF Test Suite (30/30 Passing)
```bash
# TypeScript typecheck (zero errors)
npm run typecheck --prefix web

# Vitest unit & security tests
npm test --prefix web
```
- **`composables.test.ts`**: Validates `can()`, `reason()`, `resolveLandingPath()`, and `safeRedirect()` against open-redirect payloads (`//evil.com`, `/\evil.com`, `javascript:`).
- **`csrf.test.ts`**: Verifies rejection of cross-site requests, missing `X-Requested-With` headers, and origin mismatches.
- **`tokens.test.ts`**: Tests proactive refresh and the single-flight mutex locking map.
- **`proxy.test.ts`**: Validates internal route allowlisting (`me`, `notes`, `admin`), 100 KB payload limits, and Content-Type enforcement.
- **`pages-auth.test.ts`**: Static analysis verifying that all page components declare explicit auth metadata.

### 3. Playwright E2E & Accessibility Suite
```bash
npx playwright test --config e2e/playwright.config.ts
```
- End-to-end user registration and Mailpit email verification.
- Two-factor login and recovery code fallback.
- Policy-driven panel routing and 403 Forbidden enforcement.
- Automated **axe-core** WCAG 2.1 AA accessibility auditing.

---

## Documentation & Architecture Deep Dives

- **[STRIDE Threat Model](docs/security/threat-model.md)**: Trust boundaries, attack trees, and mitigation strategies.
- **[Token Lifecycle & Cryptography](docs/security/token-lifecycle.md)**: ES256 key rollover, JWKS, sliding rotation, and reuse detection.
- **[Authorization Matrix](docs/security/authorization-matrix.md)**: Comprehensive mapping of permissions, roles, policies, and requirements.
- **[ASVS & OWASP Compliance](docs/security/owasp-mapping.md)**: Detailed audit against OWASP Top 10 (2021) and ASVS v4.0.3.
- **[18 Architecture Decision Records (ADRs)](docs/adr/)**: Architectural decisions documented with context, alternatives rejected, and consequences.
- **[18 Technical Interview Notes](docs/INTERVIEW-NOTES.md)**: Code-referenced answers to deep-dive interview questions.

---

## License

This repository is licensed under the [MIT License](LICENSE). Every dependency used in this project is strictly open-source (MIT, Apache-2.0, BSD-3-Clause, or ISC).
