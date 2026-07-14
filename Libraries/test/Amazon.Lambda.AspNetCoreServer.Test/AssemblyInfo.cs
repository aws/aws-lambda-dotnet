// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Xunit;

// Tests in this assembly share process-global state: TestSnapStartInitialization sets the
// AWS_LAMBDA_FUNCTION_NAME / AWS_LAMBDA_INITIALIZATION_TYPE environment variables and drives a real
// LambdaBootstrap polling loop, and SnapStartController.Invoked is a static flag. Running test
// collections in parallel lets that background bootstrap and the env-var state interfere with other
// tests (observed in CI as the whole dotnet test process hanging until credentials expired). Disable
// in-assembly parallelization so these tests run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
