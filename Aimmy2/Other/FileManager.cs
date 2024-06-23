using Aimmy2.AILogic;
using Aimmy2.Class;
using Aimmy2.Other;
using Class;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Visuality;
using static Aimmy2.Other.GithubManager;

namespace Other
{
    internal class FileManager
    {
        public FileSystemWatcher? ModelFileWatcher;
        public FileSystemWatcher? ConfigFileWatcher;

        private ListBox ModelListBox;
        private Label SelectedModelNotifier;

        private ListBox ConfigListBox;
        private Label SelectedConfigNotifier;

        public bool InQuittingState = false;

        //private DetectedPlayerWindow DetectedPlayerOverlay;
        //private FOV FOVWindow;

        public static AIManager? AIManager;

        public FileManager(ListBox modelListBox, Label selectedModelNotifier, ListBox configListBox, Label selectedConfigNotifier)
        {
            ModelListBox = modelListBox;
            SelectedModelNotifier = selectedModelNotifier;

            ConfigListBox = configListBox;
            SelectedConfigNotifier = selectedConfigNotifier;

            ModelListBox.SelectionChanged += ModelListBox_SelectionChanged;
            ConfigListBox.SelectionChanged += ConfigListBox_SelectionChanged;

            CheckForRequiredFolders();
            InitializeFileWatchers();
            LoadModelsIntoListBox(null, null);
            LoadConfigsIntoListBox(null, null);
        }

        private void CheckForRequiredFolders()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] dirs = ["bin\\models", "bin\\images", "bin\\labels", "bin\\configs", "bin\\anti_recoil_configs"];

            try
            {
                foreach (string dir in dirs)
                {
                    string fullPath = Path.Combine(baseDir, dir);
                    if (!Directory.Exists(fullPath))
                    {
                        Directory.CreateDirectory(fullPath);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating a required directory: {ex}");
                Application.Current.Shutdown();
            }
        }

        public static bool CurrentlyLoadingModel = false;

        private async void ModelListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModelListBox.SelectedItem == null) return;

            string selectedModel = ModelListBox.SelectedItem.ToString()!;

            string modelPath = Path.Combine("bin/models", selectedModel);

            // Check if the model is already selected or currently loading
            if (Dictionary.lastLoadedModel == selectedModel || CurrentlyLoadingModel) return;

            CurrentlyLoadingModel = true;
            Dictionary.lastLoadedModel = selectedModel;

            // Store original values and disable them temporarily
            var toggleKeys = new[] { "Aim Assist", "Constant AI Tracking", "Auto Trigger", "Show Detected Player", "Show AI Confidence", "Show Tracers" };
            var originalToggleStates = toggleKeys.ToDictionary(key => key, key => Dictionary.toggleState[key]);
            foreach (var key in toggleKeys)
            {
                Dictionary.toggleState[key] = false;
            }

            // Let the AI finish up
            await Task.Delay(150);

            // Reload AIManager with new model
            AIManager?.Dispose();
            AIManager = new AIManager(modelPath);

            // Restore original values
            foreach (var keyValuePair in originalToggleStates)
            {
                Dictionary.toggleState[keyValuePair.Key] = keyValuePair.Value;
            }

            string content = "Loaded Model: " + selectedModel;
            SelectedModelNotifier.Content = content;
            new NoticeBar(content, 2000).Show();
        }

        private void ConfigListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConfigListBox.SelectedItem == null) return;
            string selectedConfig = ConfigListBox.SelectedItem.ToString()!;

            string configPath = Path.Combine("bin/configs", selectedConfig);

            SaveDictionary.LoadJSON(Dictionary.sliderSettings, configPath);
            PropertyChanger.PostNewConfig(configPath, true);

            SelectedConfigNotifier.Content = "Loaded Config: " + selectedConfig;
        }

        public void InitializeFileWatchers()
        {
            ModelFileWatcher = new FileSystemWatcher();
            ConfigFileWatcher = new FileSystemWatcher();

            InitializeWatcher(ref ModelFileWatcher, "bin/models", "*.onnx");
            InitializeWatcher(ref ConfigFileWatcher, "bin/configs", "*.cfg");
        }

        private void InitializeWatcher(ref FileSystemWatcher watcher, string path, string filter)
        {
            watcher.Path = path;
            watcher.Filter = filter;
            watcher.EnableRaisingEvents = true;

            if (filter == "*.onnx")
            {
                watcher.Changed += LoadModelsIntoListBox;
                watcher.Created += LoadModelsIntoListBox;
                watcher.Deleted += LoadModelsIntoListBox;
                watcher.Renamed += LoadModelsIntoListBox;
            }
            else if (filter == "*.cfg")
            {
                watcher.Changed += LoadConfigsIntoListBox;
                watcher.Created += LoadConfigsIntoListBox;
                watcher.Deleted += LoadConfigsIntoListBox;
                watcher.Renamed += LoadConfigsIntoListBox;
            }
        }

        public void LoadModelsIntoListBox(object? sender, FileSystemEventArgs? e)
        {
            if (!InQuittingState)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    string[] onnxFiles = Directory.GetFiles("bin/models", "*.onnx");
                    ModelListBox.Items.Clear();

                    foreach (string filePath in onnxFiles)
                    {
                        ModelListBox.Items.Add(Path.GetFileName(filePath));
                    }

                    if (ModelListBox.Items.Count > 0)
                    {
                        string? lastLoadedModel = Dictionary.lastLoadedModel;
                        if (lastLoadedModel != "N/A" && !ModelListBox.Items.Contains(lastLoadedModel)) { ModelListBox.SelectedItem = lastLoadedModel; }
                        SelectedModelNotifier.Content = $"Loaded Model: {lastLoadedModel}";
                    }
                });
            }
        }

        public void LoadConfigsIntoListBox(object? sender, FileSystemEventArgs? e)
        {
            if (!InQuittingState)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    string[] configFiles = Directory.GetFiles("bin/configs", "*.cfg");
                    ConfigListBox.Items.Clear();

                    foreach (string filePath in configFiles)
                    {
                        ConfigListBox.Items.Add(Path.GetFileName(filePath));
                    }

                    if (ConfigListBox.Items.Count > 0)
                    {
                        string? lastLoadedConfig = Dictionary.lastLoadedConfig;
                        if (lastLoadedConfig != "N/A" && !ConfigListBox.Items.Contains(lastLoadedConfig)) { ConfigListBox.SelectedItem = lastLoadedConfig; }

                        SelectedConfigNotifier.Content = "Loaded Config: " + lastLoadedConfig;
                    }
                });
            }
        }

        public static async Task<HashSet<string>> RetrieveAndAddFiles(string repoLink, string localPath, HashSet<string> allFiles)
        {
            try
            {
                GithubManager githubManager = new();

                var files = await githubManager.FetchGithubFilesAsync(repoLink);

                foreach (var file in files)
                {
                    if (file == null) continue;

                    if (!allFiles.Contains(file) && !File.Exists(Path.Combine(localPath, file)))
                    {
                        allFiles.Add(file);
                    }
                }

                githubManager.Dispose();

                return allFiles;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
        }
    }
}