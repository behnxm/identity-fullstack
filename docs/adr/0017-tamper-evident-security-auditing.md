# ADR 0017: Dedicated Tamper-Evident Security Auditing Pipeline

## Status
Accepted

## Context
Standard application logs (e.g. `ILogger` console output) are volatile, unstructured, and often intermingle debug diagnostics with critical security events. In production environments, standard log files can be rotated, truncated, or overwhelmed with noise during traffic spikes.

Compliance frameworks (SOC 2, ISO 27001, PCI-DSS) and incident response teams require a structured, tamper-evident, queryable audit trail of authentication and authorization events that persists independently of diagnostic log streams.

## Decision
I built a dedicated **Security Auditing Engine** backed by a dedicated PostgreSQL database table (`SecurityEvents`):
1. **Strongly-Typed Event Taxonomy**: Security events are cataloged as strict string constants (`SecurityEventTypes`):
   - `auth.login_success`, `auth.login_failed`, `auth.token_refreshed`, `auth.token_reuse_detected`
   - `auth.logout`, `auth.2fa_enabled`, `auth.2fa_disabled`, `auth.session_revoked`, `auth.user_locked`, `auth.password_changed`
2. **Context Enrichment**: Every record captures the exact timestamp (UTC), actor user ID, client IP address, User-Agent, and structured JSON metadata.
3. **Dedicated Auditing Service**: `ISecurityAuditor` provides an asynchronous, non-blocking pipeline for capturing events without impacting endpoint latency.
4. **Administrative Query Interface**: Exposes `GET /api/v1/admin/audit-events` protected by policy `audit.read` and active MFA, enabling administrators to filter and inspect security activity.

## Alternatives Rejected
- **Relying Exclusively on stdout / Structured Console Logs**: Rejected because console logs are difficult to query securely in real-time from an administrative UI and can be dropped under logging pipeline backpressure.
- **Third-Party SIEM Webhooks Only**: While integrating with SIEMs is supported in production, having an in-database audit table ensures local forensic availability and self-contained interview demonstrations.

## Consequences
- **Positive**: Reliable, tamper-evident, queryable audit trail for incident analysis and compliance verification.
- **Positive**: Clean separation between operational diagnostics and compliance auditing.
- **Trade-off**: Requires database storage capacity; production deployments should implement partition archiving for events older than 90–365 days.
