---
name: new-event-source
description: Add a new AWS event source attribute (e.g., Kinesis, Kafka, MQ) to the Lambda .NET Annotations framework, including the attribute class, source generator integration, CloudFormation writer, unit tests, writer tests, source generator tests, and integration tests
---

# Adding a New Event Source to Lambda Annotations

This skill guides you through adding a complete new event source attribute to the AWS Lambda .NET Annotations framework. Use this when a user asks to add support for a new AWS event source like Kinesis, Kafka, MQ, etc.

## Prerequisites

Before starting, gather from the user:
1. **Service name** (e.g., "Kinesis", "Kafka", "MQ")
2. **Primary resource identifier** (e.g., stream ARN, topic ARN, broker ARN)
3. **CloudFormation event type string** (e.g., "Kinesis", "MSK", "MQ")
4. **Event class name** from the corresponding `Amazon.Lambda.*Events` NuGet package (e.g., `KinesisEvent`)
5. **Optional properties** the attribute should support (e.g., BatchSize, StartingPosition, Filters)
6. **Whether `@` references use `Fn::GetAtt` or `Ref`** — event source mappings use `Fn::GetAtt`, subscriptions use `Ref`

## Reference Examples

Read these files to understand existing patterns before creating new ones:
- **SNS (simplest, subscription-based)**: `Libraries/src/Amazon.Lambda.Annotations/SNS/SNSEventAttribute.cs`
- **SQS (event source mapping with batching)**: `Libraries/src/Amazon.Lambda.Annotations/SQS/SQSEventAttribute.cs`
- **DynamoDB (stream-based)**: `Libraries/src/Amazon.Lambda.Annotations/DynamoDB/DynamoDBEventAttribute.cs`
- **S3 (notification-based)**: `Libraries/src/Amazon.Lambda.Annotations/S3/S3EventAttribute.cs`

Also see `Libraries/src/Amazon.Lambda.Annotations/ADDING_NEW_EVENT_SOURCE.md` for the full detailed developer guide.

## Steps

### Step 1: Create the Event Attribute Class

**Create**: `Libraries/src/Amazon.Lambda.Annotations/{ServiceName}/{ServiceName}EventAttribute.cs`

Key patterns:
- Add copyright header: `// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.` + `// SPDX-License-Identifier: Apache-2.0`
- Inherit from `Attribute` with `[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]`
- Constructor takes the primary resource identifier as a required `string` parameter
- All optional properties use nullable backing fields with `Is<PropertyName>Set` internal properties
- Include auto-derived `ResourceName` property (strips `@` prefix or extracts name from ARN)
- Include `internal List<string> Validate()` method with all validation rules
- Use `Regex("^[a-zA-Z0-9]+$")` for ResourceName validation

### Step 2: Register Type Full Names

**Modify**: `Libraries/src/Amazon.Lambda.Annotations.SourceGenerator/TypeFullNames.cs`

Add constants:
```csharp
public const string {ServiceName}EventAttribute = "Amazon.Lambda.Annotations.{ServiceName}.{ServiceName}EventAttribute";
public const string {ServiceName}Event = "Amazon.Lambda.{ServiceName}Events.{ServiceName}Event";
```

Also add to `EventType` enum if needed in `Libraries/src/Amazon.Lambda.Annotations.SourceGenerator/Models/EventType.cs`.

### Step 3: Create the Attribute Builder

**Create**: `Libraries/src/Amazon.Lambda.Annotations.SourceGenerator/Models/Attributes/{ServiceName}EventAttributeBuilder.cs`

Extracts attribute data from Roslyn `AttributeData`. Use consistent `else if` chaining. Reference: `SNSEventAttributeBuilder.cs`.

### Step 4: Register in AttributeModelBuilder

**Modify**: `Libraries/src/Amazon.Lambda.Annotations.SourceGenerator/Models/Attributes/AttributeModelBuilder.cs`

Add `else if` block for the new attribute type after the existing event attribute blocks.

### Step 5: Register in EventTypeBuilder

**Modify**: `Libraries/src/Amazon.Lambda.Annotations.SourceGenerator/Models/EventTypeBuilder.cs`

Add `else if` block mapping the attribute to the `EventType` enum value.

### Step 6: Add DiagnosticDescriptor

**Modify**: `Libraries/src/Amazon.Lambda.Annotations.SourceGenerator/Diagnostics/DiagnosticDescriptors.cs`

Add descriptor with the next available `AWSLambda0XXX` ID for invalid attribute validation errors.

**Modify**: `Libraries/src/Amazon.Lambda.Annotations.SourceGenerator/Diagnostics/AnalyzerReleases.Unshipped.md` — add the new diagnostic ID.

### Step 7: Add Validation in LambdaFunctionValidator

**Modify**: `Libraries/src/Amazon.Lambda.Annotations.SourceGenerator/Validation/LambdaFunctionValidator.cs`

1. Add `Validate{ServiceName}Events()` call in `ValidateFunction` method
2. Create private `Validate{ServiceName}Events()` method that validates:
   - Attribute properties via `Validate()` method
   - Method parameters (first must be event type, optional second is `ILambdaContext`)
   - Return type (usually `void` or `Task`)

### Step 8: Add Dependency Check

**Modify**: `Libraries/src/Amazon.Lambda.Annotations.SourceGenerator/Validation/LambdaFunctionValidator.cs`

In `ValidateDependencies`, add check for `Amazon.Lambda.{ServiceName}Events` NuGet package.

### Step 9: Check SyntaxReceiver

**Check**: `Libraries/src/Amazon.Lambda.Annotations.SourceGenerator/SyntaxReceiver.cs`

Add the new attribute name if the SyntaxReceiver filters by attribute name strings.

### Step 10: Add CloudFormation Writer Logic

**Modify**: `Libraries/src/Amazon.Lambda.Annotations.SourceGenerator/Writers/CloudFormationWriter.cs`

1. Add `case AttributeModel<{ServiceName}EventAttribute>` in the event processing switch
2. Create `Process{ServiceName}Attribute()` method that writes CF template properties
   - Event source mappings (SQS, DynamoDB, Kinesis): use `Fn::GetAtt` for `@` references
   - Subscription events (SNS): use `Ref` for `@` references
   - Track synced properties in metadata

### Step 11: Create Attribute Unit Tests

**Create**: `Libraries/test/Amazon.Lambda.Annotations.SourceGenerators.Tests/{ServiceName}EventAttributeTests.cs`

Cover: constructor, defaults, property tracking, ResourceName derivation, all validation paths. Reference: `SQSEventAttributeTests.cs`, `DynamoDBEventAttributeTests.cs`, `SNSEventAttributeTests.cs`.

### Step 12: Create CloudFormation Writer Tests

**Create**: `Libraries/test/Amazon.Lambda.Annotations.SourceGenerators.Tests/WriterTests/{ServiceName}EventsTests.cs`

This is a `partial class CloudFormationWriterTests`. Include tests for:
1. `Verify{ServiceName}EventAttributes_AreCorrectlyApplied` — Theory with JSON/YAML and property combinations
2. `Verify{ServiceName}EventProperties_AreSyncedCorrectly` — Synced properties update when attributes change
3. `SwitchBetweenArnAndRef_For{Resource}` — ARN to `@` reference switching
4. `Verify{Resource}CanBeSet_FromCloudFormationParameter` — CF Parameters handling
5. `VerifyManuallySet{ServiceName}EventProperties_ArePreserved` — Hand-edited template preservation

Reference: `SQSEventsTests.cs`, `DynamoDBEventsTests.cs`, `SNSEventsTests.cs`.

### Step 13: Create Valid Event Examples + Source Generator Test

**Create**: `Libraries/test/TestServerlessApp/{ServiceName}EventExamples/Valid{ServiceName}Events.cs.txt`
**Create**: `Libraries/test/Amazon.Lambda.Annotations.SourceGenerators.Tests/Snapshots/{ServiceName}/` (generated handler snapshots)
**Create**: `Libraries/test/Amazon.Lambda.Annotations.SourceGenerators.Tests/Snapshots/ServerlessTemplates/{serviceName}Events.template`
**Modify**: `Libraries/test/Amazon.Lambda.Annotations.SourceGenerators.Tests/SourceGeneratorTests.cs` — add `VerifyValid{ServiceName}Events()` test

### Step 14: Create Invalid Event Examples + Source Generator Test

**Create**: `Libraries/test/TestServerlessApp/{ServiceName}EventExamples/Invalid{ServiceName}Events.cs.error`

Cover: invalid property values, invalid params, invalid return type, multiple events, invalid ARN, invalid resource name.

**Modify**: `Libraries/test/Amazon.Lambda.Annotations.SourceGenerators.Tests/SourceGeneratorTests.cs` — add `VerifyInvalid{ServiceName}Events_ThrowsCompilationErrors()` test with diagnostic assertions including line spans.

### Step 15: Create Generated Code Snapshots

**Create**: `Libraries/test/Amazon.Lambda.Annotations.SourceGenerators.Tests/Snapshots/{ServiceName}/`

Tip: Run the source generator once to get actual output, then use as snapshot.

### Step 16: Create Integration Test

**Create**: `Libraries/test/TestServerlessApp.IntegrationTests/{ServiceName}EventSourceMapping.cs`
**Modify**: `Libraries/test/TestServerlessApp.IntegrationTests/IntegrationTestContextFixture.cs` — resource lookup
**Modify**: `Libraries/test/TestServerlessApp.IntegrationTests/DeploymentScript.ps1` — if needed

### Step 17: Update AnalyzerReleases.Unshipped.md

**Modify**: `Libraries/src/Amazon.Lambda.Annotations.SourceGenerator/Diagnostics/AnalyzerReleases.Unshipped.md`

## File Map Summary

| Action | File Path |
|--------|-----------|
| Create | `src/Amazon.Lambda.Annotations/{ServiceName}/{ServiceName}EventAttribute.cs` |
| Modify | `src/Amazon.Lambda.Annotations.SourceGenerator/TypeFullNames.cs` |
| Create | `src/Amazon.Lambda.Annotations.SourceGenerator/Models/Attributes/{ServiceName}EventAttributeBuilder.cs` |
| Modify | `src/Amazon.Lambda.Annotations.SourceGenerator/Models/Attributes/AttributeModelBuilder.cs` |
| Modify | `src/Amazon.Lambda.Annotations.SourceGenerator/Models/EventTypeBuilder.cs` |
| Modify | `src/Amazon.Lambda.Annotations.SourceGenerator/Diagnostics/DiagnosticDescriptors.cs` |
| Modify | `src/Amazon.Lambda.Annotations.SourceGenerator/Validation/LambdaFunctionValidator.cs` |
| Modify | `src/Amazon.Lambda.Annotations.SourceGenerator/SyntaxReceiver.cs` |
| Modify | `src/Amazon.Lambda.Annotations.SourceGenerator/Writers/CloudFormationWriter.cs` |
| Modify | `src/Amazon.Lambda.Annotations.SourceGenerator/Diagnostics/AnalyzerReleases.Unshipped.md` |
| Create | `test/Amazon.Lambda.Annotations.SourceGenerators.Tests/{ServiceName}EventAttributeTests.cs` |
| Create | `test/Amazon.Lambda.Annotations.SourceGenerators.Tests/WriterTests/{ServiceName}EventsTests.cs` |
| Create | `test/TestServerlessApp/{ServiceName}EventExamples/Valid{ServiceName}Events.cs.txt` |
| Create | `test/TestServerlessApp/{ServiceName}EventExamples/Invalid{ServiceName}Events.cs.error` |
| Create | `test/Amazon.Lambda.Annotations.SourceGenerators.Tests/Snapshots/{ServiceName}/` |
| Create | `test/Amazon.Lambda.Annotations.SourceGenerators.Tests/Snapshots/ServerlessTemplates/{serviceName}Events.template` |
| Modify | `test/Amazon.Lambda.Annotations.SourceGenerators.Tests/SourceGeneratorTests.cs` |
| Create | `test/TestServerlessApp.IntegrationTests/{ServiceName}EventSourceMapping.cs` |
| Modify | `test/TestServerlessApp.IntegrationTests/IntegrationTestContextFixture.cs` |

## Important Conventions

- **Copyright header** on every new `.cs` file
- **Consistent `else if` chaining** in attribute builders (never `if` then `if` for the same loop)
- **Both JSON and YAML** template formats must be tested in writer tests
- **Invalid event test spans** must reference exact line numbers in the `.cs.error` file
- **`.cs.txt` extension** for valid test files (prevents deployment)
- **`.cs.error` extension** for invalid test files (prevents compilation)
