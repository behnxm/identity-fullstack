# ADR 0001: Backend-for-Frontend (BFF) Pattern Over Direct Browser Tokens

## Status
Accepted

## Context
Traditional Single Page Applications (SPAs) often receive access and refresh tokens directly in the browser, storing them in `localStorage`, `sessionStorage`, or JavaScript-accessible memory. In this model, any Cross-Site Scripting (XSS) vulnerability grants attackers immediate, unrestricted access to the user's refresh tokens and API access credentials, enabling silent session hijacking from remote machines.

Furthermore, direct browser-to-API communication forces the internal backend service to configure Cross-Origin Resource Sharing (CORS), exposing internal API surfaces directly to the internet.

## Decision
I chose to implement the **Backend-for-Frontend (BFF) pattern** using Nuxt 4 server routes (`/api/auth/*` and `/api/proxy/*`).

In this design:
1. The browser NEVER sees or receives a JWT access token or a refresh token.
2. Tokens are stored exclusively on the server, encrypted within a sealed, `httpOnly`, `SameSite=Lax`, `Secure` session cookie managed by `nuxt-auth-utils`.
3. The Nuxt server acts as a confidential client, validating session authenticity and proxying API requests to the ASP.NET Core 10 backend on the browser's behalf.
4. The ASP.NET Core API has zero CORS configuration because it sits entirely within the private container network and accepts requests solely from the BFF.

## Alternatives Rejected
- **Local Storage / Session Storage JWT Storage**: Rejected due to catastrophic susceptibility to XSS exfiltration.
- **In-Memory JWT Storage with Silent Refresh iframe**: Rejected because third-party cookie restrictions in modern browsers break silent refresh iframes, and refresh token theft remains possible via malicious scripts.
- **API Directly Setting Auth Cookies**: Rejected because it requires exposing the API directly to the browser with complex cross-subdomain cookie and CORS configurations.

## Consequences
- **Positive**: Complete elimination of token theft via XSS; the browser cannot exfiltrate what it cannot see.
- **Positive**: The internal API surface is completely insulated from public network probing.
- **Trade-off**: Requires running and maintaining a Node.js SSR/BFF tier alongside the .NET backend.
