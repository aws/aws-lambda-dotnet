// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.Models;

/// <summary>
/// Represents a base exception that is thrown by the test tool.
/// </summary>
/// <param name="message"></param>
/// <param name="innerException"></param>
public abstract class TestToolException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>
/// Thrown if the API Gateway Emulator mode was not provided,
/// </summary>
public class InvalidApiGatewayModeException(string message, Exception? innerException = null)
    : TestToolException(message, innerException);
