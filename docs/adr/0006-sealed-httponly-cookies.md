# ADR 0006: Sealed httpOnly SameSite Session Cookies in Nuxt BFF

## Status
Accepted

## Context
In a Backend-for-Frontend (BFF) architecture, the client (browser) must authenticate to the BFF server on each request. The traditional options are either bearer headers containing a session identifier or browser cookies.

Bearer headers require JavaScript client code to hold and attach tokens, re-introducing the risk of token exfiltration through XSS vulnerabilities. Conversely, cookies can be shielded from JavaScript, but require robust configuration to prevent Cross-Site Request Forgery (CSRF) and cookie hijacking.

## Decision
I chose **sealed, encrypted, `httpOnly` session cookies** managed by `nuxt-auth-utils`:
1. **JavaScript Inaccessible (`HttpOnly`)**: The cookie cannot be read via `document.cookie`, neutralizing credential theft from XSS payloads.
2. **Transport Security (`Secure`)**: Enforced in production environments so cookies are only transmitted over TLS channels.
3. **Cross-Site Restrictions (`SameSite=Lax`)**: Prevents the browser from sending session cookies on cross-origin requests, providing baseline CSRF resistance.
4. **Origin & Path Lockdown (`__Host-` prefix)**: In production environments, the cookie is named `__Host-id_session`. Browsers adhering to the Cookie Prefixes specification reject any attempt from subdomains or non-secure origins to overwrite or shadow this cookie.
5. **Sealed Encryption**: Session contents are sealed using the Iron protocol with AES-256-CBC and HMAC-SHA256, ensuring confidentiality and integrity even if the cookie jar is inspected locally.

## Alternatives Rejected
- **Standard Unprefixed Cookies**: Rejected in production because subdomains could manipulate or inject duplicate cookies (`cookie tossing`).
- **Authorization: Bearer <session-id> Header**: Rejected because the client browser script would need to store the token in `localStorage`, leaving it vulnerable to XSS.

## Consequences
- **Positive**: State-of-the-art client credential protection aligned with OWASP ASVS V3 recommendations.
- **Positive**: Seamless session continuity for SSR and client navigations.
- **Trade-off**: Requires dedicated anti-CSRF defenses for state-changing HTTP requests.
