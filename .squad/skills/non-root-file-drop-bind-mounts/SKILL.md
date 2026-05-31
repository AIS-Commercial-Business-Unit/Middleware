---
name: "non-root-file-drop-bind-mounts"
description: "How to mount writable local file drop-zones for non-root containers in docker-compose"
domain: "devops"
confidence: "high"
source: "earned"
---

## Context
Use this when a containerized service writes inbound/processed/error files locally and the service runs as a non-root user. This is especially relevant in this repo for file-processing services that must work end-to-end with `docker compose up` on Rancher Desktop.

## Patterns
- Prefer repo-local bind mounts under `.docker-data/` when developers need to inspect generated files on the host.
- For Java/Spring services that create their own directory tree successfully, mount the parent data root (for example `./.docker-data/renewals/java:/app/data`) so the service can create `renewals/inbound`, `processed`, and `error` itself.
- If a non-root runtime fails to create an intermediate parent directory from a fresh mount, bind-mount the terminal subdirectories individually (for example `inbound`, `processed`, `error`) instead of the higher-level parent path.
- Keep local data outside source-controlled paths; rely on `.gitignore` to exclude `.docker-data/`.

## Examples
- `docker-compose.yml`: `platform-file-processing-service` uses `./.docker-data/renewals/java:/app/data` and successfully creates host-visible drop-zone folders.
- `docker-compose.yml`: `dotnet-file-processing` uses three bind mounts for `/app/data/renewals/inbound`, `/processed`, and `/error` to avoid non-root permission failures.

## Anti-Patterns
- Using a named volume when the local workflow requires direct host visibility into generated batch files.
- Assuming all non-root runtimes can create nested folders under a fresh parent bind mount.
- Writing batch files into tracked source directories instead of ignored local data paths.
