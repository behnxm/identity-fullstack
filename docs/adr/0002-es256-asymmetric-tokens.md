# ADR 0002: Asymmetric ES256 (ECDSA P-256) JWS Over Symmetric HS256

## Status
Accepted

## Context
When signing JSON Web Tokens (JWTs), systems typically choose between symmetric algorithms (e.g. HS256) or asymmetric algorithms (e.g. RS256 or ES256).

Symmetric signing requires every service that validates a token to share the exact same cryptographic secret key used to sign the token. In microservice architectures or multi-tier deployments, sharing private keys across service boundaries creates severe secret sprawl and expands the blast radius: if any consumer service is compromised, attackers can mint arbitrary forged tokens.

## Decision
I chose **ES256 (ECDSA over curve P-256 with SHA-256)** for all access token signing:
1. The ASP.NET Core API authorization service holds the ECDSA private key and never shares it.
2. The authorization server exposes its public keys in standard JSON Web Key Set (JWKS) format at `/.well-known/jwks.json`.
3. Verifying services require only the public key to validate token authenticity.
4. Access tokens declare `typ: "at+jwt"` in accordance with RFC 9068 to prevent cross-JWT confusion attacks.

## Alternatives Rejected
- **HS256 (HMAC-SHA256)**: Rejected because sharing the symmetric signing secret with validating services violates the principle of least privilege and prevents secure delegation.
- **RS256 (RSA 2048/4096)**: Rejected because RSA signatures are large (256–512 bytes vs. 64 bytes for ES256), leading to bloated HTTP headers and higher memory and network serialization overhead.

## Consequences
- **Positive**: Zero shared secrets across service boundaries.
- **Positive**: Extremely compact 64-byte signature representation.
- **Positive**: High throughput point-multiplication performance on modern CPU architectures.
- **Trade-off**: Slightly higher CPU cost during token creation compared to symmetric HMAC, but well within acceptable thresholds.
