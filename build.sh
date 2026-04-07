#!/usr/bin/env bash
set -euo pipefail
dotnet restore RookieMacOS.sln
dotnet build RookieMacOS.sln
