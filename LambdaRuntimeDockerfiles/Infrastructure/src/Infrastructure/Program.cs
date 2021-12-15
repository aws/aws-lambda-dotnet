/*
 * Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 *
 *  http://aws.amazon.com/apache2.0
 *
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using Amazon.CDK;
using Amazon.JSII.JsonModel.Spec;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Infrastructure
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var configuration = new Configuration();
            var app = new App();
            if (configuration.EcrRepositoryNames.Length != configuration.Frameworks.Length &&
                configuration.EcrRepositoryNames.Length != configuration.Channels.Length)
                throw new ArgumentException(
                    "There is a mismatch between the number of ECR Repositories, .NET Versions and .NET Channels.");
            for (var i = 0; i < configuration.Frameworks.Length; i++)
            {
                new PipelineStack(app, Configuration.ProjectName, configuration.EcrRepositoryNames[i], configuration.Frameworks[i], configuration.Channels[i], configuration.DockerBuildImages[configuration.Frameworks[i]], configuration, new StackProps 
                { 
                    TerminationProtection = true
                });
            }
            app.Synth();
        }
    }
}