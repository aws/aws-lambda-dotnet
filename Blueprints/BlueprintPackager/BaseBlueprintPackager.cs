using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Compression;

using System.Text;
using System.Xml;

using Newtonsoft.Json;

namespace Packager
{
    public abstract class BaseBlueprintPackager
    {
        protected string _blueprintRoot;
        protected IList<string> _excludeBlueprints;

        /// <summary>
        /// Construct a new BaseBlueprintPackager.
        /// </summary>
        /// <param name="blueprintRoot">root to look for blueprint-manifest.json in</param>
        /// <param name="excludeBlueprints">names of blueprints to exclude from this BaseBlueprintPackager</param>
        public BaseBlueprintPackager(string blueprintRoot, IList<string> excludeBlueprints = null)
        {
            if (excludeBlueprints == null)
            {
                excludeBlueprints = new List<string>();
            }

            this._blueprintRoot = blueprintRoot;
            this._excludeBlueprints = excludeBlueprints;
        }

        protected IList<string> SearchForblueprintManifests()
        {
            var temp = Directory.GetFiles(_blueprintRoot, "blueprint-manifest.json", SearchOption.AllDirectories);
            var result = new List<string>();
            foreach(string possible in temp)
            {
                var include = true;
                foreach (var excludeBlueprint in _excludeBlueprints)
                {
                    if (Path.GetFileName(Path.GetDirectoryName(possible)) == excludeBlueprint)
                    {
                        include = false;
                        break;
                    }
                }
                if (include)
                {
                    result.Add(possible);
                }
            }

            return result;
        }
    }
}