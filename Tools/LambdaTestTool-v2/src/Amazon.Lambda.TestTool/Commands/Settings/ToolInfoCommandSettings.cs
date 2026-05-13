// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Models;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Amazon.Lambda.TestTool.Commands.Settings;

/// <summary>
/// Represents the settings for configuring the <see cref="ToolInfoCommand"/>.
/// </summary>
public sealed class ToolInfoCommandSettings : CommandSettings
{
    public enum InfoFormat 
    {
        Text,
        Json
    }

    /// <summary>
    /// The format the info is displayed as.
    /// The available formats are: Text, Json.
    /// </summary>
    [CommandOption("--format <FORMAT>")]
    [Description(
        "The format the info is displayed as. " +
        "The available formats are: Text, Json.")]
    public InfoFormat? Format { get; set; }
}
