# ADR 0008: Multi-Layered CSRF Defense (Custom Header + Fetch Metadata + Origin)

## Status
Accepted

## Context
Because the Nuxt BFF uses session cookies with `SameSite=Lax`, top-level GET navigations (such as following an external link) send cookies automatically. Although `SameSite=Lax` blocks cookies on standard cross-site POST form submissions in modern browsers, relying solely on cookie attributes leaves exposure to:
1. Older or non-compliant browsers.
2. Web navigation edge cases and timing attacks.
3. Subdomain cross-site request attacks.

Traditional anti-CSRF tokens (Synchronizer Token Pattern) require issuing, storing, and synchronizing per-form random tokens, adding state complexity and latency to serverless or stateless deployments.

## Decision
I implemented a **Multi-Layered Defense-in-Depth Anti-CSRF Middleware** (`server/middleware/csrf.ts`):
1. **Safe Methods Exemption**: `GET`, `HEAD`, and `OPTIONS` bypass checks because RFC 9110 specifies they must be safe and idempotent.
2. **Fetch Metadata Inspection**: Modern browsers send `Sec-Fetch-Site`. If `Sec-Fetch-Site` is `cross-site` or `none` on API routes, the request is instantly rejected with HTTP 403.
3. **Custom Header Enforcement**: All state-changing requests (`POST`, `PUT`, `PATCH`, `DELETE`) to `/api/*` MUST include `X-Requested-With: XMLHttpRequest`. Standard HTML `<form>` submissions cannot set custom headers; only asynchronous JavaScript (`fetch`, `XMLHttpRequest`) can set them, subject to CORS preflight checks.
4. **Origin & Referer Validation**: If the `Origin` or `Referer` header is present, its host is validated against the request's `Host` header to ensure it matches the application origin.

## Alternatives Rejected
- **Synchronizer Token Pattern (CSRF cookies + hidden inputs)**: Rejected due to unnecessary state synchronization overhead across distributed SSR instances.
- **Relying Solely on SameSite=Lax**: Rejected because defense-in-depth requires active server-side enforcement independent of browser cookie policies.

## Consequences
- **Positive**: 100% stateless, zero-overhead CSRF protection with zero database or session lookups.
- **Positive**: Compliant with OWASP ASVS 4.2 recommendations for REST/BFF APIs.
- **Trade-off**: Requires client-side fetch abstractions (`useBff()`) to consistently include the `X-Requested-With` header.
