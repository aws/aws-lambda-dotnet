// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations.SourceGenerator.Models;

namespace Amazon.Lambda.Annotations.SourceGenerator.Templates
{
    public partial class ALBInvoke
    {
        private readonly LambdaFunctionModel _model;

        public readonly string _parameterSignature;

        public ALBInvoke(LambdaFunctionModel model, string parameterSignature)
        {
            _model = model;
            _parameterSignature = parameterSignature;
        }
    }
}
