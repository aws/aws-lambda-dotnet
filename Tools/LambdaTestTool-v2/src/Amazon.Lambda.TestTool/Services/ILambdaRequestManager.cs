// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Models;

namespace Amazon.Lambda.TestTool.Services;

/// <summary>
/// This class manages the sample Lambda input requests. This includes the pre-canned requests and saved requests.
/// </summary>
public interface ILambdaRequestManager
{
    /// <summary>
    /// Retrieves Lambda input requests which include pre-canned requests and saved requests.
    /// </summary>
    /// <param name="functionName">Lambda function name</param>
    /// <param name="includeSampleRequests">true, to include sample requests. false, to exclude sample requests.</param>
    /// <param name="includeSavedRequests">true, to include saved requests. false, to exclude saved requests.</param>
    /// <returns>Lambda input requests</returns>
    IDictionary<string, IList<LambdaRequest>> GetLambdaRequests(string functionName, bool includeSampleRequests = true, bool includeSavedRequests = true);

    /// <summary>
    /// Retrieves a specific Lambda input request.
    /// </summary>
    /// <param name="functionName">Lambda function name</param>
    /// <param name="requestName">Request name</param>
    /// <returns>Lambda input request</returns>
    string GetRequest(string functionName, string requestName);

    /// <summary>
    /// Saves the user's input request to a physical location on disk.
    /// </summary>
    /// <param name="functionName">Lambda function name</param>
    /// <param name="requestName">Request name</param>
    /// <param name="content">The content of the Lambda input request</param>
    void SaveRequest(string functionName, string requestName, string content);

    /// <summary>
    /// Deletes the user's input request from disk.
    /// </summary>
    /// <param name="functionName">Lambda function name</param>
    /// <param name="requestName">Request name</param>
    void DeleteRequest(string functionName, string requestName);
}
