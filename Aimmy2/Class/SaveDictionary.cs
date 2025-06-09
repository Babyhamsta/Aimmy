using Newtonsoft.Json;
using System.IO;
using MessageBox = System.Windows.MessageBox;

namespace Class
{
    internal class SaveDictionary
    {
        // Ensure all required directories exist at startup
        public static void EnsureDirectoriesExist()
        {
            var requiredDirectories = new[]
            {
                "bin",
                "bin\\configs",
                "bin\\anti_recoil_configs",
                "bin\\labels",
                "bin\\models"
            };

            foreach (var dir in requiredDirectories)
            {
                if (!Directory.Exists(dir))
                {
                    try
                    {
                        Directory.CreateDirectory(dir);
                    }
                    catch (Exception ex)
                    {
                        // Log the error but don't crash - the app might still work without some directories
                        System.Diagnostics.Debug.WriteLine($"Failed to create directory {dir}: {ex.Message}");
                    }
                }
            }
        }
        public static void WriteJSON(Dictionary<string, dynamic> dictionary, string path = "bin\\configs\\Default.cfg", string SuggestedModel = "", string ExtraStrings = "")
        {
            try
            {
                // Ensure the directory exists
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

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
                // Only show error if it's not a directory creation issue
                MessageBox.Show($"Error writing JSON, please note:\n{ex}");
            }
        }

        public static void LoadJSON(Dictionary<string, dynamic> dictionary, string path = "bin\\configs\\Default.cfg", bool strict = true)
        {
            try
            {
                // Ensure the directory exists before checking for the file
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

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
                // If there's an error loading, try to recreate the file with defaults
                try
                {
                    WriteJSON(dictionary, path);
                }
                catch
                {
                    // Only show error if we can't even create a default file
                    MessageBox.Show("Error loading JSON, please note:\n" + ex.ToString());
                }
            }
        }
    }
}