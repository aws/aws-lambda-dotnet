// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Xml.Linq;

namespace Amazon.Lambda.TestTool.SampleRequests;

/// <summary>
/// This class manages the sample Lambda input requests. This includes the pre-canned requests and saved requests.
/// </summary>
public class SampleRequestManager(string preferenceDirectory)
{
    public const string SavedRequestGroup = "Saved Requests";
    public const string SavedRequestDirectory = "SavedRequests";

    public IDictionary<string, IList<LambdaRequest>> GetSampleRequests()
    {
        var content = GetEmbeddedResource("manifest.xml");
        XDocument xmlDoc = XDocument.Parse(content);

        var query = from item in xmlDoc.Descendants("request")
            select new
            {
                Name = item.Element("name")!.Value,
                Filename = item.Element("filename")!.Value
            };

        var requests = from item in xmlDoc.Descendants("request")
            select new LambdaRequest
            {
                Group = item.Attribute("category")?.Value ?? string.Empty,
                Name = item.Element("name")?.Value ?? string.Empty,
                Filename = item.Element("filename")?.Value ?? string.Empty,
            };

        var hash = new Dictionary<string, IList<LambdaRequest>>();

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

        var savedRequestDirectory = GetSavedRequestDirectory();
        if(Directory.Exists(savedRequestDirectory))
        {
            var savedRequestFiles = Directory.GetFiles(GetSavedRequestDirectory(), "*.json");
            if (savedRequestFiles.Length > 0)
            {
                var savedRequests = new List<LambdaRequest>();
                hash[SavedRequestGroup] = savedRequests;
                foreach (var file in savedRequestFiles)
                {
                    var r = new LambdaRequest
                    {
                        Filename = $"{SavedRequestDirectory}@{Path.GetFileName(file)}",
                        Group = SavedRequestGroup,
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

    public static bool TryDetermineSampleRequestName(string value, out string? sampleName)
    {
        sampleName = null;
        if (value == null)
            return false;

        if (!value.StartsWith(SavedRequestDirectory))
            return false;

        // The minus 6 is for the "@" and the trailing ".json"
        sampleName = value.Substring(SavedRequestDirectory.Length + 1, value.Length - SavedRequestDirectory.Length - 6);
        return true;
    }

    public string GetRequest(string name)
    {
        if(name.StartsWith(SavedRequestDirectory + "@"))
        {
            name = name.Substring(name.IndexOf("@") + 1);
            var path = Path.Combine(GetSavedRequestDirectory(), name);
            return File.ReadAllText(path);
        }
        return GetEmbeddedResource(name);
    }

    public string SaveRequest(string name, string content)
    {
        var filename = $"{name}.json";

        var savedRequestDirectory = GetSavedRequestDirectory();
        if (!Directory.Exists(savedRequestDirectory))
            Directory.CreateDirectory(savedRequestDirectory);

        File.WriteAllText(Path.Combine(savedRequestDirectory, filename), content);
        return $"{SavedRequestDirectory}@{filename}";
    }

    public string GetSaveRequestRelativePath(string name)
    {
        var relativePath = $"{SavedRequestDirectory}{Path.DirectorySeparatorChar}{name}";
        if (!name.EndsWith(".json"))
            relativePath += ".json";

        return relativePath;
    }

    private string GetEmbeddedResource(string name)
    {
        using (var stream =
            typeof(SampleRequestManager).Assembly.GetManifestResourceStream(
                "Amazon.Lambda.TestTool.Resources.SampleRequests." + name))
        using(var reader = new StreamReader(stream!))
        {
            return reader.ReadToEnd();
        }
    }

    public string GetSavedRequestDirectory()
    {
        var path = Path.Combine(preferenceDirectory, SavedRequestDirectory);
        return path;
    }
}
