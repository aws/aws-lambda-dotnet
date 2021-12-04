#!/bin/bash

# This script is used to locate 2 files in the /var/task folder, where the end-user assembly is located
# The 2 files are <assembly name>.deps.json and <assembly name>.runtimeconfig.json
# These files are used to add the end-user assembly into context and make the code reachable to the dotnet process
# Since the file names are not known in advance, we use this shell script to find the files and pass them to the dotnet process as parameters
# You can improve cold-start performance by setting the LAMBDA_DOTNET_MAIN_ASSEMBLY environment variable and specifying the assembly name
USER_LAMBDA_BINARIES_DIR="/var/task"
if [ ! -d "${USER_LAMBDA_BINARIES_DIR}" ]; then
  echo "Error: .NET binaries for Lambda function are not correctly installed in the ${USER_LAMBDA_BINARIES_DIR} directory of the image when the image was built. The ${USER_LAMBDA_BINARIES_DIR} directory is missing." 1>&2
  exit 1
fi

# Get version of Lambda .NET runtime if available
export "$(grep LAMBDA_RUNTIME_NAME /var/runtime/runtime-release 2>/dev/null || echo LAMBDA_RUNTIME_NAME=dotnet_custom)"
export AWS_EXECUTION_ENV="AWS_Lambda_${LAMBDA_RUNTIME_NAME}"

export DOTNET_ROOT="/var/lang/bin"
DOTNET_BIN="${DOTNET_ROOT}/dotnet"
DOTNET_EXEC="exec"
DOTNET_ARGS=()

LAMBDA_HANDLER=""
# Command-line parameter has precedence over "_HANDLER" environment variable
if [ -n "${1}" ]; then
  LAMBDA_HANDLER="${1}"
elif [ -n "${_HANDLER}" ]; then
  LAMBDA_HANDLER="${_HANDLER}"
else
  echo "Error: No Lambda Handler function was specified." 1>&2
  exit 1
fi

if [[ $(expr index "${LAMBDA_HANDLER}" ":") == 0 ]]; then
  EXECUTABLE_ASSEMBLY="${USER_LAMBDA_BINARIES_DIR}"/"${LAMBDA_HANDLER}"
  if [[ "${EXECUTABLE_ASSEMBLY}" != *.dll ]]; then
    EXECUTABLE_ASSEMBLY="${EXECUTABLE_ASSEMBLY}.dll"
  fi

  if [ ! -f "${EXECUTABLE_ASSEMBLY}" ]; then
    echo "Error: executable assembly ${EXECUTABLE_ASSEMBLY} was not found." 1>&2
    exit 1
  fi

  DOTNET_ARGS+=("${EXECUTABLE_ASSEMBLY}")
else
  ASSEMBLY_NAME="${LAMBDA_DOTNET_MAIN_ASSEMBLY}"
  if [ -z "${ASSEMBLY_NAME}" ]; then
    DEPS_FILE=$(find "${USER_LAMBDA_BINARIES_DIR}" -name \*.deps.json -print)
    if [ -z "${DEPS_FILE}" ]; then
      echo "Error: .NET binaries for Lambda function are not correctly installed in the ${USER_LAMBDA_BINARIES_DIR} directory of the image when the image was built. The ${USER_LAMBDA_BINARIES_DIR} directory is missing the required .deps.json file." 1>&2
      exit 1
    fi
    RUNTIMECONFIG_FILE=$(find "${USER_LAMBDA_BINARIES_DIR}" -name \*.runtimeconfig.json -print)
    if [ -z "${RUNTIMECONFIG_FILE}" ]; then
      echo "Error: .NET binaries for Lambda function are not correctly installed in the ${USER_LAMBDA_BINARIES_DIR} directory of the image when the image was built. The ${USER_LAMBDA_BINARIES_DIR} directory is missing the required .runtimeconfig.json file." 1>&2
      exit 1
    fi
  else
    if [[ "${ASSEMBLY_NAME}" == *.dll ]]; then
      ASSEMBLY_NAME="${ASSEMBLY_NAME::-4}"
    fi
    DEPS_FILE="${USER_LAMBDA_BINARIES_DIR}/${ASSEMBLY_NAME}.deps.json"
    RUNTIMECONFIG_FILE="${USER_LAMBDA_BINARIES_DIR}/${ASSEMBLY_NAME}.runtimeconfig.json"
  fi

  DOTNET_ARGS+=("--depsfile" "${DEPS_FILE}"
                "--runtimeconfig" "${RUNTIMECONFIG_FILE}"
                "/var/runtime/Amazon.Lambda.RuntimeSupport.dll" "${LAMBDA_HANDLER}")
fi


# To support runtime wrapper scripts
# https://docs.aws.amazon.com/lambda/latest/dg/runtimes-modify.html#runtime-wrapper
if [ -z "${AWS_LAMBDA_EXEC_WRAPPER}" ]; then
  exec "${DOTNET_BIN}" "${DOTNET_EXEC}" "${DOTNET_ARGS[@]}"
else
  if [ ! -f "${AWS_LAMBDA_EXEC_WRAPPER}" ]; then
    echo "${AWS_LAMBDA_EXEC_WRAPPER}: does not exist"
    exit 127
  fi
  if [ ! -x "${AWS_LAMBDA_EXEC_WRAPPER}" ]; then
    echo "${AWS_LAMBDA_EXEC_WRAPPER}: is not an executable"
    exit 126
  fi
  exec -- "${AWS_LAMBDA_EXEC_WRAPPER}" "${DOTNET_BIN}" "${DOTNET_EXEC}" "${DOTNET_ARGS[@]}"
fi
