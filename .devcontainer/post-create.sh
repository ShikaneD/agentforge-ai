#!/usr/bin/env bash
set -euo pipefail

cd /workspaces/AgentForge
dotnet tool restore
dotnet restore AgentForge.slnx
npm --prefix web ci
