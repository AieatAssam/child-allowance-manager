#!/usr/bin/env bash
set -euo pipefail

compose=(docker compose -f docker-compose.test.yml)
db="${CAM_TEST_DB:-child_allowance_manager_test}"
connection="Host=localhost;Port=55432;Database=${db};Username=postgres;Password=postgres"
dotnet_bin="${DOTNET_BIN:-dotnet}"

cleanup() {
  if [[ "${CAM_TEST_KEEP:-0}" == "1" ]]; then
    # Concurrent agents share the container. Drop only this agent's database.
    "${compose[@]}" exec -T postgres \
      psql -U postgres -d postgres \
      -c "DROP DATABASE IF EXISTS \"${db}\" WITH (FORCE);" >/dev/null 2>&1 || true
    return
  fi
  "${compose[@]}" down --volumes --remove-orphans
}
trap cleanup EXIT

"${compose[@]}" up -d --wait

# The compose file only creates the default database. Create this agent's, if needed.
"${compose[@]}" exec -T postgres \
  psql -U postgres -d postgres \
  -c "SELECT 1 FROM pg_database WHERE datname='${db}'" | grep -q 1 || \
"${compose[@]}" exec -T postgres \
  psql -U postgres -d postgres -c "CREATE DATABASE \"${db}\";"

ConnectionStrings__Postgres="$connection" "$dotnet_bin" test ChildAllowanceManager.sln "$@"
