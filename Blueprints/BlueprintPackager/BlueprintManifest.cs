using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace Packager
{
    public class BlueprintManifest
    {
        [JsonProperty(PropertyName = "display-name")]
        public string DisplayName { get; set; }
        [JsonProperty(PropertyName = "system-name")]
        public string SystemName { get; set; }
        public string Description { get; set; }

        [JsonProperty(PropertyName = "sort-order")]
        public int SortOrder { get; set; }
        public List<string> Tags { get; set; }

        [JsonProperty(PropertyName = "hidden-tags")]
        public List<string> HiddenTags { get; set; }
    }
}
