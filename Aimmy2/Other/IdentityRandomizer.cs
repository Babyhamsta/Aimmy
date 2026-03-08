using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using Aimmy2.Class;
using Other;

namespace Aimmy2.Other
{
    /// <summary>
    /// 身份随机化管理器 - 随机化应用名、进程名、窗口名等标识
    /// </summary>
    public class IdentityRandomizer : IDisposable
    {
        private static IdentityRandomizer? _instance;
        private static readonly object LockObject = new();
        
        private readonly Random _random;
        private bool _disposed;
        private string? _originalProcessName;
        private string? _originalWindowTitle;
        private System.Threading.Timer? _periodicUpdateTimer;

        // 随机名称库
        private static readonly string[] RandomPrefixes = new[]
        {
            "steam", "origin", "uplay", "epic", "gog", "discord", "slack", "teams", "zoom", 
            "chrome", "firefox", "edge", "opera", "spotify", "netflix", "notepad", "calculator",
            "photos", "media", "player", "editor", "viewer", "manager", "service", "agent",
            "helper", "updater", "installer", "launcher", "runtime", "framework", "driver"
        };

        private static readonly string[] RandomSuffixes = new[]
        {
            "helper", "service", "agent", "host", "ui", "client", "server", "backend", "frontend",
            "process", "task", "job", "worker", "thread", "module", "component", "plugin", "addon",
            "extension", "app", "exe", "bin", "tool", "utility", "manager", "controller", "handler"
        };

        private static readonly string[] RandomDescriptions = new[]
        {
            "Unified library of best and free in-game modifications.",
            "Entertainment platform for gamers.",
            "Game distribution and management application.",
            "Social gaming community and store.",
            "Digital distribution platform for PC gaming.",
            "Gaming social network and launcher.",
            "Game store and community platform.",
            "Game library and launcher application.",
            "Video game digital distribution service.",
            "Gaming and entertainment platform."
        };

        // 当前随机化的名称
        private string _currentRandomProcessName = string.Empty;
        private string _currentRandomWindowTitle = string.Empty;
        private string _currentRandomDescription = string.Empty;

        public static IdentityRandomizer Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (LockObject)
                    {
                        if (_instance == null)
                        {
                            _instance = new IdentityRandomizer();
                        }
                    }
                }
                return _instance;
            }
        }

        private IdentityRandomizer()
        {
            _random = new Random();
            _originalProcessName = Process.GetCurrentProcess().ProcessName;
            _originalWindowTitle = string.Empty;
        }

        /// <summary>
        /// 初始化身份随机化系统
        /// </summary>
        public void Initialize()
        {
            try
            {
                LogManager.Log(LogManager.LogLevel.Info, "[IdentityRandomizer] 开始初始化身份随机化系统...");

                // 保存原始窗口标题
                var mainWindow = Application.Current?.MainWindow;
                if (mainWindow != null)
                {
                    _originalWindowTitle = mainWindow.Title;
                }

                // 从配置加载设置
                LoadConfiguration();

                // 如果启用，则执行随机化
                if (GetToggleState("Randomize Identity", false))
                {
                    ExecuteRandomization();
                    StartPeriodicUpdate();
                }

                LogManager.Log(LogManager.LogLevel.Info, "[IdentityRandomizer] 身份随机化系统初始化完成");
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[IdentityRandomizer] 初始化失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 从配置加载参数
        /// </summary>
        private void LoadConfiguration()
        {
            // 配置已在 Dictionary 中管理
        }

        private bool GetToggleState(string key, bool defaultValue)
        {
            return Dictionary.toggleState.ContainsKey(key) ? Dictionary.toggleState[key] : defaultValue;
        }

        /// <summary>
        /// 执行完整的身份随机化
        /// </summary>
        public void ExecuteRandomization()
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                LogManager.Log(LogManager.LogLevel.Info, "[IdentityRandomizer] 开始执行身份随机化...");

                // 1. 使用高级欺骗技术修改PEB
                if (GetToggleState("Advanced Process Name Spoofing", true))
                {
                    PerformAdvancedSpoofing();
                }

                // 2. 随机化窗口标题
                if (GetToggleState("Randomize Window Title", true))
                {
                    RandomizeWindowTitle();
                }

                // 3. 随机化应用描述/横幅文本
                if (GetToggleState("Randomize Description", true))
                {
                    RandomizeDescription();
                }

                // 4. 随机化窗口类名（可选，更高级）
                if (GetToggleState("Randomize Window Class", false))
                {
                    RandomizeWindowClass();
                }

                stopwatch.Stop();
                LogManager.Log(LogManager.LogLevel.Info, $"[IdentityRandomizer] 身份随机化完成，耗时：{stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[IdentityRandomizer] 随机化失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 执行高级欺骗 - 修改PEB绕过任务管理器检测
        /// </summary>
        private void PerformAdvancedSpoofing()
        {
            try
            {
                string randomProcessName = GenerateRandomProcessName();
                string randomCommandLine = $"\"C:\\Program Files\\RandomApp\\{randomProcessName}\" --normal-start";
                
                LogManager.Log(LogManager.LogLevel.Info, $"[IdentityRandomizer] 执行高级PEB欺骗: {randomProcessName}");
                
                bool success = AdvancedIdentitySpoofer.PerformFullSpoof(randomProcessName, randomCommandLine);
                
                if (success)
                {
                    LogManager.Log(LogManager.LogLevel.Info, "[IdentityRandomizer] 高级PEB欺骗成功完成", true, 3000);
                }
                else
                {
                    LogManager.Log(LogManager.LogLevel.Warning, "[IdentityRandomizer] 高级PEB欺骗部分失败，回退到基础模式");
                }
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[IdentityRandomizer] 高级欺骗失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 随机化窗口标题
        /// </summary>
        private void RandomizeWindowTitle()
        {
            try
            {
                _currentRandomWindowTitle = GenerateRandomWindowTitle();
                var mainWindow = Application.Current?.MainWindow;
                
                if (mainWindow != null && Application.Current != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        mainWindow.Title = _currentRandomWindowTitle;
                    });
                }

                LogManager.Log(LogManager.LogLevel.Info, $"[IdentityRandomizer] 窗口标题已随机化为：{_currentRandomWindowTitle}");
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[IdentityRandomizer] 窗口标题随机化失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 随机化描述文本
        /// </summary>
        private void RandomizeDescription()
        {
            try
            {
                _currentRandomDescription = GenerateRandomDescription();
                LogManager.Log(LogManager.LogLevel.Info, $"[IdentityRandomizer] 描述已随机化为：{_currentRandomDescription}");
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[IdentityRandomizer] 描述随机化失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 随机化窗口类名
        /// </summary>
        private void RandomizeWindowClass()
        {
            try
            {
                // 注意：WPF 窗口类名在运行时修改比较复杂
                // 这里我们记录日志，但实际修改需要更高级的技术
                LogManager.Log(LogManager.LogLevel.Info, "[IdentityRandomizer] 窗口类名随机化（高级功能，需要额外实现）");
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[IdentityRandomizer] 窗口类名随机化失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 生成随机窗口标题
        /// </summary>
        private string GenerateRandomWindowTitle()
        {
            var prefix = RandomPrefixes[_random.Next(RandomPrefixes.Length)];
            var suffix = RandomSuffixes[_random.Next(RandomSuffixes.Length)];
            var number = _random.Next(1000, 99999);
            
            // 随机选择格式
            var formats = new[]
            {
                $"{prefix}_{suffix}",
                $"{prefix}{suffix}",
                $"{prefix}-{suffix}",
                $"{prefix}.{suffix}",
                $"{prefix}{suffix}.exe",
                $"{prefix}_{suffix}_{number}",
                $"{prefix}{suffix}{number}",
                $"{prefix}-{number}",
                $"{prefix}{number}.exe"
            };

            return formats[_random.Next(formats.Length)];
        }

        /// <summary>
        /// 生成随机进程名
        /// </summary>
        public string GenerateRandomProcessName()
        {
            var prefix = RandomPrefixes[_random.Next(RandomPrefixes.Length)];
            var suffix = RandomSuffixes[_random.Next(RandomSuffixes.Length)];
            var number = _random.Next(1000, 99999);
            
            var formats = new[]
            {
                $"{prefix}{suffix}.exe",
                $"{prefix}_{suffix}.exe",
                $"{prefix}-{suffix}.exe",
                $"{prefix}{number}.exe",
                $"{prefix}_{suffix}_{number}.exe",
                $"{prefix}{suffix}{number}.exe"
            };

            return formats[_random.Next(formats.Length)];
        }

        /// <summary>
        /// 生成随机描述
        /// </summary>
        private string GenerateRandomDescription()
        {
            return RandomDescriptions[_random.Next(RandomDescriptions.Length)];
        }

        /// <summary>
        /// 启动周期性更新
        /// </summary>
        private void StartPeriodicUpdate()
        {
            try
            {
                var intervalMinutes = GetSliderValue("Identity Update Interval", 30);
                if (intervalMinutes > 0)
                {
                    int intervalMs = intervalMinutes * 60 * 1000;
                    _periodicUpdateTimer = new System.Threading.Timer(
                        OnPeriodicUpdate,
                        null,
                        intervalMs,
                        intervalMs
                    );
                    LogManager.Log(LogManager.LogLevel.Info, $"[IdentityRandomizer] 周期性更新已启动，间隔：{intervalMinutes}分钟");
                }
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[IdentityRandomizer] 周期性更新启动失败：{ex.Message}");
            }
        }

        private int GetSliderValue(string key, int defaultValue)
        {
            return Dictionary.sliderSettings.ContainsKey(key) ? Convert.ToInt32(Dictionary.sliderSettings[key]) : defaultValue;
        }

        /// <summary>
        /// 周期性更新回调
        /// </summary>
        private void OnPeriodicUpdate(object? state)
        {
            try
            {
                LogManager.Log(LogManager.LogLevel.Info, "[IdentityRandomizer] 执行周期性身份更新...");
                ExecuteRandomization();
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[IdentityRandomizer] 周期性更新失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 应用配置更改
        /// </summary>
        public void ApplyConfigurationChanges()
        {
            try
            {
                var enabled = GetToggleState("Randomize Identity", false);
                
                if (enabled)
                {
                    ExecuteRandomization();
                    StartPeriodicUpdate();
                    LogManager.Log(LogManager.LogLevel.Info, "[IdentityRandomizer] 身份随机化已启用", true, 3000);
                }
                else
                {
                    RestoreOriginalIdentity();
                    _periodicUpdateTimer?.Dispose();
                    _periodicUpdateTimer = null;
                    LogManager.Log(LogManager.LogLevel.Info, "[IdentityRandomizer] 身份随机化已禁用", true, 3000);
                }
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[IdentityRandomizer] 配置更改失败：{ex.Message}", true);
            }
        }

        /// <summary>
        /// 恢复原始身份
        /// </summary>
        private void RestoreOriginalIdentity()
        {
            try
            {
                var mainWindow = Application.Current?.MainWindow;
                if (mainWindow != null && !string.IsNullOrEmpty(_originalWindowTitle) && Application.Current != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        mainWindow.Title = _originalWindowTitle;
                    });
                }

                LogManager.Log(LogManager.LogLevel.Info, "[IdentityRandomizer] 已恢复原始身份");
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[IdentityRandomizer] 恢复原始身份失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 手动触发随机化
        /// </summary>
        public void TriggerRandomization()
        {
            ExecuteRandomization();
            LogManager.Log(LogManager.LogLevel.Info, "[IdentityRandomizer] 手动触发身份随机化完成", true, 3000);
        }

        /// <summary>
        /// 获取当前状态信息
        /// </summary>
        public string GetStatusInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine("身份随机化系统状态:");
            sb.AppendLine($"  已启用：{GetToggleState("Randomize Identity", false)}");
            sb.AppendLine($"  当前窗口标题：{_currentRandomWindowTitle}");
            sb.AppendLine($"  当前描述：{_currentRandomDescription}");
            return sb.ToString();
        }

        /// <summary>
        /// 获取随机窗口标题（用于 UI 显示）
        /// </summary>
        public string GetCurrentRandomWindowTitle()
        {
            return _currentRandomWindowTitle;
        }

        /// <summary>
        /// 获取随机描述（用于 UI 显示）
        /// </summary>
        public string GetCurrentRandomDescription()
        {
            return _currentRandomDescription;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                RestoreOriginalIdentity();
                _periodicUpdateTimer?.Dispose();
                _disposed = true;
            }
        }
    }
}
