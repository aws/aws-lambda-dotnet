// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Internal;

internal static class SubTypeValidator
{
    /// <summary>
    /// Returns <paramref name="subType"/>, or <paramref name="defaultSubType"/> when null or empty.
    /// Throws if it violates the service constraint: 1 to 32 characters from <c>[a-zA-Z0-9-_]</c>.
    /// </summary>
    public static string? Resolve(string? subType, string? defaultSubType, string paramName)
    {
        if (string.IsNullOrEmpty(subType))
            return defaultSubType;

        if (subType.Length > 32 || !subType.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_'))
            throw new ArgumentException($"SubType '{subType}' is invalid. It must be 1 to 32 characters from [a-zA-Z0-9-_].", paramName);

        return subType;
    }
}
