// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    public class DurableExecutionModelTests
    {
        [Fact]
        public void TypeFullNames_ContainsDurableExecutionConstants()
        {
            Assert.Equal("Amazon.Lambda.Annotations.DurableExecutionAttribute", TypeFullNames.DurableExecutionAttribute);
            Assert.Equal("Amazon.Lambda.DurableExecution.DurableExecutionInvocationInput", TypeFullNames.DurableExecutionInvocationInput);
            Assert.Equal("Amazon.Lambda.DurableExecution.DurableExecutionInvocationOutput", TypeFullNames.DurableExecutionInvocationOutput);
            Assert.Equal("Amazon.Lambda.DurableExecution.DurableFunction", TypeFullNames.DurableFunction);
        }

        [Fact]
        public void TypeFullNames_Events_ContainsDurableExecutionAttribute()
        {
            Assert.Contains(TypeFullNames.DurableExecutionAttribute, TypeFullNames.Events);
        }

        [Fact]
        public void EventType_HasDurableExecutionValue()
        {
            Assert.NotEqual(EventType.API, EventType.DurableExecution);
            Assert.NotEqual(EventType.Schedule, EventType.DurableExecution);
        }

        // ===== Attribute unit tests (the attribute now lives in Amazon.Lambda.Annotations) =====

        [Fact]
        public void Attribute_Defaults_OnlyTimeoutSet()
        {
            // ExecutionTimeout is a required constructor argument; RetentionPeriodInDays stays optional.
            var attr = new DurableExecutionAttribute(300);

            Assert.False(attr.IsRetentionPeriodInDaysSet);
            Assert.Equal(300, attr.ExecutionTimeout);
            Assert.Empty(attr.Validate());
        }

        [Fact]
        public void Attribute_SettingRetention_TracksIsSet()
        {
            var attr = new DurableExecutionAttribute(300) { RetentionPeriodInDays = 7 };

            Assert.True(attr.IsRetentionPeriodInDaysSet);
            Assert.Equal(7, attr.RetentionPeriodInDays);
            Assert.Equal(300, attr.ExecutionTimeout);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(91)] // Above the service maximum of 90 days.
        public void Attribute_Validate_RejectsOutOfRangeRetentionPeriod(int value)
        {
            var attr = new DurableExecutionAttribute(300) { RetentionPeriodInDays = value };

            var errors = attr.Validate();

            Assert.Single(errors);
            Assert.Contains(nameof(DurableExecutionAttribute.RetentionPeriodInDays), errors[0]);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(31622401)] // Above the service maximum of 31622400 seconds.
        public void Attribute_Validate_RejectsOutOfRangeExecutionTimeout(int value)
        {
            var attr = new DurableExecutionAttribute(value);

            var errors = attr.Validate();

            Assert.Single(errors);
            Assert.Contains(nameof(DurableExecutionAttribute.ExecutionTimeout), errors[0]);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(90)]
        public void Attribute_Validate_AcceptsInRangeRetentionPeriod(int value)
        {
            var attr = new DurableExecutionAttribute(300) { RetentionPeriodInDays = value };

            Assert.Empty(attr.Validate());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(31622400)]
        public void Attribute_Validate_AcceptsInRangeExecutionTimeout(int value)
        {
            var attr = new DurableExecutionAttribute(value);

            Assert.Empty(attr.Validate());
        }

        [Fact]
        public void Attribute_Validate_ReportsBothInvalidValues()
        {
            var attr = new DurableExecutionAttribute(-5) { RetentionPeriodInDays = 0 };

            Assert.Equal(2, attr.Validate().Count);
        }

        // ===== Recognition + builder tests, driven through a real Roslyn compilation =====

        // The attribute type must fully bind for Roslyn to surface its NamedArguments, which requires
        // referencing the runtime assemblies (System.Runtime et al.), not just System.Private.CoreLib.
        // Reference every trusted-platform assembly the test host already loaded, plus the Annotations
        // assembly that defines DurableExecutionAttribute.
        private static IReadOnlyList<MetadataReference> BuildReferences()
        {
            var references = new List<MetadataReference>();

            var trustedAssemblies = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
                .Split(Path.PathSeparator);
            foreach (var path in trustedAssemblies)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    references.Add(MetadataReference.CreateFromFile(path));
                }
            }

            references.Add(MetadataReference.CreateFromFile(typeof(DurableExecutionAttribute).Assembly.Location));
            return references;
        }

        private static IMethodSymbol GetWorkflowMethod(string userSource)
        {
            var compilation = CSharpCompilation.Create(
                "DurableExecutionModelTests",
                new[]
                {
                    CSharpSyntaxTree.ParseText(userSource)
                },
                BuildReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var workflowType = compilation.GetTypeByMetadataName("MyApp.Workflows");
            Assert.NotNull(workflowType);
            return workflowType.GetMembers("Run").OfType<IMethodSymbol>().Single();
        }

        [Fact]
        public void EventTypeBuilder_RecognizesDurableExecutionAttribute()
        {
            var method = GetWorkflowMethod(@"
namespace MyApp
{
    public class Workflows
    {
        [Amazon.Lambda.Annotations.DurableExecution(300)]
        public void Run() { }
    }
}");

            // EventTypeBuilder.Build matches by ToDisplayString() and does not use the context argument.
            var events = EventTypeBuilder.Build(method, default);

            Assert.Contains(EventType.DurableExecution, events);
            Assert.Single(events);
        }

        [Fact]
        public void DurableExecutionAttributeBuilder_ReadsConstructorAndNamedArguments()
        {
            var method = GetWorkflowMethod(@"
namespace MyApp
{
    public class Workflows
    {
        [Amazon.Lambda.Annotations.DurableExecution(600, RetentionPeriodInDays = 14)]
        public void Run() { }
    }
}");

            var att = method.GetAttributes().Single();
            var data = DurableExecutionAttributeBuilder.Build(att);

            Assert.True(data.IsRetentionPeriodInDaysSet);
            Assert.Equal(14, data.RetentionPeriodInDays);
            Assert.Equal(600, data.ExecutionTimeout);
        }

        [Fact]
        public void DurableExecutionAttributeBuilder_OmitsUnsetRetention()
        {
            // Only the required ExecutionTimeout is supplied; RetentionPeriodInDays stays unset so it is
            // omitted from the generated template.
            var method = GetWorkflowMethod(@"
namespace MyApp
{
    public class Workflows
    {
        [Amazon.Lambda.Annotations.DurableExecution(300)]
        public void Run() { }
    }
}");

            var att = method.GetAttributes().Single();
            var data = DurableExecutionAttributeBuilder.Build(att);

            Assert.False(data.IsRetentionPeriodInDaysSet);
            Assert.Equal(300, data.ExecutionTimeout);
        }
    }
}
