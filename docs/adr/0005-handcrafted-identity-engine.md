# ADR 0005: Handcrafted Minimal Identity Engine Over Duende or Third-Party IdPs

## Status
Accepted

## Context
Many modern enterprise architectures delegate authentication to heavy, commercial, or hosted Identity Providers (e.g. Duende IdentityServer, Auth0, Okta, Keycloak). While these products are suited for federated SSO ecosystems, they introduce significant disadvantages for internal reference implementations:
1. Proprietary or restrictive licensing (e.g., Duende's paid commercial license model).
2. Opaque abstractions that hide fundamental cryptographic and security mechanics.
3. Substantial runtime overhead, external operational dependencies, and vendor lock-in.

## Decision
I chose to build a **handcrafted, production-quality, minimal identity and token engine** built directly on top of:
1. ASP.NET Core Identity Core (for secure user storage, password hashing, and token providers).
2. `Microsoft.IdentityModel.JsonWebTokens` (for high-performance, low-allocation JWS creation and validation).
3. Entity Framework Core on PostgreSQL with explicit entity models for sessions and audit events.

Every security decision—from the 256-bit entropy generator to the grace window logic and JWT claim mapping—is written explicitly, audited, and covered by unit and integration tests.

## Alternatives Rejected
- **Duende IdentityServer**: Rejected due to commercial licensing restrictions and excessive protocol complexity for a dedicated BFF topology.
- **ASP.NET Core `MapIdentityApi`**: Rejected because `MapIdentityApi` produces rigid cookie/token endpoints that do not support asymmetric ES256 signing, rotating refresh tokens with grace periods, or custom policy evaluation structures.
- **Hosted Identity Products (Auth0, Firebase)**: Rejected because relying on third-party SaaS obscures the core engineering principles of token generation, key rotation, and session management.

## Consequences
- **Positive**: 100% open-source, permissive MIT/Apache-2.0 licensing with zero vendor dependencies.
- **Positive**: Total control over cryptographic algorithms, claim models, rate limiting, and session lifetimes.
- **Trade-off**: Requires dedicated maintenance and security engineering for all authentication code paths.
