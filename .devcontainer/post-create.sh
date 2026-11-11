#!/usr/bin/env bash
set -euo pipefail

cd /workspaces/agentforge-ai
dotnet --version
dotnet tool restore
dotnet restore AgentForge.slnx
npm --prefix web ci
