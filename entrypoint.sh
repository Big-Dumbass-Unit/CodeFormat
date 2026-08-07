#!/bin/sh
set -e
ARGS=""
for p in $INPUT_PATH; do ARGS="$ARGS --path $p"; done
for i in $INPUT_IGNORE; do ARGS="$ARGS --ignore $i"; done
exec dotnet /app/CodeFormat.dll $ARGS