---
name: "non-root-volume-bootstrap"
description: "How to make named Docker volumes writable for non-root services without giving up non-root runtime"
domain: "containers"
confidence: "high"
source: "earned"
---

## Context
This skill applies when a container runs as a non-root user but writes into a Docker named volume mounted at runtime. Image-layer directories created in the Dockerfile are hidden by the mounted volume, so the service can still fail with missing-directory or permission errors.

## Patterns
- Keep the application process non-root, but let the container entrypoint start as root long enough to prepare the mounted volume.
- In the entrypoint, `mkdir -p` required subdirectories, then `chown` and `chmod` the mounted root before `exec`-ing the app as the non-root user.
- Add application startup validation that calls `Files.createDirectories(...)` and checks writability for configured paths so filesystem issues fail fast.
- Harden write endpoints to use `Files.createDirectories(...)` instead of ignoring `mkdirs()` return values.

## Examples
- `java/platform-file-processing-service/docker-entrypoint.sh`: prepares `/app/data/renewals/{inbound,processed,error}` and then runs `java -jar /app/app.jar` via `su-exec appuser`.
- `java/platform-file-processing-service/src/main/java/com/ais/middleware/platform/fileprocessing/config/FileProcessingDirectoryInitializer.java`: verifies inbound/processed/error paths on startup.
- `java/platform-file-processing-service/src/main/java/com/ais/middleware/platform/fileprocessing/api/FileBatchController.java`: ensures the inbound directory exists and is writable before creating a batch file.

## Anti-Patterns
- Assuming `RUN mkdir -p ...` in the Dockerfile is enough when a named volume is mounted over that path.
- Leaving a non-root service to create first-run directories inside a root-owned volume.
- Ignoring the boolean return from `mkdirs()` and then diagnosing a later `No such file or directory` error.
- Solving the problem by running the long-lived application process as root.
