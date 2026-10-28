# ADR 0013: Anti-Enumeration Protections in Registration and Password Reset

## Status
Accepted

## Context
User account enumeration (disclosing whether a particular email address exists in the system) is a widespread vulnerability in web applications. Attackers leverage registration forms, password reset endpoints, and login error messages to build targeted lists of active users for credential stuffing, spear-phishing, and extortion campaigns.

Disclosing account existence through subtle differences in HTTP status codes, error messages, or response timing violates OWASP ASVS V2.2.1.

## Decision
I designed strict **Anti-Enumeration Safeguards** across all user-facing auth entry points:
1. **Forgot Password Endpoint**: Returns HTTP 200 OK with a generic message regardless of whether the email address is registered: *"If an account matching that email address exists, password reset instructions have been dispatched."*
2. **Resend Confirmation Endpoint**: Returns HTTP 200 OK regardless of whether the email address exists or is already confirmed.
3. **Registration Endpoint**: If an email is already registered, the backend returns HTTP 200/202 and dispatches an instructional email stating that registration was attempted on an existing account, rather than returning HTTP 409 Conflict.
4. **Timing Uniformity**: Operations on non-existent accounts simulate password hashing or execute synthetic dummy operations to eliminate timing side-channel attacks.

## Alternatives Rejected
- **Returning 404 Not Found on Forgot Password**: Rejected because it directly exposes valid user identities.
- **Returning 409 Conflict with "Email already exists"**: Rejected because automated scripts can scan millions of breached email lists to discover which accounts exist on the platform.

## Consequences
- **Positive**: Complete defense against user harvesting and account enumeration bots.
- **Positive**: Fully compliant with OWASP Top 10 A07:2021 and ASVS V2.2.1.
- **Trade-off**: Requires clear, empathetic UI copy to guide users who may have forgotten their registered email.
