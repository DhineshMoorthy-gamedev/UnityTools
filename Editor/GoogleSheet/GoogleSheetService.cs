using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityProductivityTools.GoogleSheet.Editor
{
    public class GoogleSheetService
    {
        private readonly GoogleSheetSettings _settings;
        private string _accessToken;
        private DateTime _tokenExpiry;

        public GoogleSheetService(GoogleSheetSettings settings)
        {
            _settings = settings;
        }

        public async Task<List<List<string>>> FetchDataAsync()
        {
            if (string.IsNullOrEmpty(_settings.SpreadsheetId))
            {
                Debug.LogError("Spreadsheet ID is missing in Google Sheet Settings.");
                return null;
            }

            string url = $"https://sheets.googleapis.com/v4/spreadsheets/{_settings.SpreadsheetId}/values/{_settings.SheetName}!{_settings.Range}";
            
            if (!string.IsNullOrEmpty(_settings.ApiKey))
            {
                url += $"?key={_settings.ApiKey}";
            }

            Debug.Log($"[GoogleSheetService] Fetching data from URL: {url.Replace(_settings.ApiKey, "********")}");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                Debug.Log($"[GoogleSheetService] Response Code: {request.responseCode}");

                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (request.responseCode == 403)
                    {
                        Debug.LogError("[GoogleSheetService] Error 403 Forbidden: \n" +
                                       "This usually means the Spreadsheet is not shared correctly or the API is not enabled for your key.\n" +
                                       "Please ensure the spreadsheet is shared with 'Anyone with the link' and the Google Sheets API is enabled in your Cloud Console.");
                    }
                    else
                    {
                        Debug.LogError($"[GoogleSheetService] Error fetching: {request.error}\nDownload Handler Text: {request.downloadHandler.text}");
                    }
                    return null;
                }

                string jsonResponse = request.downloadHandler.text;
                // Debug.Log($"[GoogleSheetService] Raw JSON Response: {jsonResponse}");

                var response = JsonUtility.FromJson<GoogleSheetResponse>(jsonResponse);
                
                // JsonUtility doesn't support List<List<string>>, so we parse 'values' manually
                List<List<string>> parsedValues = ManualParseValues(jsonResponse);
                
                if (parsedValues == null || parsedValues.Count == 0)
                {
                    Debug.LogWarning("[GoogleSheetService] No rows found. Is the sheet empty or is the Sheet Name/Range incorrect?");
                    return new List<List<string>>();
                }

                Debug.Log($"[GoogleSheetService] Successfully parsed {parsedValues.Count} rows.");
                return parsedValues;
            }
        }

        private List<List<string>> ManualParseValues(string json)
        {
            List<List<string>> rows = new List<List<string>>();
            
            int valuesIndex = json.IndexOf("\"values\":");
            if (valuesIndex == -1) return rows;

            int start = json.IndexOf("[", valuesIndex);
            if (start == -1) return rows;

            // Find the outer array content by counting brackets to handle nesting
            int bracketCount = 0;
            int end = -1;
            for (int k = start; k < json.Length; k++)
            {
                if (json[k] == '[') bracketCount++;
                else if (json[k] == ']') bracketCount--;
                
                if (bracketCount == 0)
                {
                    end = k;
                    break;
                }
            }
            if (end == -1) return rows;

            string content = json.Substring(start + 1, end - start - 1);
            
            // Extract each inner array [...] correctly
            int i = 0;
            while (i < content.Length)
            {
                if (content[i] == '[')
                {
                    int rowStart = i;
                    int rowBracketCount = 0;
                    int rowEnd = -1;
                    for (int k = i; k < content.Length; k++)
                    {
                        if (content[k] == '[') rowBracketCount++;
                        else if (content[k] == ']') rowBracketCount--;
                        
                        if (rowBracketCount == 0)
                        {
                            rowEnd = k;
                            break;
                        }
                    }
                    if (rowEnd != -1)
                    {
                        string rowStr = content.Substring(rowStart + 1, rowEnd - rowStart - 1);
                        rows.Add(ParseRowCells(rowStr));
                        i = rowEnd + 1;
                    }
                    else break;
                }
                else i++;
            }

            return rows;
        }

        private List<string> ParseRowCells(string rowStr)
        {
            List<string> cells = new List<string>();
            bool inQuotes = false;
            string currentCell = "";
            
            for (int i = 0; i < rowStr.Length; i++)
            {
                char c = rowStr[i];
                if (c == '"')
                {
                    // Handle escaped quotes (if Google uses them)
                    if (i + 1 < rowStr.Length && rowStr[i+1] == '"')
                    {
                        currentCell += '"';
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    cells.Add(currentCell.Trim());
                    currentCell = "";
                }
                else
                {
                    currentCell += c;
                }
            }
            
            // Add the last cell
            cells.Add(currentCell.Trim());
            
            return cells;
        }

        public async Task<(List<List<string>> data, Dictionary<string, List<string>> validation)> FetchDataWithValidationAsync()
        {
            if (string.IsNullOrEmpty(_settings.SpreadsheetId))
            {
                Debug.LogError("Spreadsheet ID is missing in Google Sheet Settings.");
                return (null, null);
            }

            // Optional: Use Access Token if Service Account is provided
            bool useToken = !string.IsNullOrEmpty(_settings.ServiceAccountJson);
            if (useToken && (string.IsNullOrEmpty(_accessToken) || DateTime.Now >= _tokenExpiry))
            {
                await RefreshAccessTokenAsync();
            }

            string range = $"{_settings.SheetName}!{_settings.Range}";
            string url = $"https://sheets.googleapis.com/v4/spreadsheets/{_settings.SpreadsheetId}?ranges={UnityWebRequest.EscapeURL(range)}&includeGridData=true";
            
            if (!useToken && !string.IsNullOrEmpty(_settings.ApiKey))
            {
                url += $"&key={_settings.ApiKey}";
            }

            Debug.Log($"[GoogleSheetService] Fetching grid data... (Using {(useToken ? "Service Account" : "API Key")})");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                if (useToken && !string.IsNullOrEmpty(_accessToken))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {_accessToken}");
                }

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[GoogleSheetService] Error fetching: {request.error}\nCode: {request.responseCode}\nBody: {request.downloadHandler.text}");
                    return (null, null);
                }

                string jsonResponse = request.downloadHandler.text;
                return ParseFullGridData(jsonResponse);
            }
        }

        private (List<List<string>> data, Dictionary<string, List<string>> validation) ParseFullGridData(string json)
        {
            List<List<string>> data = new List<List<string>>();
            Dictionary<string, List<string>> validation = new Dictionary<string, List<string>>();

            try
            {
                // We use manual parsing because the JSON structure is very deep and nested
                // We are looking for "sheets" -> "data" -> "rowData" -> "values"
                int sheetsIdx = json.IndexOf("\"sheets\":");
                if (sheetsIdx == -1) return (data, validation);

                int dataIdx = json.IndexOf("\"data\":", sheetsIdx);
                if (dataIdx == -1) return (data, validation);

                int rowDataIdx = json.IndexOf("\"rowData\":", dataIdx);
                if (rowDataIdx == -1) return (data, validation);

                // Extract the rowData array
                int arrayStart = json.IndexOf("[", rowDataIdx);
                int bracketCount = 0;
                int arrayEnd = -1;
                for (int k = arrayStart; k < json.Length; k++)
                {
                    if (json[k] == '[') bracketCount++;
                    else if (json[k] == ']') bracketCount--;
                    if (bracketCount == 0) { arrayEnd = k; break; }
                }

                if (arrayEnd == -1) return (data, validation);
                string rowDataJson = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);

                // Parse each row
                int i = 0;
                int rowIndex = 0;
                while (i < rowDataJson.Length)
                {
                    if (rowDataJson[i] == '{')
                    {
                        int rowStart = i;
                        int rowBracketCount = 0;
                        int rowEnd = -1;
                        for (int k = i; k < rowDataJson.Length; k++)
                        {
                            if (rowDataJson[k] == '{') rowBracketCount++;
                            else if (rowDataJson[k] == '}') rowBracketCount--;
                            if (rowBracketCount == 0) { rowEnd = k; break; }
                        }

                        if (rowEnd != -1)
                        {
                            string rowContent = rowDataJson.Substring(rowStart, rowEnd - rowStart + 1);
                            var rowValues = ParseRowValues(rowContent, rowIndex, validation);
                            data.Add(rowValues);
                            rowIndex++;
                            i = rowEnd + 1;
                        }
                        else break;
                    }
                    else i++;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GoogleSheetService] Parsing Error: {e.Message}");
            }

            return (data, validation);
        }

        private List<string> ParseRowValues(string rowJson, int rowIndex, Dictionary<string, List<string>> validation)
        {
            List<string> values = new List<string>();
            int valsIdx = rowJson.IndexOf("\"values\":");
            if (valsIdx == -1) return values;

            int arrayStart = rowJson.IndexOf("[", valsIdx);
            int bracketCount = 0;
            int arrayEnd = -1;
            for (int k = arrayStart; k < rowJson.Length; k++)
            {
                if (rowJson[k] == '[') bracketCount++;
                else if (rowJson[k] == ']') bracketCount--;
                if (bracketCount == 0) { arrayEnd = k; break; }
            }

            if (arrayEnd == -1) return values;
            string valsContent = rowJson.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);

            int i = 0;
            int colIndex = 0;
            while (i < valsContent.Length)
            {
                if (valsContent[i] == '{')
                {
                    int cellStart = i;
                    int cellBracketCount = 0;
                    int cellEnd = -1;
                    for (int k = i; k < valsContent.Length; k++)
                    {
                        if (valsContent[k] == '{') cellBracketCount++;
                        else if (valsContent[k] == '}') cellBracketCount--;
                        if (cellBracketCount == 0) { cellEnd = k; break; }
                    }

                    if (cellEnd != -1)
                    {
                        string cellJson = valsContent.Substring(cellStart, cellEnd - cellStart + 1);
                        string value = ExtractCellValue(cellJson);
                        values.Add(value);

                        // Check for data validation
                        var options = ExtractValidationOptions(cellJson);
                        if (options != null && options.Count > 0)
                        {
                            validation[$"{rowIndex}:{colIndex}"] = options;
                        }

                        colIndex++;
                        i = cellEnd + 1;
                    }
                    else break;
                }
                else i++;
            }

            return values;
        }

        private string ExtractCellValue(string cellJson)
        {
            // Look for effectiveValue -> stringValue/numberValue/boolValue
            int effIdx = cellJson.IndexOf("\"formattedValue\":");
            if (effIdx != -1)
            {
                int quoteStart = cellJson.IndexOf("\"", effIdx + 17);
                int quoteEnd = cellJson.IndexOf("\"", quoteStart + 1);
                return cellJson.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
            }
            return "";
        }

        private List<string> ExtractValidationOptions(string cellJson)
        {
            // Look for dataValidation -> condition -> values
            int ruleIdx = cellJson.IndexOf("\"dataValidation\":");
            if (ruleIdx == -1) return null;

            int conditionIdx = cellJson.IndexOf("\"condition\":", ruleIdx);
            if (conditionIdx == -1) return null;

            int typeIdx = cellJson.IndexOf("\"type\":", conditionIdx);
            if (typeIdx != -1)
            {
                int q1 = cellJson.IndexOf("\"", typeIdx + 7);
                int q2 = cellJson.IndexOf("\"", q1 + 1);
                string type = cellJson.Substring(q1 + 1, q2 - q1 - 1);
                
                if (type == "ONE_OF_LIST")
                {
                    List<string> options = new List<string>();
                    int valuesIdx = cellJson.IndexOf("\"values\":", conditionIdx);
                    if (valuesIdx != -1)
                    {
                        // Parse the values array
                        int start = cellJson.IndexOf("[", valuesIdx);
                        int bCount = 0;
                        int end = -1;
                        for (int k = start; k < cellJson.Length; k++)
                        {
                            if (cellJson[k] == '[') bCount++;
                            else if (cellJson[k] == ']') bCount--;
                            if (bCount == 0) { end = k; break; }
                        }
                        
                        if (end != -1)
                        {
                            string valsArray = cellJson.Substring(start + 1, end - start - 1);
                            // Extract each "userEnteredValue"
                            int vIdx = 0;
                            while ((vIdx = valsArray.IndexOf("\"userEnteredValue\":", vIdx)) != -1)
                            {
                                int qStart = valsArray.IndexOf("\"", vIdx + 19);
                                int qEnd = valsArray.IndexOf("\"", qStart + 1);
                                if (qStart != -1 && qEnd != -1)
                                {
                                    options.Add(valsArray.Substring(qStart + 1, qEnd - qStart - 1));
                                }
                                vIdx = qEnd + 1;
                            }
                        }
                    }
                    return options;
                }
            }
            return null;
        }

        public async Task<bool> UpdateCellAsync(string range, string value)
        {
            if (string.IsNullOrEmpty(_settings.ServiceAccountJson))
            {
                Debug.LogError("[GoogleSheetService] Writing data requires a Service Account JSON. Please provide one in Settings.");
                return false;
            }

            // Ensure we have a valid access token
            if (string.IsNullOrEmpty(_accessToken) || DateTime.Now >= _tokenExpiry)
            {
                if (!await RefreshAccessTokenAsync()) return false;
            }

            string url = $"https://sheets.googleapis.com/v4/spreadsheets/{_settings.SpreadsheetId}/values/{_settings.SheetName}!{range}?valueInputOption=RAW";
            
            string json = $"{{\"values\": [[\"{value.Replace("\"", "\\\"")}\"]]}}";
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest request = new UnityWebRequest(url, "PUT"))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {_accessToken}");

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Error updating Google Sheet: {request.error}\n{request.downloadHandler.text}");
                    return false;
                }

                return true;
            }
        }

        private async Task<bool> RefreshAccessTokenAsync()
        {
            Debug.Log("[GoogleSheetService] Refreshing Access Token using Service Account...");
            
            ServiceAccountData sa = JsonUtility.FromJson<ServiceAccountData>(_settings.ServiceAccountJson);
            if (sa == null || string.IsNullOrEmpty(sa.private_key))
            {
                Debug.LogError("[GoogleSheetService] Failed to parse Service Account JSON.");
                return false;
            }

            // Create JWT Header
            string header = Base64UrlEncode("{\"alg\":\"RS256\",\"typ\":\"JWT\"}");
            
            // Create JWT Payload
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string payload = Base64UrlEncode($"{{\"iss\":\"{sa.client_email}\",\"scope\":\"https://www.googleapis.com/auth/spreadsheets\",\"aud\":\"https://oauth2.googleapis.com/token\",\"exp\":{now + 3600},\"iat\":{now}}}");
            
            string signatureInput = $"{header}.{payload}";
            string signature = SignWithRSA(signatureInput, sa.private_key);

            if (string.IsNullOrEmpty(signature)) return false;

            string jwt = $"{signatureInput}.{signature}";

            // Exchange JWT for token
            WWWForm form = new WWWForm();
            form.AddField("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer");
            form.AddField("assertion", jwt);

            using (UnityWebRequest request = UnityWebRequest.Post("https://oauth2.googleapis.com/token", form))
            {
                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[GoogleSheetService] Failed to exchange JWT for token: {request.downloadHandler.text}");
                    return false;
                }

                TokenResponse res = JsonUtility.FromJson<TokenResponse>(request.downloadHandler.text);
                _accessToken = res.access_token;
                _tokenExpiry = DateTime.Now.AddSeconds(res.expires_in - 60);
                
                Debug.Log("[GoogleSheetService] Successfully obtained access token.");
                return true;
            }
        }

        private string SignWithRSA(string input, string privateKeyPem)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(input);
            
            try 
            {
                byte[] pkeyRaw = Convert.FromBase64String(privateKeyPem
                    .Replace("-----BEGIN PRIVATE KEY-----", "")
                    .Replace("-----END PRIVATE KEY-----", "")
                    .Replace("\n", "").Replace("\r", "").Trim());

                // Use manual parser for Mono compatibility
                var rsaParams = DecodePkcs8PrivateKey(pkeyRaw);
                
                using (var rsa = new System.Security.Cryptography.RSACryptoServiceProvider())
                {
                    rsa.ImportParameters(rsaParams);
                    byte[] signature = rsa.SignData(data, System.Security.Cryptography.CryptoConfig.MapNameToOID("SHA256"));
                    return Base64UrlEncode(signature);
                }
            }
            catch (Exception e) 
            {
                Debug.LogError($"[GoogleSheetService] RSA Sign Error: {e.Message}\n{e.StackTrace}");
                return "";
            }
        }

        // --- MANUAL ASN.1 PKCS#8 PARSER FOR MONO COMPATIBILITY ---
        private System.Security.Cryptography.RSAParameters DecodePkcs8PrivateKey(byte[] pkcs8)
        {
            using (var ms = new System.IO.MemoryStream(pkcs8))
            using (var reader = new System.IO.BinaryReader(ms))
            {
                // Sequence
                if (reader.ReadByte() != 0x30) throw new Exception("Expected Sequence");
                ReadLength(reader);
                
                // Version
                ReadInteger(reader); // Skip Version
                
                // Algorithm Identifier
                if (reader.ReadByte() != 0x30) throw new Exception("Expected Algorithm Sequence");
                int algLen = ReadLength(reader);
                reader.ReadBytes(algLen); // Skip Algorithm ID
                
                // Private Key (Octet String)
                if (reader.ReadByte() != 0x04) throw new Exception("Expected Octet String");
                ReadLength(reader);
                
                // --- PKCS#1 RSAPrivateKey ---
                if (reader.ReadByte() != 0x30) throw new Exception("Expected PKCS#1 Sequence");
                ReadLength(reader);
                
                ReadInteger(reader); // Version
                
                var rsaParams = new System.Security.Cryptography.RSAParameters();
                rsaParams.Modulus = ReadInteger(reader);
                rsaParams.Exponent = ReadInteger(reader);
                rsaParams.D = ReadInteger(reader);
                rsaParams.P = ReadInteger(reader);
                rsaParams.Q = ReadInteger(reader);
                rsaParams.DP = ReadInteger(reader);
                rsaParams.DQ = ReadInteger(reader);
                rsaParams.InverseQ = ReadInteger(reader);
                
                return rsaParams;
            }
        }

        private byte[] ReadInteger(System.IO.BinaryReader reader)
        {
            if (reader.ReadByte() != 0x02) throw new Exception("Expected Integer");
            int len = ReadLength(reader);
            byte[] bytes = reader.ReadBytes(len);
            
            // Remove leading zero byte if it exists (ASN.1 padding)
            if (bytes[0] == 0x00 && bytes.Length > 1)
            {
                byte[] temp = new byte[bytes.Length - 1];
                Array.Copy(bytes, 1, temp, 0, temp.Length);
                return temp;
            }
            return bytes;
        }

        private int ReadLength(System.IO.BinaryReader reader)
        {
            int length = reader.ReadByte();
            if (length > 0x80)
            {
                int count = length & 0x7f;
                length = 0;
                for (int i = 0; i < count; i++)
                    length = (length << 8) | reader.ReadByte();
            }
            return length;
        }

        private string Base64UrlEncode(string input) => Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(input));
        private string Base64UrlEncode(byte[] input) => Convert.ToBase64String(input).Split('=')[0].Replace('+', '-').Replace('/', '_');

        [Serializable] private class ServiceAccountData { public string client_email; public string private_key; }
        [Serializable] private class TokenResponse { public string access_token; public int expires_in; }

        [Serializable]
        private class GoogleSheetResponse
        {
            public string range;
            public string majorDimension;
            public List<List<string>> values;
        }
    }
}
