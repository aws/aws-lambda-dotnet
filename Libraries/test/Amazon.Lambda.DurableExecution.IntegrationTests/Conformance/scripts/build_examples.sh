#!/bin/bash
# Build .NET Durable Execution conformance test handlers into publish/<Fn>/
# directories that the SAM templates deploy via the makefile BuildMethod.
#
# Each handler project references the in-repo SDK directly (../../../../src/...),
# so no SDK copy/pack step is needed. Each project publishes a self-contained
# `bootstrap` executable for the dotnet8 managed runtime.
#
# Usage:
#   ./build_examples.sh [operation...]
#
# Operations (default: every suite directory found next to the templates):
#   step wait callback child invoke parallel map wait_for_callback wait_for_condition
#
# Examples:
#   ./build_examples.sh step
#   ./build_examples.sh

set -e
set -o pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFORMANCE_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
PUBLISH_DIR="${CONFORMANCE_DIR}/publish"

# Remaining args are operations; default to every suite dir that has handlers.
if [[ $# -gt 0 ]]; then
    OPERATIONS=("$@")
else
    OPERATIONS=()
    for dir in "${CONFORMANCE_DIR}"/*/; do
        op="$(basename "$dir")"
        [[ "$op" == publish ]] && continue
        [[ "$op" == scripts ]] && continue
        # Only treat dirs that actually contain handler projects as suites.
        if find "$dir" -name "*.csproj" -print -quit | grep -q .; then
            OPERATIONS+=("$op")
        fi
    done
fi

echo "Building .NET conformance test handlers..." >&2
echo "  Output:      ${PUBLISH_DIR}" >&2
echo "  Operations:  ${OPERATIONS[*]}" >&2
echo "" >&2

rm -rf "${PUBLISH_DIR}"
mkdir -p "${PUBLISH_DIR}"

for op in "${OPERATIONS[@]}"; do
    OP_DIR="${CONFORMANCE_DIR}/${op}"
    if [[ ! -d "${OP_DIR}" ]]; then
        echo "Warning: Operation directory '${op}/' not found, skipping." >&2
        continue
    fi

    echo "=== Building operation: ${op} ===" >&2

    while IFS= read -r csproj; do
        PROJECT_NAME="$(basename "${csproj}" .csproj)"
        echo "  Publishing ${PROJECT_NAME}..." >&2
        dotnet publish "${csproj}" \
            -c Release \
            -f net8.0 \
            --self-contained false \
            -o "${PUBLISH_DIR}/${PROJECT_NAME}" >&2

        # Makefile that SAM's makefile BuildMethod invokes: copy the pre-built
        # publish output into the SAM artifact directory (the bootstrap binary
        # is already produced above).
        cat > "${PUBLISH_DIR}/${PROJECT_NAME}/Makefile" <<MKEOF
.PHONY: build-${PROJECT_NAME}

build-${PROJECT_NAME}:
	cp -r . \$(ARTIFACTS_DIR)/
	rm -f \$(ARTIFACTS_DIR)/Makefile
MKEOF
    done < <(find "${OP_DIR}" -name "*.csproj")

    echo "" >&2
done

# --- Alias binaries reused under a second function logical id ---
# Some templates register the same binary as a second Lambda (e.g. a tenancy-
# enabled echo target). SAM's makefile build target is keyed on the function
# logical id, so each alias needs its own publish dir + matching Makefile target.
# Format: "<source-project>:<alias-name>". Only aliased when the source was
# actually published in this run (i.e. its suite was selected).
ALIASES=("InvokeEchoTarget:InvokeEchoTargetTenant")
for pair in "${ALIASES[@]}"; do
    src="${pair%%:*}"
    alias="${pair##*:}"
    if [[ -d "${PUBLISH_DIR}/${src}" ]]; then
        echo "  Aliasing ${src} -> ${alias}..." >&2
        rm -rf "${PUBLISH_DIR}/${alias}"
        cp -r "${PUBLISH_DIR}/${src}" "${PUBLISH_DIR}/${alias}"
        cat > "${PUBLISH_DIR}/${alias}/Makefile" <<MKEOF
.PHONY: build-${alias}

build-${alias}:
	cp -r . \$(ARTIFACTS_DIR)/
	rm -f \$(ARTIFACTS_DIR)/Makefile
MKEOF
    fi
done

echo "Build completed successfully!" >&2
echo "  Published to: ${PUBLISH_DIR}" >&2
