#!/bin/sh
# Copyright (c) Meta Platforms, Inc. and affiliates.
#
# This source code is licensed under the MIT license found in the
# LICENSE file in the root directory of this source tree.

SCRIPT=$(readlink -f "$0")
BASEDIR=$(dirname "$SCRIPT")
exec dotnet "$BASEDIR"/MemorySnapshotAnalyzer.exe "$@"
