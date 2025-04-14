// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace Amazon.Lambda.TestTool.IntegrationTests.Helpers.snapshot;
public class SnapshotTestHelper
{
    private readonly string _snapshotDirectory;
    private readonly JsonSerializerOptions _serializerOptions;

    public SnapshotTestHelper(JsonSerializerOptions? serializerOptions = null, string snapshotDirectory = "Snapshots")
    {
        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../"));
        _snapshotDirectory = Path.Combine(projectDir, snapshotDirectory);
        _serializerOptions = serializerOptions ?? new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }

    public async Task SaveSnapshot<T>(T value, string snapshotName)
    {
        Directory.CreateDirectory(_snapshotDirectory);
        var filePath = GetSnapshotPath(snapshotName);
        var serialized = JsonSerializer.Serialize(value, _serializerOptions);
        await File.WriteAllTextAsync(filePath, serialized);
    }

    public async Task<T> LoadSnapshot<T>(string snapshotName)
    {
        var filePath = GetSnapshotPath(snapshotName);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Snapshot file not found: {filePath}");
        }

        var content = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<T>(content, _serializerOptions);
    }

    private string GetSnapshotPath(string snapshotName) =>
        Path.Combine(_snapshotDirectory, $"{snapshotName}.json");
}
