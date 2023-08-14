#!/bin/sh
# Copyright (c) Meta Platforms, Inc. and affiliates.
#
# This source code is licensed under the MIT license found in the
# LICENSE file in the root directory of this source tree.

cp MemorySnapshotAnalyzer/appsettings.json .
cp MemorySnapshotAnalyzer/thirdparty.rcl .
exec dotnet run --project MemorySnapshotAnalyzer/MemorySnapshotAnalyzer.csproj
