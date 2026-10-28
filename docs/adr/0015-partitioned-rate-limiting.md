# ADR 0015: Sliding-Window and Token-Bucket Partitioned Rate Limiting

## Status
Accepted

## Context
Publicly accessible authentication endpoints are prime targets for automated credential stuffing, password spray attacks, and distributed denial-of-service (DDoS). Simple global rate limiting is insufficient:
- Global limits allow attackers to exhaust API capacity, denying service to legitimate users across the entire platform.
- Simple IP-based limits penalize legitimate corporate users sharing egress NAT gateways.
- Fixed-window limits allow "bursting" across window boundary edges (the "2x limit" problem).

## Decision
I designed a **Multi-Partitioned Rate Limiting Architecture** using ASP.NET Core's built-in `System.Threading.RateLimiting`:
1. **Sliding-Window for Auth Endpoints**: Login, register, and password-reset endpoints enforce sliding-window limiters with 6 segments per window. This eliminates boundary burst attacks.
2. **Dual-Key Partitioning on Authentication**: Login attempts are partitioned by both **Client IP** and **Normalized Username/Email**. An attacker attempting to brute-force a single account from multiple IPs is throttled by the username partition, while an attacker spraying thousands of usernames from one IP is throttled by the IP partition.
3. **Token Bucket on Resource Endpoints**: Personal notes and read queries utilize a token bucket algorithm with replenishment intervals, accommodating natural bursts of user activity while capping sustained throughput.
4. **Trusted Proxy Forwarding**: Client IP resolution strictly inspects forwarded headers (`X-Forwarded-For`) only from the configured BFF CIDR block (`Security:BffNetwork`), preventing attackers from spoofing their IP via fabricated headers.

## Alternatives Rejected
- **Third-Party Rate Limit Middleware (e.g. AspNetCoreRateLimit)**: Rejected in favor of the official, high-performance, allocation-optimized `Microsoft.AspNetCore.RateLimiting` framework introduced in modern .NET.
- **Unpartitioned IP-Only Limiting**: Rejected because it fails to protect specific targeted user accounts from distributed botnet attacks.

## Consequences
- **Positive**: Resilient protection against credential stuffing, brute-force, and resource exhaustion attacks.
- **Positive**: Zero external dependencies (like Redis) required for single-node deployments; easily swappable with distributed Redis partitions.
- **Trade-off**: Requires tuning permit counts and segment numbers based on production traffic profiles.
