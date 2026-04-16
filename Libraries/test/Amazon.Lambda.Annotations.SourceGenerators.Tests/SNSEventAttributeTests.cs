// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations.SNS;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    public class SNSEventAttributeTests
    {
        // ===== Constructor and Default Values =====

        [Fact]
        public void Constructor_SetsTopicProperty()
        {
            var attr = new SNSEventAttribute("@MyTopic");

            Assert.Equal("@MyTopic", attr.Topic);
        }

        [Fact]
        public void DefaultValues_AreCorrect()
        {
            var attr = new SNSEventAttribute("@MyTopic");

            Assert.False(attr.IsEnabledSet);
            Assert.Null(attr.FilterPolicy);
            Assert.False(attr.IsFilterPolicySet);
            Assert.False(attr.IsResourceNameSet);
            Assert.Equal("MyTopic", attr.ResourceName);
        }

        // ===== ResourceName Tests =====

        [Fact]
        public void ResourceName_DerivedFromTopic_WithAtPrefix()
        {
            var attr = new SNSEventAttribute("@TestTopic");

            Assert.False(attr.IsResourceNameSet);
            Assert.Equal("TestTopic", attr.ResourceName);
        }

        [Fact]
        public void ResourceName_DerivedFromTopicArn()
        {
            var attr = new SNSEventAttribute("arn:aws:sns:us-east-1:123456789012:MyTopic");

            Assert.False(attr.IsResourceNameSet);
            Assert.Equal("MyTopic", attr.ResourceName);
        }

        [Fact]
        public void ResourceName_WhenExplicitlySet_IsTracked()
        {
            var attr = new SNSEventAttribute("@MyTopic");

            Assert.False(attr.IsResourceNameSet);

            attr.ResourceName = "CustomEventName";
            Assert.True(attr.IsResourceNameSet);
            Assert.Equal("CustomEventName", attr.ResourceName);
        }

        // ===== FilterPolicy Tests =====

        [Fact]
        public void FilterPolicy_DefaultIsNull()
        {
            var attr = new SNSEventAttribute("@MyTopic");

            Assert.Null(attr.FilterPolicy);
            Assert.False(attr.IsFilterPolicySet);
        }

        [Fact]
        public void FilterPolicy_WhenSet_IsTracked()
        {
            var attr = new SNSEventAttribute("@MyTopic")
            {
                FilterPolicy = "{\"store\": [\"example_corp\"]}"
            };

            Assert.True(attr.IsFilterPolicySet);
            Assert.Equal("{\"store\": [\"example_corp\"]}", attr.FilterPolicy);
        }

        // ===== Enabled Property Tests =====

        [Fact]
        public void Enabled_DefaultNotSet()
        {
            var attr = new SNSEventAttribute("@MyTopic");

            Assert.False(attr.IsEnabledSet);
        }

        [Fact]
        public void Enabled_WhenSetToFalse_IsTracked()
        {
            var attr = new SNSEventAttribute("@MyTopic")
            {
                Enabled = false
            };

            Assert.True(attr.IsEnabledSet);
            Assert.False(attr.Enabled);
        }

        [Fact]
        public void Enabled_WhenSetToTrue_IsTracked()
        {
            var attr = new SNSEventAttribute("@MyTopic")
            {
                Enabled = true
            };

            Assert.True(attr.IsEnabledSet);
            Assert.True(attr.Enabled);
        }

        // ===== Validation Tests =====

        [Fact]
        public void Validate_ValidResourceReference_ReturnsNoErrors()
        {
            var attr = new SNSEventAttribute("@MyTopic");

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_ValidTopicArn_ReturnsNoErrors()
        {
            var attr = new SNSEventAttribute("arn:aws:sns:us-east-1:123456789012:MyTopic");

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_InvalidTopicArn_ReturnsError()
        {
            var attr = new SNSEventAttribute("not-a-valid-arn");

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("Topic", errors[0]);
            Assert.Contains("ARN", errors[0]);
        }

        [Fact]
        public void Validate_InvalidResourceName_ReturnsError()
        {
            var attr = new SNSEventAttribute("@MyTopic")
            {
                ResourceName = "invalid-name!"
            };

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("ResourceName", errors[0]);
            Assert.Contains("alphanumeric", errors[0]);
        }

        [Fact]
        public void Validate_MultipleErrors_ReturnsAll()
        {
            var attr = new SNSEventAttribute("not-valid")
            {
                ResourceName = "invalid!"
            };

            var errors = attr.Validate();
            Assert.Equal(2, errors.Count);
        }

        [Fact]
        public void Validate_AllValidWithOptionals_ReturnsNoErrors()
        {
            var attr = new SNSEventAttribute("@MyTopic")
            {
                ResourceName = "MySNSEvent",
                FilterPolicy = "{\"store\": [\"example_corp\"]}",
                Enabled = true
            };

            var errors = attr.Validate();
            Assert.Empty(errors);
        }
    }
}
