// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.S3;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using System;

namespace TestServerlessApp.S3EventExamples
{
    public class S3EventProcessing
    {
        [LambdaFunction(ResourceName = "S3EventHandler", Policies = "AWSLambdaBasicExecutionRole,AmazonS3ReadOnlyAccess", PackageType = LambdaPackageType.Image)]
        [S3Event("@TestS3Bucket", Events = "s3:ObjectCreated:*", FilterSuffix = ".json")]
        public void ProcessS3Event(S3Event evnt)
        {
            Console.WriteLine($"Received S3 event with {evnt.Records.Count} records");
        }
    }
}
