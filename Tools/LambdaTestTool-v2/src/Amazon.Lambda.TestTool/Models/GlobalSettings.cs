// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.Models;

/// <summary>
/// Container for the test tool global settings.
/// </summary>
public class GlobalSettings
{
    /// <summary>
    /// Indicates whether to display the sample requests.
    /// </summary>
    public bool ShowSampleRequests { get; set; } = true;

    /// <summary>
    /// Indicates whether to display the saved requests.
    /// </summary>
    public bool ShowSavedRequests { get; set; } = true;

    /// <summary>
    /// Indicates whether to display the requests list.
    /// </summary>
    public bool ShowRequestsList { get; set; } = true;

    /// <summary>
    /// Method to create a deep copy for immutable updates.
    /// </summary>
    /// <returns>Global settings</returns>
    public GlobalSettings DeepCopy()
    {
        return new GlobalSettings
        {
            ShowSampleRequests = ShowSampleRequests,
            ShowSavedRequests = ShowSavedRequests,
            ShowRequestsList = ShowRequestsList
        };
    }
}
