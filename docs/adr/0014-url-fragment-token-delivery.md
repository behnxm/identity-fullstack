# ADR 0014: URL Fragment Token Delivery to Prevent HTTP Referer Leakage

## Status
Accepted

## Context
When sending email confirmation or password reset links, web applications traditionally include the sensitive reset token as a query parameter in the URL: `https://example.com/reset-password?token=secret123&userId=abc`.

This design has a critical security flaw:
1. When the user opens the page, the browser issues an HTTP request to the web server that includes the token in the query string, logging the sensitive token in plain-text inside web server access logs, reverse proxy logs, and CDN edge logs.
2. If the reset page loads external resources (e.g. scripts, images, CDNs, or external stylesheets), the browser transmits the full URL—including the reset token—in the HTTP `Referer` header to third-party servers.
3. If the user clicks any outbound link, the reset token is leaked to external sites.

## Decision
I implemented **URL Hash Fragment Token Delivery**:
1. Outgoing emails format confirmation and reset links using URL fragments:
   `https://app.example.com/reset-password?userId=...#token=secret123`
2. Under RFC 3986, the fragment portion of a URI (`#...`) is strictly client-side: **browsers NEVER transmit the URL fragment to the server** in HTTP request lines, and browsers **never include the fragment in HTTP Referer headers**.
3. Neither reverse proxies, CDN edge servers, nor web server access logs ever observe the token.
4. On page mount, client-side Vue/Nuxt code extracts the token from `window.location.hash` and posts it in the JSON body of an AJAX request over TLS.

## Alternatives Rejected
- **Standard Query Parameters (`?token=...`)**: Rejected because sensitive tokens are leaked into access logs, analytics tools, and Referer headers.
- **Short Numeric Codes in Emails**: While workable for SMS/TOTP, manual entry of long high-entropy tokens creates unnecessary user friction compared to one-click links.

## Consequences
- **Positive**: Complete prevention of credential leakage in web server access logs and HTTP Referer headers.
- **Positive**: Aligns with modern defense-in-depth recommendations for password reset tokens.
- **Trade-off**: Requires client-side JavaScript execution to extract the hash fragment and invoke the confirmation API.
