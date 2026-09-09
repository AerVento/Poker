using System.IO;
using System.Linq;
using System.Xml.Linq;
using Game.Achievements;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;

namespace Game.EditorTools
{
    // Reflection-only achievement classes and their constructors must survive IL2CPP stripping.
    public sealed class AchievementLinker : IUnityLinkerProcessor
    {
        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            var path = Path.GetFullPath("Library/Achievements/link.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            new XDocument(new XElement("linker", new XElement("assembly",
                new XAttribute("fullname", typeof(Achievement).Assembly.GetName().Name),
                AchievementCatalog.DiscoverTypes().Select(t => new XElement("type",
                    new XAttribute("fullname", t.FullName), new XAttribute("preserve", "all")))))).Save(path);
            return path;
        }
    }
}
