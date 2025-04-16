// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace Amazon.Lambda.TestTool.UnitTests.SnapshotHelper;

/// <summary>
/// Provides functionality for saving and loading snapshot tests, primarily used for testing purposes.
/// </summary>
public class SnapshotTestHelper
{
    private readonly string _snapshotDirectory;
    private readonly JsonSerializerOptions _serializerOptions;

    /// <summary>
    /// Initializes a new instance of the SnapshotTestHelper class.
    /// </summary>
    /// <param name="serializerOptions">Custom JSON serializer options. If null, default options with indented writing will be used.</param>
    /// <param name="snapshotDirectory">The directory name where snapshots will be stored. Defaults to "Snapshots".</param>
    public SnapshotTestHelper(JsonSerializerOptions? serializerOptions = null, string snapshotDirectory = "Snapshots")
    {
        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../"));
        _snapshotDirectory = Path.Combine(projectDir, snapshotDirectory);
        _serializerOptions = serializerOptions ?? new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }

    /// <summary>
    /// Saves a snapshot of the specified value to a JSON file.
    /// </summary>
    /// <typeparam name="T">The type of the value to be saved.</typeparam>
    /// <param name="value">The value to save as a snapshot.</param>
    /// <param name="snapshotName">The name of the snapshot file (without extension).</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public async Task SaveSnapshot<T>(T value, string snapshotName)
    {
        Directory.CreateDirectory(_snapshotDirectory);
        var filePath = GetSnapshotPath(snapshotName);
        var serialized = JsonSerializer.Serialize(value, _serializerOptions);
        await File.WriteAllTextAsync(filePath, serialized);
    }

    /// <summary>
    /// Loads a snapshot from a JSON file and deserializes it to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the snapshot into.</typeparam>
    /// <param name="snapshotName">The name of the snapshot file (without extension).</param>
    /// <returns>The deserialized snapshot object.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified snapshot file does not exist.</exception>
    public async Task<T> LoadSnapshot<T>(string snapshotName)
    {
        var filePath = GetSnapshotPath(snapshotName);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Snapshot file not found: {filePath}");
        }

        var content = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<T>(content, _serializerOptions)!;
    }

    /// <summary>
    /// Gets the full file path for a snapshot file.
    /// </summary>
    /// <param name="snapshotName">The name of the snapshot file (without extension).</param>
    /// <returns>The full file path including the .json extension.</returns>
    private string GetSnapshotPath(string snapshotName) =>
        Path.Combine(_snapshotDirectory, $"{snapshotName}.json");
}
