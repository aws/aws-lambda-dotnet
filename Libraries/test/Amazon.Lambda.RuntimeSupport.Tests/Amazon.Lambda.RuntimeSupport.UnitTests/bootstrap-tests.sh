#!/bin/bash
# SPDX-License-Identifier: Apache-2.0

set -eu

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
bootstrap="${script_directory}/../../../src/Amazon.Lambda.RuntimeSupport/bootstrap.sh"
test_directory="$(mktemp -d)"
trap 'rm -rf "${test_directory}"' EXIT

wrapper="${test_directory}/wrapper"
printf '#!/bin/bash\nprintf "%%s\\n" "$@"\n' > "${wrapper}"
chmod +x "${wrapper}"

failures=0
tests=0

run_case() {
  local name="$1" nullglob="$2" expected_exit="$3" expected_message="$4"
  local expected_deps="$5" expected_runtimeconfig="$6"
  shift 6
  local directory="${test_directory}/${name}-${nullglob}"
  mkdir "${directory}"
  for file in "$@"; do
    printf '{}\n' > "${directory}/${file}"
  done

  local actual_exit=0
  env -u BASH_ENV -u SHELLOPTS -u BASHOPTS -u LAMBDA_DOTNET_MAIN_ASSEMBLY -u _HANDLER \
    LAMBDA_TASK_ROOT="${directory}" AWS_LAMBDA_EXEC_WRAPPER="${wrapper}" \
    bash "${nullglob}" nullglob "${bootstrap}" 'Example::Function::Handle' \
    > "${directory}/stdout" 2> "${directory}/stderr" || actual_exit=$?

  tests=$((tests + 1))
  if [ "${actual_exit}" -ne "${expected_exit}" ]; then
    printf 'FAIL %s (%s): expected exit %s, got %s\n' "${name}" "${nullglob}" "${expected_exit}" "${actual_exit}" >&2
    cat "${directory}/stderr" >&2
    failures=$((failures + 1))
    return
  fi

  if [ "${expected_exit}" -ne 0 ]; then
    if ! grep -F -q -- "${expected_message}" "${directory}/stderr"; then
      printf 'FAIL %s (%s): missing diagnostic: %s\n' "${name}" "${nullglob}" "${expected_message}" >&2
      cat "${directory}/stderr" >&2
      failures=$((failures + 1))
    fi
    return
  fi

  printf '%s\n' /var/lang/bin/dotnet exec \
    --depsfile "${directory}/${expected_deps}" \
    --runtimeconfig "${directory}/${expected_runtimeconfig}" \
    /var/runtime/Amazon.Lambda.RuntimeSupport.dll 'Example::Function::Handle' \
    > "${directory}/expected"
  if ! cmp -s "${directory}/expected" "${directory}/stdout" || [ -s "${directory}/stderr" ]; then
    printf 'FAIL %s (%s): unexpected bootstrap arguments or error output\n' "${name}" "${nullglob}" >&2
    cat "${directory}/stderr" >&2
    failures=$((failures + 1))
  fi
}

for nullglob in +O -O; do
  run_case exact "${nullglob}" 0 '' Example.deps.json Example.runtimeconfig.json \
    Example.deps.json Example.runtimeconfig.json
  run_case missing-deps "${nullglob}" 105 'missing the required .deps.json file' '' '' \
    Example.runtimeconfig.json
  run_case ambiguous-deps "${nullglob}" 105 'multiple .deps.json files' '' '' \
    First.deps.json Second.deps.json Example.runtimeconfig.json
  run_case missing-runtimeconfig "${nullglob}" 106 'missing the required .runtimeconfig.json file' '' '' \
    Example.deps.json
  run_case ambiguous-runtimeconfig "${nullglob}" 106 'multiple .runtimeconfig.json files' '' '' \
    Example.deps.json First.runtimeconfig.json Second.runtimeconfig.json
  run_case single-fallback "${nullglob}" 0 '' Alternate.deps.json Alternate.runtimeconfig.json \
    Alternate.deps.json Alternate.runtimeconfig.json
  run_case exact-with-extras "${nullglob}" 0 '' Example.deps.json Example.runtimeconfig.json \
    Example.deps.json Example.runtimeconfig.json First.deps.json Second.deps.json First.runtimeconfig.json Second.runtimeconfig.json
  run_case both-ambiguous "${nullglob}" 105 'multiple .deps.json files' '' '' \
    First.deps.json Second.deps.json First.runtimeconfig.json Second.runtimeconfig.json
  run_case spaced-fallback "${nullglob}" 0 '' 'Alternate assembly.deps.json' 'Alternate assembly.runtimeconfig.json' \
    'Alternate assembly.deps.json' 'Alternate assembly.runtimeconfig.json'
done

printf 'Bootstrap metadata tests: %s passed, %s failed\n' "$((tests - failures))" "${failures}"
[ "${failures}" -eq 0 ]
