#!/bin/sh
cp MemorySnapshotAnalyzer/appsettings.json .
exec dotnet run --project MemorySnapshotAnalyzer/MemorySnapshotAnalyzer.csproj
