#!/bin/sh
# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0
#
# The image's ENTRYPOINT, at /var/runtime/lambda-entrypoint.sh. It sets the handler and execs
# /var/runtime/bootstrap. A container-per-function image passes its CMD (the handler, such as
# 'handler.ps1') as the first argument, which becomes _HANDLER; a customer-managed runtime may be
# started with no argument, and then the _HANDLER Lambda sets from the function's handler is kept.
# It replaces provided.al2023's /lambda-entrypoint.sh, which exits with status 142 when it is started
# without a handler argument. A handler that is still empty is reported by runtime.ps1 as an init
# error, so the invoke says why. With no Runtime API (a local 'docker run'), the bootstrap runs under
# the base image's Runtime Interface Emulator, as it did under the base entrypoint.
if [ $# -ge 1 ]; then
  export _HANDLER="$1"
fi

if [ -z "${AWS_LAMBDA_RUNTIME_API:-}" ] && [ -x /usr/local/bin/aws-lambda-rie ]; then
  exec /usr/local/bin/aws-lambda-rie /var/runtime/bootstrap
fi
exec /var/runtime/bootstrap
