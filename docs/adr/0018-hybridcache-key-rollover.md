# ADR 0018: HybridCache L1/L2 Caching for Key Rollover and Session Revocation

## Status
Accepted

## Context
Validating JWT access tokens on every incoming API request requires looking up public signing keys and verifying that the user's session or account has not been revoked. Querying the PostgreSQL database on every single API call would degrade database throughput and increase latency.

Conversely, standard `IMemoryCache` (L1) does not synchronize across multi-node API clusters, while distributed cache (L2, Redis) adds network round-trip overhead on every request. Furthermore, high-concurrency environments suffer from "cache stampedes" when popular cache keys expire simultaneously.

## Decision
I adopted .NET's new **`HybridCache` framework**:
1. **Two-Tier Architecture**: Combines in-process L1 memory caching with an optional out-of-process L2 distributed cache, delivering microsecond L1 hit speeds with cross-cluster consistency.
2. **Stampede Protection (Lock-Striping)**: If a cache entry expires under heavy load, `HybridCache` coordinates callers so only a single database query is executed, while other callers await the serialized result.
3. **Optimized Key Rollover & Session Caching**:
   - ECDSA public keys and JWKS payloads are cached with long expirations (24h) and invalidated immediately when key rotation is triggered.
   - User session revocation states are cached with brief 60-second time-to-live (TTL), striking an optimal balance between database offloading and timely revocation enforcement.

## Alternatives Rejected
- **Pure `IMemoryCache`**: Rejected because in-memory state does not synchronize across multiple load-balanced API instances, creating stale key windows during rotations.
- **Pure `IDistributedCache`**: Rejected because making network round-trips to Redis on every sub-millisecond JWT validation introduces unnecessary network latency.

## Consequences
- **Positive**: Sub-millisecond JWT key resolution from in-memory L1 cache with zero cache stampede risk.
- **Positive**: Seamless transition from single-node local development to multi-node distributed Redis clusters without altering application code.
- **Trade-off**: Requires .NET 9+ / .NET 10 runtime support.
