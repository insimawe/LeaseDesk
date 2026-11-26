#!/bin/sh
set -eu
mkdir -p /app/data
chown -R app:app /app/data
export ASPNETCORE_HTTP_PORTS="${PORT:-8080}"
cd /app
exec runuser -u app -- dotnet LeaseDesk.dll
