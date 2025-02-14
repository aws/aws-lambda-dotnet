// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;
using Amazon.Lambda.TestTool.Utilities;
using Spectre.Console.Cli;

namespace Amazon.Lambda.TestTool.Commands;

/// <summary>
/// Command to display tool information like the version of the tool.
/// </summary>
/// <param name="toolInteractiveService"></param>
public class ToolInfoCommand(IToolInteractiveService toolInteractiveService)
    : Command<ToolInfoCommandSettings>
{
    /// <summary>
    /// The method responsible for executing the <see cref="RunCommand"/>.
    /// </summary>
    public override int Execute(CommandContext context, ToolInfoCommandSettings settings)
    {
        var info = CollectInformation();

        var formattedInfo = settings.Format switch
        {
            ToolInfoCommandSettings.InfoFormat.Text => GenerateToolInfoText(info),
            ToolInfoCommandSettings.InfoFormat.Json => GenerateToolInfoJson(info),
            _ => GenerateToolInfoText(info)
        };

        toolInteractiveService.WriteLine(formattedInfo);
        return CommandReturnCodes.Success;
    }

    private string GenerateToolInfoText(IDictionary<string, string> info)
    {
        var stringBuilder = new StringBuilder();
        foreach(var kvp in info)
        {
            stringBuilder.AppendLine($"{kvp.Key}: {kvp.Value}");
        }

        return stringBuilder.ToString();
    }

    private string GenerateToolInfoJson(IDictionary<string, string> info)
    {
        var stream = new MemoryStream();
        Utf8JsonWriter utf8JsonWriter = new Utf8JsonWriter(stream, options: new JsonWriterOptions()
        {
            Indented = false
        });

        utf8JsonWriter.WriteStartObject();

        foreach (var kvp in info)
        {
            utf8JsonWriter.WriteString(kvp.Key, kvp.Value);
        }

        utf8JsonWriter.WriteEndObject();
        utf8JsonWriter.Flush();

        stream.Position = 0;
        return new StreamReader(stream).ReadToEnd();
    }

    private Dictionary<string, string> CollectInformation()
    {
        var info = new Dictionary<string, string>();
        info["Version"] = Utils.DetermineToolVersion();
        info["InstallPath"] = GetInstallPath();
        return info;
    }

    private string GetInstallPath() => Directory.GetParent(typeof(Utils).Assembly.Location)!.FullName;
}
