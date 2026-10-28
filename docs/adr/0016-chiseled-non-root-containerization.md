# ADR 0016: Non-Root Chiseled Containerization with Zero-Shell Footprint

## Status
Accepted

## Context
Standard Docker container images typically ship with full Linux userlands: package managers (`apt`, `apk`), shells (`bash`, `sh`), network utilities (`curl`, `wget`), and compiler toolchains.

If an attacker achieves remote code execution or arbitrary command execution inside such a container, the presence of these utilities allows them to:
1. Download external rootkits or malware using `curl` or `wget`.
2. Spawn interactive shells (`/bin/sh`) or reverse TCP shells.
3. Attempt local privilege escalation to root.
4. Scan internal Docker networks using pre-installed networking tools.

## Decision
I containerized the ASP.NET Core 10 API using **Ubuntu Noble Chiseled Runtime Images** (`mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`):
1. **Zero Shell Footprint**: Chiseled images contain ONLY the .NET runtime and essential shared C libraries (glibc, OpenSSL). There is **no shell (`sh`, `bash`)**, no package manager, and no command-line utilities.
2. **Non-Root Execution**: The container runs under the dedicated, unprivileged `app` user (UID 1654).
3. **Read-Only Root Filesystem**: The root filesystem is mounted read-only (`read_only: true` in `compose.yaml`), with `/tmp` mounted as an isolated memory-backed `tmpfs`.
4. **Minimal Attack Surface**: Vulnerability scanners report virtually zero Common Vulnerabilities and Exposures (CVEs) compared to standard Debian/Ubuntu base images.

For the web tier, `node:22-alpine` similarly runs under the unprivileged `node` user (UID 1000) with unnecessary packages removed.

## Alternatives Rejected
- **Standard Debian/Ubuntu Images (`aspnet:10.0`)**: Rejected due to hundreds of megabytes of unnecessary utilities and higher CVE counts.
- **Running as Root User**: Rejected because root in a container maps to root on the host if user namespaces are misconfigured, risking container escape.

## Consequences
- **Positive**: Hardens the runtime environment against post-exploitation activities; attackers cannot execute shell scripts or download payloads.
- **Positive**: Tiny image sizes (~110 MB) yielding rapid container pulls and minimal memory usage.
- **Trade-off**: Interactive debugging inside the running container requires ephemeral debug containers (`docker debug`) rather than traditional `docker exec -it ... /bin/sh`.
