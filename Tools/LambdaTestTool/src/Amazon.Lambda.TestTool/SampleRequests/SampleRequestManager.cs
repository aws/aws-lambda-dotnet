using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Amazon.Lambda.TestTool.SampleRequests
{
    /// <summary>
    /// This class manages the sample Lambda input requests. This includes the precanned requests and saved requests.
    /// </summary>
    public class SampleRequestManager
    {
        public const string SAVED_REQUEST_GROUP = "Saved Requests";
        public const string SAVED_REQUEST_DIRECTORY = "SavedRequests";
        private string _preferenceDirectory;
        
        public SampleRequestManager(string preferenceDirectory)
        {
            this._preferenceDirectory = preferenceDirectory;
        }
        
        public IDictionary<string, IList<LambdaRequest>> GetSampleRequests()
        {
            var content = GetEmbeddedResource("manifest.xml");
            XDocument xmlDoc = XDocument.Parse(content);

            var query = from item in xmlDoc.Descendants("request")
                select new
                {
                    Name = item.Element("name").Value,
                    Filename = item.Element("filename").Value
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
                IList<LambdaRequest> r;
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
                    hash[SAVED_REQUEST_GROUP] = savedRequests;
                    foreach (var file in savedRequestFiles)
                    {
                        var r = new LambdaRequest
                        {
                            Filename = $"{SAVED_REQUEST_DIRECTORY}@{Path.GetFileName(file)}",
                            Group = SAVED_REQUEST_GROUP,
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

        public static bool TryDetermineSampleRequestName(string value, out string sampleName)
        {
            sampleName = null;
            if (value == null)
                return false;

            if (!value.StartsWith(SAVED_REQUEST_DIRECTORY))
                return false;

            // The minus 6 is for the "@" and the trailing ".json"
            sampleName = value.Substring(SAVED_REQUEST_DIRECTORY.Length + 1, value.Length - SAVED_REQUEST_DIRECTORY.Length - 6);
            return true;
        }

        public string GetRequest(string name)
        {
            if(name.StartsWith(SAVED_REQUEST_DIRECTORY + "@"))
            {
                name = name.Substring(name.IndexOf("@") + 1);
                var path = Path.Combine(this.GetSavedRequestDirectory(), name);
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
            return $"{SAVED_REQUEST_DIRECTORY}@{filename}";
        }

        public string GetSaveRequestRelativePath(string name)
        {
            var relativePath = $"{SAVED_REQUEST_DIRECTORY}{Path.DirectorySeparatorChar}{name}";
            if (!name.EndsWith(".json"))
                relativePath += ".json";

            return relativePath;
        }

        private string GetEmbeddedResource(string name)
        {
            using (var stream =
                typeof(Amazon.Lambda.TestTool.Services.IAWSService).Assembly.GetManifestResourceStream(
                    "Amazon.Lambda.TestTool.Resources.SampleRequests." + name))
            using(var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        public string GetSavedRequestDirectory()
        {
            var path = Path.Combine(this._preferenceDirectory, SAVED_REQUEST_DIRECTORY);
            return path;
        }
    }
}