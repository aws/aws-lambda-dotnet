#!/bin/bash

# .NET on Linux uses OpenSSL to handle certificates. The .NET runtime will load the certs by first reading
# the default cert bundle file which can be overriden by the SSL_CERT_FILE env var. Then it will load the
# certs in the default cert directory which can be overriden by the SSL_CERT_DIR env var. On AL2023
# The default cert bundle file, via symbolic links, resolves to being in a file under the default cert directory.
# This means the default cert bundle file is double loaded causing a cold start performance hit. This logic
# sets the SSL_CERT_FILE to a noop file if SSL_CERT_FILE hasn't been explicitly
# set. This avoid the double load of the default cert bundle file.
if [ -z "${SSL_CERT_FILE}"]; then
  export SSL_CERT_FILE="/tmp/noop"
fi

# This script is used to locate 2 files in the /var/task folder, where the end-user assembly is located
# The 2 files are <assembly name>.deps.json and <assembly name>.runtimeconfig.json
# These files are used to add the end-user assembly into context and make the code reachable to the dotnet process
# Since the file names are not known in advance, we use this shell script to find the files and pass them to the dotnet process as parameters
# You can improve cold-start performance by setting the LAMBDA_DOTNET_MAIN_ASSEMBLY environment variable and specifying the assembly name
# LAMBDA_TASK_ROOT is inherited from the Lambda execution environment/base image as "/var/task", but can be overridden for use in custom images.
if [ -z "${LAMBDA_TASK_ROOT}" ]; then
  echo "Error: Environment variable LAMBDA_TASK_ROOT needs to be defined in order for the Lambda Runtime to load the function handler to be executed." 1>&2
  exit 101
fi

if [ ! -d "${LAMBDA_TASK_ROOT}" ] | [ -z "$(ls -A ${LAMBDA_TASK_ROOT})" ]; then
  echo "Error: .NET binaries for Lambda function are not correctly installed in the ${LAMBDA_TASK_ROOT} directory of the image when the image was built. The ${LAMBDA_TASK_ROOT} directory is missing." 1>&2
  exit 102
fi

# Get version of Lambda .NET runtime if available
export "$(grep LAMBDA_RUNTIME_NAME /var/runtime/runtime-release 2>/dev/null || echo LAMBDA_RUNTIME_NAME=dotnet_custom)"
export AWS_EXECUTION_ENV="AWS_Lambda_${LAMBDA_RUNTIME_NAME}"

export DOTNET_ROOT="/var/lang/bin"
DOTNET_BIN="${DOTNET_ROOT}/dotnet"
DOTNET_EXEC="exec"
DOTNET_ARGS=()
EXECUTABLE_BINARY_EXIST=false

LAMBDA_HANDLER=""
# Command-line parameter has precedence over "_HANDLER" environment variable
if [ -n "${1}" ]; then
  LAMBDA_HANDLER="${1}"
elif [ -n "${_HANDLER}" ]; then
  LAMBDA_HANDLER="${_HANDLER}"
else
  echo "Error: No Lambda Handler function was specified." 1>&2
  exit 103
fi

HANDLER_COL_INDEX=$(expr index "${LAMBDA_HANDLER}" ":")

if [[ "${HANDLER_COL_INDEX}" == 0 ]]; then 
  EXECUTABLE_ASSEMBLY="${LAMBDA_TASK_ROOT}/${LAMBDA_HANDLER}"
  EXECUTABLE_BINARY="${LAMBDA_TASK_ROOT}/${LAMBDA_HANDLER}"
  if [[ "${EXECUTABLE_ASSEMBLY}" != *.dll ]]; then
    EXECUTABLE_ASSEMBLY="${EXECUTABLE_ASSEMBLY}.dll"
  fi
  if [[ -f "${EXECUTABLE_ASSEMBLY}" ]]; then
    DOTNET_ARGS+=("${EXECUTABLE_ASSEMBLY}")
  elif [[ -f "${EXECUTABLE_BINARY}" ]]; then
    EXECUTABLE_BINARY_EXIST=true
  else
    echo "Error: executable assembly ${EXECUTABLE_ASSEMBLY} or binary ${EXECUTABLE_BINARY} not found." 1>&2
    exit 104
  fi
else
  if [ -n "${LAMBDA_DOTNET_MAIN_ASSEMBLY}" ]; then
    if [[ "${LAMBDA_DOTNET_MAIN_ASSEMBLY}" == *.dll ]]; then
      ASSEMBLY_NAME="${LAMBDA_DOTNET_MAIN_ASSEMBLY::-4}"
    else
      ASSEMBLY_NAME="${LAMBDA_DOTNET_MAIN_ASSEMBLY}"
    fi
  else
    ASSEMBLY_NAME="${LAMBDA_HANDLER::${HANDLER_COL_INDEX}-1}"
  fi

  DEPS_FILE="${LAMBDA_TASK_ROOT}/${ASSEMBLY_NAME}.deps.json"
  if ! [ -f "${DEPS_FILE}" ]; then
    DEPS_FILES=( "${LAMBDA_TASK_ROOT}"/*.deps.json )

    # Check if there were any matches to the *.deps.json glob, and that the glob was resolved
    # This makes the matching independent from the global `nullopt` shopt's value (https://www.gnu.org/software/bash/manual/html_node/The-Shopt-Builtin.html)
    if [ "${#DEPS_FILES[@]}" -ne 1 ] || echo "${DEPS_FILES[0]}" | grep -q -F '*'; then
      echo "Error: .NET binaries for Lambda function are not correctly installed in the ${LAMBDA_TASK_ROOT} directory of the image when the image was built. The ${LAMBDA_TASK_ROOT} directory is missing the required .deps.json file." 1>&2
      exit 105
    fi
    DEPS_FILE="${DEPS_FILES[0]}"
  fi

  RUNTIMECONFIG_FILE="${LAMBDA_TASK_ROOT}/${ASSEMBLY_NAME}.runtimeconfig.json"
  if ! [ -f "${RUNTIMECONFIG_FILE}" ]; then
    RUNTIMECONFIG_FILES=( "${LAMBDA_TASK_ROOT}"/*.runtimeconfig.json )

    # Check if there were any matches to the *.runtimeconfig.json glob, and that the glob was resolved
    # This makes the matching independent from the global `nullopt` shopt's value (https://www.gnu.org/software/bash/manual/html_node/The-Shopt-Builtin.html)
    if [ "${#RUNTIMECONFIG_FILES[@]}" -ne 1 ] || echo "${RUNTIMECONFIG_FILES[0]}" | grep -q -F '*'; then
      echo "Error: .NET binaries for Lambda function are not correctly installed in the ${LAMBDA_TASK_ROOT} directory of the image when the image was built. The ${LAMBDA_TASK_ROOT} directory is missing the required .runtimeconfig.json file." 1>&2
      exit 106
    fi
    RUNTIMECONFIG_FILE="${RUNTIMECONFIG_FILES[0]}"
  fi

  DOTNET_ARGS+=("--depsfile" "${DEPS_FILE}"
                "--runtimeconfig" "${RUNTIMECONFIG_FILE}"
                "/var/runtime/Amazon.Lambda.RuntimeSupport.dll" "${LAMBDA_HANDLER}")
fi


# To support runtime wrapper scripts
# https://docs.aws.amazon.com/lambda/latest/dg/runtimes-modify.html#runtime-wrapper
if [ -z "${AWS_LAMBDA_EXEC_WRAPPER}" ]; then
  if [ ${EXECUTABLE_BINARY_EXIST} = true ]; then
    exec "${EXECUTABLE_BINARY}"
  else
    exec "${DOTNET_BIN}" "${DOTNET_EXEC}" "${DOTNET_ARGS[@]}"
  fi
else
  if [ ! -f "${AWS_LAMBDA_EXEC_WRAPPER}" ]; then
    echo "${AWS_LAMBDA_EXEC_WRAPPER}: does not exist"
    exit 127
  fi
  if [ ! -x "${AWS_LAMBDA_EXEC_WRAPPER}" ]; then
    echo "${AWS_LAMBDA_EXEC_WRAPPER}: is not an executable"
    exit 126
  fi
  if [ ${EXECUTABLE_BINARY_EXIST} = true ]; then
    exec -- "${AWS_LAMBDA_EXEC_WRAPPER}" "${EXECUTABLE_BINARY}"
  else
    exec -- "${AWS_LAMBDA_EXEC_WRAPPER}" "${DOTNET_BIN}" "${DOTNET_EXEC}" "${DOTNET_ARGS[@]}"
  fi
fi