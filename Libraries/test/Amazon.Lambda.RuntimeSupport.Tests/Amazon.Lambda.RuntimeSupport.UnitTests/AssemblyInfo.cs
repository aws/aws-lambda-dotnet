// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Xunit;

// Tests in this assembly share process-global state: SetCustomerLoggerLogAction points a static
// field on the loaded Amazon.Lambda.Core assembly at a per-invocation StringWriter, and the handler
// tests drive LambdaBootstrap whose background RunAsync work writes through that static action. When
// test collections run in parallel, one test's logging can be captured by another's writer (observed
// intermittently under load as "Can't find method name in console text" in PositiveHandlerTestsAsync).
// Some classes already share a [Collection] to serialize relative to each other, but that does not
// cover the rest of the assembly, so disable in-assembly parallelization outright.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
