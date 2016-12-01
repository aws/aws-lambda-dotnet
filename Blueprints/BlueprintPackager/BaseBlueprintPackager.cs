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

        public BaseBlueprintPackager(string blueprintRoot)
        {
            this._blueprintRoot = blueprintRoot;
        }

        protected IList<string> SearchForblueprintManifests()
        {
            return Directory.GetFiles(_blueprintRoot, "blueprint-manifest.json", SearchOption.AllDirectories).ToList();
        }        
    }
}