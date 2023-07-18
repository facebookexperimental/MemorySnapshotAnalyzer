#!/bin/sh
cp MemorySnapshotAnalyzer/appsettings.json .
cp MemorySnapshotAnalyzer/thirdparty.rcl .
exec dotnet run --project MemorySnapshotAnalyzer/MemorySnapshotAnalyzer.csproj
