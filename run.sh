#!/bin/bash
# run.sh — build and start the API (workaround for dotnet run WSL2 pipe issue)
set -e

PROJECT="src/VoiceAssistLab.Api"
DLL="src/VoiceAssistLab.Api/bin/Debug/net8.0/VoiceAssistLab.Api.dll"

echo "Building..."
dotnet build "$PROJECT" -v q --nologo

echo ""
echo "Starting VoiceAssist Lab API..."
echo "  → http://localhost:5000"
echo "  → Swagger: http://localhost:5000/swagger"
echo ""

export ASPNETCORE_ENVIRONMENT=Development
exec dotnet "$DLL"
