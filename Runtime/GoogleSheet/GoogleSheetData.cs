using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityProductivityTools.GoogleSheet
{
    [Serializable]
    public class GoogleSheetRow
    {
        public string Id;
        public string Title;
        public string Description;
        public string Status;
        public string Priority;
        public string Assignee;
        public string Date;
        
        // Dynamic fields for columns we haven't predicted
        public List<string> AdditionalFields = new List<string>();

        public GoogleSheetRow()
        {
            Id = Guid.NewGuid().ToString();
        }
    }

    [Serializable]
    public class GoogleSheetData : ScriptableObject
    {
        public string SpreadsheetId;
        public string SheetName = "Sheet1";
        public List<GoogleSheetRow> Rows = new List<GoogleSheetRow>();
        public List<string> Headers = new List<string>();
    }
}
