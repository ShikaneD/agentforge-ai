#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
env_file="$repo_root/.env"

if [[ -f "$env_file" ]] && grep -q '^MSSQL_SA_PASSWORD=' "$env_file"; then
  exit 0
fi

password="AgentForge!$(openssl rand -hex 24)A9"

printf 'MSSQL_SA_PASSWORD=%s\n' "$password" > "$env_file"
chmod 600 "$env_file"
