# Token Lifecycle & Cryptographic Specifications

## 1. Key Management & Asymmetric Cryptography

### Algorithm: ES256 (ECDSA P-256 with SHA-256)
I chose **ES256 (ECDSA P-256 with SHA-256)** over symmetric algorithms like HS256 and legacy asymmetric algorithms like RS256:
- **Zero Shared Secrets**: Verification services only require the public key; private keys never leave the secure boundary of the authorization server.
- **Compact Signatures**: An ECDSA P-256 signature is exactly 64 bytes (two 32-byte integers $r$ and $s$), compared to 256–512 bytes for RSA signatures.
- **High Performance**: Modern hardware accelerates elliptic curve point multiplication, yielding superior throughput and reduced CPU overhead.

### Dual-Key Rollover & JWKS
- **JWKS Endpoint**: `GET /.well-known/jwks.json` exposes active public keys in standard RFC 7517 format.
- **Key Rollover**: The system supports active and retiring keys simultaneously. Tokens signed with retired keys remain valid until their expiration, while all new tokens are signed with the current active key.

---

## 2. Access Token Specification (RFC 9068)

Access tokens adhere to RFC 9068 (JSON Web Token Profile for OAuth 2.0 Access Tokens).

### JWS Header
```json
{
  "alg": "ES256",
  "typ": "at+jwt",
  "kid": "key-2026-q4"
}
```
*Note: `typ: at+jwt` provides explicit token type protection, preventing access tokens from being substituted as ID tokens.*

### JWS Payload
```json
{
  "iss": "https://identity.local/api",
  "aud": "https://identity.local/api",
  "sub": "0192d4f2-9577-781c-9944-7890abcdef12",
  "jti": "0192d4f2-9577-781c-9944-7890abcdef99",
  "iat": 1730000000,
  "nbf": 1730000000,
  "exp": 1730000900,
  "email": "admin@identity.local",
  "email_verified": true,
  "role": ["Admin"],
  "permission": [
    "profile.manage",
    "notes.manage.own",
    "users.read",
    "audit.read",
    "users.manage",
    "notes.moderate"
  ],
  "amr": ["pwd", "mfa"]
}
```

---

## 3. Refresh Token Architecture & Security Lifecycle

```
[ User Request ]
       |
       v
Is access token valid (> 60s remaining)?
       |
       +--- YES ---> [ Proceed with Request ]
       |
       +--- NO ----> [ Check BFF Single-Flight Mutex Map ]
                           |
            +--------------+--------------+
            | Already In-Flight?          | First Request?
            v                             v
      [ Await Existing Promise ]    [ Register Promise in Mutex Map ]
            |                             |
            |                             v
            |                       [ Call POST /api/v1/auth/refresh ]
            |                             |
            |                       +-----+-------------------------------+
            |                       | Token Valid?                        | Token Reused?
            |                       v                                     v
            |                 [ Rotate Token ]                 [ Trigger Reuse Detection ]
            |                 [ 30s Grace Window ]             [ Revoke ALL User Sessions ]
            |                 [ Update Session Cookie ]        [ Log Security Event ]
            |                       |                                     |
            +-----------------------+-------------------------------------+
                                    v
                           [ Return Fresh Token ]
```

### 1. High-Entropy Generation
Refresh tokens consist of 256 bits (32 bytes) of cryptographically secure random bytes generated via `RandomNumberGenerator.GetBytes(32)` and base64url-encoded.

### 2. Opaque Hash Storage
The plain-text refresh token is returned to the client exactly once. Only the **SHA-256 hash** is stored in the `UserSessions` database table. Even in the event of an unauthorized database read, stored hashes cannot be reversed or presented as valid refresh tokens.

### 3. Rotation on Every Request
Every successful exchange of a refresh token issues a brand new refresh token and replaces the stored hash in PostgreSQL.

### 4. 30-Second Grace Window
Under high network latency or parallel browser requests, multiple requests may present the previous refresh token before the response containing the new token has arrived. To prevent false-positive lockouts, the previous token hash is retained for a strict 30-second grace window. Requests within this window receive the newly issued token.

### 5. Automatic Reuse Detection
If a refresh token is presented that has already been replaced **outside** the 30-second grace window:
1. The server flags a **Token Reuse Compromise**.
2. **Every active session** for that user is immediately revoked.
3. A `SecurityEventTypes.TokenReuseDetected` audit entry is created.
4. The client receives an HTTP 401 Unauthorized response.

---

## 4. BFF Single-Flight Mutex Deduplication

To eliminate race conditions before they ever reach the network or database, the Nuxt 4 BFF implements an in-memory single-flight promise map:

```ts
const refreshFlights = new Map<string, Promise<string>>()

export async function getValidAccessToken(event: H3Event): Promise<string> {
  const session = await getUserSession(event)
  const { accessToken, expiresAt, sessionId } = session.tokens

  // 1. Proactive Refresh (60s buffer)
  if (expiresAt - now > 60) {
    return accessToken
  }

  // 2. Await in-flight refresh if already in progress
  if (refreshFlights.has(sessionId)) {
    return await refreshFlights.get(sessionId)!
  }

  // 3. Initiate single-flight refresh
  const flight = (async () => {
    try {
      const refreshed = await apiFetch('/api/v1/auth/refresh', ...)
      await updateSession(event, refreshed)
      return refreshed.accessToken
    } finally {
      refreshFlights.delete(sessionId)
    }
  })()

  refreshFlights.set(sessionId, flight)
  return await flight
}
```
