using System.Text.RegularExpressions;

namespace HiddenDeps
{
    public static class HiddenDependencyPatterns
    {
        public static readonly Regex FindObjectOfType =
            new Regex(@"FindObjectOfType<(\w+)>", RegexOptions.Compiled);

        public static readonly Regex GameObjectFind =
            new Regex(@"GameObject\.Find\(""([^""]+)""\)", RegexOptions.Compiled);

        public static readonly Regex GetComponent =
            new Regex(@"GetComponent<(\w+)>", RegexOptions.Compiled);

        public static readonly Regex GetComponentInChildren =
            new Regex(@"GetComponentInChildren<(\w+)>", RegexOptions.Compiled);

        public static readonly Regex SendMessage =
            new Regex(@"SendMessage\(""([^""]+)""\)", RegexOptions.Compiled);
    }
}
