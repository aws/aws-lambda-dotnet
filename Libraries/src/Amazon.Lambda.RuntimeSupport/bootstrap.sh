#!/bin/bash

# This script is used to locate 2 files in the /var/task folder, where the end-user assembly is located
# The 2 files are <assembly name>.deps.json and <assembly name>.runtimeconfig.json
# These files are used to add the end-user assembly into context and make the code reachable to the dotnet process
# Since the file names are not known in advance, we use this shell script to find the files and pass them to the dotnet process as parameters
# You can improve cold-start performance by setting the LAMBDA_DOTNET_MAIN_ASSEMBLY environment variable and specifying the assembly name
USER_LAMBDA_BINARIES_DIR="/var/task/"
if [ ! -d "$USER_LAMBDA_BINARIES_DIR" ]; then
    echo "Error: .NET binaries for Lambda function are not correctly installed in the $USER_LAMBDA_BINARIES_DIR directory of the image when the image was built. The $USER_LAMBDA_BINARIES_DIR directory is missing." 1>&2
	exit 1
fi

if [[ `expr index  "$1" ":"` == 0 ]]; then
  EXECUTABLE_ASSEMBLY=$1
  if [[ "$EXECUTABLE_ASSEMBLY" != *.dll ]]; then
    EXECUTABLE_ASSEMBLY="${EXECUTABLE_ASSEMBLY}.dll"
  fi

  if [ ! -f "${USER_LAMBDA_BINARIES_DIR}/${EXECUTABLE_ASSEMBLY}" ]; then
      echo "Error: executable assembly $EXECUTABLE_ASSEMBLY was not found." 1>&2
      exit 1
  fi
  if [ -z "${AWS_LAMBDA_RUNTIME_API}" ]; then
    exec /usr/local/bin/aws-lambda-rie /var/lang/bin/dotnet exec "${USER_LAMBDA_BINARIES_DIR}/${EXECUTABLE_ASSEMBLY}"
  else
    /var/lang/bin/dotnet exec "${USER_LAMBDA_BINARIES_DIR}/${EXECUTABLE_ASSEMBLY}"
  fi
else
  ASSEMBLY_NAME="${LAMBDA_DOTNET_MAIN_ASSEMBLY}"
  if [ -z "$ASSEMBLY_NAME" ]; then
    DEPS_FILE=`find "${USER_LAMBDA_BINARIES_DIR}" -name \*.deps.json -print`
    if [ -z "$DEPS_FILE" ]; then
      echo "Error: .NET binaries for Lambda function are not correctly installed in the $USER_LAMBDA_BINARIES_DIR directory of the image when the image was built. The $USER_LAMBDA_BINARIES_DIR directory is missing the required .deps.json file." 1>&2
      exit 1
    fi
    RUNTIMECONFIG_FILE=`find "${USER_LAMBDA_BINARIES_DIR}" -name \*.runtimeconfig.json -print`
    if [ -z "$RUNTIMECONFIG_FILE" ]; then
      echo "Error: .NET binaries for Lambda function are not correctly installed in the $USER_LAMBDA_BINARIES_DIR directory of the image when the image was built. The $USER_LAMBDA_BINARIES_DIR directory is missing the required .runtimeconfig.json file." 1>&2
      exit 1
    fi
  else
    if [[ "$ASSEMBLY_NAME" == *.dll ]]; then
      ASSEMBLY_NAME="${ASSEMBLY_NAME::-4}"
    fi
    DEPS_FILE="${USER_LAMBDA_BINARIES_DIR}${ASSEMBLY_NAME}.deps.json"
    RUNTIMECONFIG_FILE="${USER_LAMBDA_BINARIES_DIR}${ASSEMBLY_NAME}.runtimeconfig.json"
  fi
  if [ -z "${AWS_LAMBDA_RUNTIME_API}" ]; then
    exec /usr/local/bin/aws-lambda-rie /var/lang/bin/dotnet exec --depsfile $DEPS_FILE --runtimeconfig $RUNTIMECONFIG_FILE /var/runtime/Amazon.Lambda.RuntimeSupport.dll $1
  else
    /var/lang/bin/dotnet exec --depsfile $DEPS_FILE --runtimeconfig $RUNTIMECONFIG_FILE /var/runtime/Amazon.Lambda.RuntimeSupport.dll $1
  fi
fi