// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Xunit;

// Many tests in this assembly mutate the process-global AWS_LAMBDA_FUNCTION_NAME (and related)
// environment variables via EnvironmentVariableHelper to simulate running inside/outside Lambda.
// That state is shared across the whole process, so running test collections in parallel lets one
// test's env var leak into another (e.g. AddAWSLambdaHosting_NotInLambda_DoesNotRegisterHostingOptions
// intermittently sees a value set by a concurrently-running test and fails). Disable in-assembly
// parallelization so these env-var-dependent tests run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
