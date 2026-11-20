#!/bin/bash
# Publish the handlers once into publish/ and write the Makefile SAM's makefile BuildMethod needs.
# Every function in template.yaml shares the one bundle, so each logical id gets a target that copies it.
#
# Usage: ./scripts/build_examples.sh

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PUBLISH_DIR="${ROOT}/publish"

rm -rf "${PUBLISH_DIR}"
dotnet publish "${ROOT}/Amazon.Lambda.DurableExecution.ConformanceTests.Otel.csproj" \
    -c Release --self-contained false -o "${PUBLISH_DIR}" >&2

{
    # Logical ids are the two-space-indented keys under Resources whose type is a function.
    for id in $(awk '/^Resources:/{r=1;next} r && /^  [A-Za-z0-9]+:$/{gsub(/[ :]/,"");print}' "${ROOT}/template.yaml"); do
        printf '.PHONY: build-%s\nbuild-%s:\n\tcp -r . $(ARTIFACTS_DIR)/\n\trm -f $(ARTIFACTS_DIR)/Makefile\n\n' "${id}" "${id}"
    done
} > "${PUBLISH_DIR}/Makefile"

echo "Published to ${PUBLISH_DIR}" >&2
