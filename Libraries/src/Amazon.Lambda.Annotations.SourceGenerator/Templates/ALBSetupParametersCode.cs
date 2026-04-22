// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations.SourceGenerator.Models;

namespace Amazon.Lambda.Annotations.SourceGenerator.Templates
{
    public partial class ALBSetupParameters
    {
        private readonly LambdaFunctionModel _model;

        public string ParameterSignature { get; set; }

        public ALBSetupParameters(LambdaFunctionModel model)
        {
            _model = model;
        }
    }
}
