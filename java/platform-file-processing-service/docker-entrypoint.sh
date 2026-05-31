#!/bin/sh
set -eu

DATA_ROOT="${FILE_DATA_ROOT:-/app/data/renewals}"

mkdir -p "$DATA_ROOT/inbound" "$DATA_ROOT/processed" "$DATA_ROOT/error"
chown -R appuser:appgroup "$DATA_ROOT"
chmod -R ug+rwX "$DATA_ROOT"

exec su-exec appuser java -jar /app/app.jar
