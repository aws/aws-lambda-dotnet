// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace TestServerlessApp.ALB.IntegrationTests
{
    [CollectionDefinition("ALB Integration Tests")]
    public class ALBIntegrationTestContextFixtureCollection : ICollectionFixture<ALBIntegrationTestContextFixture>
    {
    }
}
