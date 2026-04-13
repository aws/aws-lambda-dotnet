// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations.Schedule;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    public class ScheduleEventAttributeTests
    {
        // ===== Constructor and Default Values =====

        [Fact]
        public void Constructor_SetsScheduleProperty()
        {
            var attr = new ScheduleEventAttribute("rate(5 minutes)");

            Assert.Equal("rate(5 minutes)", attr.Schedule);
        }

        [Fact]
        public void DefaultValues_AreCorrect()
        {
            var attr = new ScheduleEventAttribute("rate(5 minutes)");

            Assert.False(attr.IsEnabledSet);
            Assert.Null(attr.Description);
            Assert.False(attr.IsDescriptionSet);
            Assert.Null(attr.Input);
            Assert.False(attr.IsInputSet);
            Assert.False(attr.IsResourceNameSet);
            Assert.Equal("rate5minutes", attr.ResourceName);
        }

        // ===== ResourceName Tests =====

        [Fact]
        public void ResourceName_DerivedFromScheduleExpression()
        {
            var attr = new ScheduleEventAttribute("rate(1 hour)");

            Assert.False(attr.IsResourceNameSet);
            Assert.Equal("rate1hour", attr.ResourceName);
        }

        [Fact]
        public void ResourceName_DerivedFromCronExpression()
        {
            var attr = new ScheduleEventAttribute("cron(0 12 * * ? *)");

            Assert.False(attr.IsResourceNameSet);
            Assert.Equal("cron012", attr.ResourceName);
        }

        [Fact]
        public void ResourceName_WhenExplicitlySet_IsTracked()
        {
            var attr = new ScheduleEventAttribute("rate(5 minutes)");

            Assert.False(attr.IsResourceNameSet);

            attr.ResourceName = "CustomSchedule";
            Assert.True(attr.IsResourceNameSet);
            Assert.Equal("CustomSchedule", attr.ResourceName);
        }

        // ===== Description Tests =====

        [Fact]
        public void Description_DefaultIsNull()
        {
            var attr = new ScheduleEventAttribute("rate(5 minutes)");

            Assert.Null(attr.Description);
            Assert.False(attr.IsDescriptionSet);
        }

        [Fact]
        public void Description_WhenSet_IsTracked()
        {
            var attr = new ScheduleEventAttribute("rate(5 minutes)")
            {
                Description = "Run every 5 minutes"
            };

            Assert.True(attr.IsDescriptionSet);
            Assert.Equal("Run every 5 minutes", attr.Description);
        }

        // ===== Input Tests =====

        [Fact]
        public void Input_DefaultIsNull()
        {
            var attr = new ScheduleEventAttribute("rate(5 minutes)");

            Assert.Null(attr.Input);
            Assert.False(attr.IsInputSet);
        }

        [Fact]
        public void Input_WhenSet_IsTracked()
        {
            var attr = new ScheduleEventAttribute("rate(5 minutes)")
            {
                Input = "{\"key\": \"value\"}"
            };

            Assert.True(attr.IsInputSet);
            Assert.Equal("{\"key\": \"value\"}", attr.Input);
        }

        // ===== Enabled Property Tests =====

        [Fact]
        public void Enabled_DefaultNotSet()
        {
            var attr = new ScheduleEventAttribute("rate(5 minutes)");

            Assert.False(attr.IsEnabledSet);
        }

        [Fact]
        public void Enabled_WhenSetToFalse_IsTracked()
        {
            var attr = new ScheduleEventAttribute("rate(5 minutes)")
            {
                Enabled = false
            };

            Assert.True(attr.IsEnabledSet);
            Assert.False(attr.Enabled);
        }

        [Fact]
        public void Enabled_WhenSetToTrue_IsTracked()
        {
            var attr = new ScheduleEventAttribute("rate(5 minutes)")
            {
                Enabled = true
            };

            Assert.True(attr.IsEnabledSet);
            Assert.True(attr.Enabled);
        }

        // ===== Validation Tests =====

        [Fact]
        public void Validate_ValidRateExpression_ReturnsNoErrors()
        {
            var attr = new ScheduleEventAttribute("rate(5 minutes)");

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_ValidCronExpression_ReturnsNoErrors()
        {
            var attr = new ScheduleEventAttribute("cron(0 12 * * ? *)");

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_NullSchedule_ReturnsError()
        {
            var attr = new ScheduleEventAttribute(null);

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("Schedule", errors[0]);
        }

        [Fact]
        public void Validate_EmptySchedule_ReturnsError()
        {
            var attr = new ScheduleEventAttribute("");

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("Schedule", errors[0]);
        }

        [Fact]
        public void Validate_InvalidScheduleExpression_ReturnsError()
        {
            var attr = new ScheduleEventAttribute("every 5 minutes");

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("Schedule", errors[0]);
            Assert.Contains("rate(", errors[0]);
        }

        [Fact]
        public void Validate_InvalidResourceName_ReturnsError()
        {
            var attr = new ScheduleEventAttribute("rate(5 minutes)")
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
            var attr = new ScheduleEventAttribute("invalid")
            {
                ResourceName = "invalid!"
            };

            var errors = attr.Validate();
            Assert.Equal(2, errors.Count);
        }

        [Fact]
        public void Enabled_DefaultValueIsTrue_WhenNotExplicitlySet()
        {
            var attr = new ScheduleEventAttribute("rate(5 minutes)");

            Assert.False(attr.IsEnabledSet);
            Assert.True(attr.Enabled);
        }

        [Fact]
        public void ResourceName_NullSchedule_DoesNotThrow()
        {
            var attr = new ScheduleEventAttribute(null);

            // Should not throw NullReferenceException
            var resourceName = attr.ResourceName;
            Assert.Equal("ScheduleEvent", resourceName);
        }

        [Fact]
        public void Validate_AllValidWithOptionals_ReturnsNoErrors()
        {
            var attr = new ScheduleEventAttribute("rate(1 hour)")
            {
                ResourceName = "MySchedule",
                Description = "Hourly job",
                Input = "{\"action\": \"cleanup\"}",
                Enabled = true
            };

            var errors = attr.Validate();
            Assert.Empty(errors);
        }
    }
}
