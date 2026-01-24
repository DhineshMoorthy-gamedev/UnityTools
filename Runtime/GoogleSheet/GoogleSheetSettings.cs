using UnityEngine;

namespace UnityProductivityTools.GoogleSheet
{
    [CreateAssetMenu(fileName = "GoogleSheetSettings", menuName = "TaskTool/GoogleSheetSettings")]
    public class GoogleSheetSettings : ScriptableObject
    {
        public string SpreadsheetId = "";
        public string ApiKey = "";
        
        [Tooltip("Required for writing data. Paste the contents of your Service Account JSON file here.")]
        [TextArea(10, 20)]
        public string ServiceAccountJson;
        
        public string SheetName = "Sheet1";
        public string Range = "A1:Z100";
    }
}
