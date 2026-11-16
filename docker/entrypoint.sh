#!/bin/sh
set -eu
mkdir -p /app/data
export ASPNETCORE_HTTP_PORTS="${PORT:-8080}"
exec dotnet LeaseDesk.dll
