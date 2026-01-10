using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HiddenDeps
{
    public static class HiddenDependencyScanner
    {
        public static List<HiddenDependencyRecord> ScanProject()
        {
            var results = new List<HiddenDependencyRecord>();
            string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript");

            foreach (string guid in scriptGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScanScript(path, results);
            }

            return results;
        }

        private static void ScanScript(string path, List<HiddenDependencyRecord> results)
        {
            string[] lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                MatchAndAdd(line, path, i + 1, results);
            }
        }

        private static void MatchAndAdd(
            string line,
            string path,
            int lineNumber,
            List<HiddenDependencyRecord> results)
        {
            TryMatch(HiddenDependencyPatterns.FindObjectOfType, DependencyType.FindObjectOfType);
            TryMatch(HiddenDependencyPatterns.GameObjectFind, DependencyType.GameObjectFind);
            TryMatch(HiddenDependencyPatterns.GetComponent, DependencyType.GetComponent);
            TryMatch(HiddenDependencyPatterns.GetComponentInChildren, DependencyType.GetComponentInChildren);
            TryMatch(HiddenDependencyPatterns.SendMessage, DependencyType.SendMessage);

            void TryMatch(System.Text.RegularExpressions.Regex regex, DependencyType type)
            {
                var match = regex.Match(line);
                if (!match.Success) return;

                results.Add(new HiddenDependencyRecord
                {
                    scriptPath = path,
                    scriptName = Path.GetFileName(path),
                    lineNumber = lineNumber,
                    dependencyType = type,
                    target = match.Groups[1].Value,
                    rawLine = line.Trim()
                });
            }
        }


        public static List<HiddenDependencyRecord> ScanSingleScript(string scriptPath)
        {
            var results = new List<HiddenDependencyRecord>();

            if (string.IsNullOrEmpty(scriptPath))
                return results;

            ScanScript(scriptPath, results);
            return results;
        }

    }
}