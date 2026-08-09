#!/usr/bin/env bash
set -euo pipefail

compose=(docker compose -f docker-compose.test.yml)
connection='Host=localhost;Port=55432;Database=child_allowance_manager_test;Username=postgres;Password=postgres'
dotnet_bin="${DOTNET_BIN:-dotnet}"

cleanup() {
  "${compose[@]}" down --volumes --remove-orphans
}
trap cleanup EXIT

"${compose[@]}" up -d --wait
ConnectionStrings__Postgres="$connection" "$dotnet_bin" test ChildAllowanceManager.sln "$@"
