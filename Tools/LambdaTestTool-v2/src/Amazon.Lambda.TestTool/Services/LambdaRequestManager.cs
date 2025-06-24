// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Xml.Linq;
using Amazon.Lambda.TestTool.Models;
using Microsoft.Extensions.Options;

namespace Amazon.Lambda.TestTool.Services;

/// <summary>
/// This class manages the sample Lambda input requests. This includes the pre-canned requests and saved requests.
/// </summary>
public class LambdaRequestManager(IOptions<LambdaOptions> lambdaOptions) : ILambdaRequestManager
{
    private string? GetRequestDirectory(string functionName) => !string.IsNullOrEmpty(lambdaOptions.Value.SavedRequestsPath) ? Path.Combine(lambdaOptions.Value.SavedRequestsPath, Constants.TestToolLocalDirectory, Constants.SavedRequestDirectory, functionName) : null;

    /// <inheritdoc />
    public IDictionary<string, IList<LambdaRequest>> GetLambdaRequests(string functionName, bool includeSampleRequests = true, bool includeSavedRequests = true)
    {
        var requestDirectory = GetRequestDirectory(functionName);
        var hash = new Dictionary<string, IList<LambdaRequest>>();

        if (includeSampleRequests)
        {
            var content = GetEmbeddedResource("manifest.xml");
            var xmlDoc = XDocument.Parse(content);

            var requests = from item in xmlDoc.Descendants("request")
                select new LambdaRequest
                {
                    Group = item.Attribute("category")?.Value ?? string.Empty,
                    Name = item.Element("name")?.Value ?? string.Empty,
                    Filename = item.Element("filename")?.Value ?? string.Empty,
                };

            foreach (var request in requests)
            {
                IList<LambdaRequest>? r;
                if (!hash.TryGetValue(request.Group, out r))
                {
                    r = new List<LambdaRequest>();
                    hash[request.Group] = r;
                }

                r.Add(request);
            }
        }

        if(includeSavedRequests && Directory.Exists(requestDirectory))
        {
            var savedRequestFiles = Directory.GetFiles(requestDirectory, "*.json");
            if (savedRequestFiles.Length > 0)
            {
                var savedRequests = new List<LambdaRequest>();
                hash[Constants.SavedRequestGroup] = savedRequests;
                foreach (var file in savedRequestFiles)
                {
                    var r = new LambdaRequest
                    {
                        Filename = $"{Constants.SavedRequestDirectory}@{Path.GetFileName(file)}",
                        Group = Constants.SavedRequestGroup,
                        Name = Path.GetFileNameWithoutExtension(file)
                    };
                    savedRequests.Add(r);
                }
            }
        }

        foreach (var key in hash.Keys.ToList())
        {
            hash[key] = hash[key].OrderBy(x => x.Name).ToList();
        }

        return hash;
    }

    /// <inheritdoc />
    public string GetRequest(string functionName, string requestName)
    {
        var requestDirectory = GetRequestDirectory(functionName);
        if (requestDirectory is null)
            return string.Empty;

        if(requestName.StartsWith(Constants.SavedRequestDirectory + "@"))
        {
            requestName = requestName.Substring(requestName.IndexOf("@", StringComparison.Ordinal) + 1);
            var path = Path.Combine(requestDirectory, requestName);
            return File.ReadAllText(path);
        }
        return GetEmbeddedResource(requestName);
    }

    /// <inheritdoc />
    public void SaveRequest(string functionName, string requestName, string content)
    {
        var requestDirectory = GetRequestDirectory(functionName);
        if (requestDirectory is null)
            return;

        var filename = $"{requestName}.json";

        if (!Directory.Exists(requestDirectory))
            Directory.CreateDirectory(requestDirectory);

        File.WriteAllText(Path.Combine(requestDirectory, filename), content);
    }

    /// <inheritdoc />
    public void DeleteRequest(string functionName, string requestName)
    {
        var requestDirectory = GetRequestDirectory(functionName);
        if (requestDirectory is null)
            return;

        var filename = $"{requestName}.json";

        if (!Directory.Exists(requestDirectory))
            return;

        File.Delete(Path.Combine(requestDirectory, filename));
    }

    private string GetEmbeddedResource(string name)
    {
        using (var stream =
            typeof(LambdaRequestManager).Assembly.GetManifestResourceStream(
                "Amazon.Lambda.TestTool.Resources.SampleRequests." + name))
        using(var reader = new StreamReader(stream!))
        {
            return reader.ReadToEnd();
        }
    }
}
