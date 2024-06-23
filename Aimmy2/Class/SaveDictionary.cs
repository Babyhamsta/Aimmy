using Newtonsoft.Json;
using System.IO;
using MessageBox = System.Windows.MessageBox;

namespace Class
{
    internal class SaveDictionary
    {
        public static void WriteJSON(Dictionary<string, dynamic> dictionary, string path = "bin\\configs\\Default.cfg", string SuggestedModel = "", string ExtraStrings = "")
        {
            try
            {
                var SavedJSONSettings = new Dictionary<string, dynamic>(dictionary);
                if (!string.IsNullOrEmpty(SuggestedModel) && SavedJSONSettings.ContainsKey("Suggested Model"))
                {
                    SavedJSONSettings["Suggested Model"] = SuggestedModel + ".onnx" + ExtraStrings;
                }

                string json = JsonConvert.SerializeObject(SavedJSONSettings, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error writing JSON, please note:\n{ex}");
            }
        }

        public static void LoadJSON(Dictionary<string, dynamic> dictionary, string path = "bin\\configs\\Default.cfg", bool strict = true)
        {
            try
            {
                if (!File.Exists(path))
                {
                    WriteJSON(dictionary, path);
                    return;
                }

                var configuration = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(File.ReadAllText(path));
                if (configuration == null) return;

                foreach (var (key, value) in configuration)
                {
                    if (dictionary.ContainsKey(key))
                    {
                        dictionary[key] = value;
                    }
                    else if (!strict)
                    {
                        dictionary.Add(key, value);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading JSON, please note:\n" + ex.ToString());
            }
        }
    }
}