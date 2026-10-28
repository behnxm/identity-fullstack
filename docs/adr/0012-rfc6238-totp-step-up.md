# ADR 0012: RFC 6238 TOTP with Step-Up AMR Claims and Single-Use Recovery Codes

## Status
Accepted

## Context
Standard password-based authentication alone fails to protect against credential stuffing, phishing, and password reuse across services. High-privilege actions and administrative panels require Multi-Factor Authentication (MFA).

However, introducing MFA requires solving key security challenges:
1. Preventing replay attacks on 6-digit one-time passcodes within their 30-second validity window.
2. Providing a secure recovery mechanism if the user loses their authenticator hardware or device.
3. Enabling seamless step-up authorization in stateless JWT tokens without requiring full session renegotiation.

## Decision
I implemented **RFC 6238 TOTP with Step-Up Authentication Methods Reference (AMR) Claims**:
1. **RFC 6238 Standard**: Generates base32 shared keys, renders QR codes via standard `otpauth://totp/...` URIs, and verifies codes using HMAC-SHA1 over 30-second timesteps.
2. **Replay Protection**: The `AppUser` entity persists `LastTotpStep`. If an attacker or user resubmits the exact same 6-digit code within the current 30-second window, the server rejects it.
3. **Step-Up AMR Claims**: When 2FA is verified, the issued access token carries `amr: ["pwd", "mfa"]`. Endpoints requiring high assurance enforce `MfaRequirement`, checking for the `"mfa"` claim.
4. **Single-Use Recovery Codes**: Upon successful enrollment, 10 cryptographically random 8-character recovery codes are generated, hashed with SHA-256, and stored in the database. When used, the consumed code is deleted permanently.

## Alternatives Rejected
- **SMS / Email One-Time Codes**: Rejected due to vulnerability to SIM-swapping, SS7 interception, and email account compromise.
- **Requiring MFA on Every Request for All Users**: Rejected because requiring 2FA for basic personal notes creates excessive friction for standard users; the system enforces MFA strictly on administrative access and sensitive operations.

## Consequences
- **Positive**: High assurance, zero-trust administrative boundaries meeting NIST SP 800-63B Authenticator Assurance Level 2 (AAL2).
- **Positive**: Single-use recovery codes provide account resilience against lost devices.
- **Trade-off**: Requires users to store recovery codes securely offline.
