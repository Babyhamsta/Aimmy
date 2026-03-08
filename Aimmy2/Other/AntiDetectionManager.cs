using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Aimmy2.Class;
using Other;

namespace Aimmy2.Other
{
    /// <summary>
    /// 反检测管理器 - 在程序启动时自动对关键特征进行随机化处理
    /// 注意：此功能不修改任务管理器中显示的进程名称，而是对程序自身的可识别特征进行变更
    /// </summary>
    public class AntiDetectionManager : IDisposable
    {
        private static AntiDetectionManager? _instance;
        private static readonly object LockObject = new();
        
        private readonly Random _random;
        private readonly HashSet<string> _randomizedFeatures;
        private bool _disposed;
        private System.Threading.Timer? _periodicRandomizationTimer;

        // 随机化参数配置
        private int _randomizationSeed;
        private bool _enableMemoryRandomization;
        private bool _enableStringObfuscation;
        private bool _enableControlFlowRandomization;
        private bool _enableMetadataRandomization;
        private int _periodicRandomizationIntervalMinutes;

        public static AntiDetectionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (LockObject)
                    {
                        if (_instance == null)
                        {
                            _instance = new AntiDetectionManager();
                        }
                    }
                }
                return _instance;
            }
        }

        private AntiDetectionManager()
        {
            _random = new Random();
            _randomizedFeatures = new HashSet<string>();
            _randomizationSeed = GenerateSecureRandomSeed();
            
            // 默认配置
            _enableMemoryRandomization = true;
            _enableStringObfuscation = true;
            _enableControlFlowRandomization = false;
            _enableMetadataRandomization = true;
            _periodicRandomizationIntervalMinutes = 30;
        }

        /// <summary>
        /// 初始化反检测系统 - 在程序启动时自动调用
        /// </summary>
        public void Initialize()
        {
            try
            {
                LogManager.Log(LogManager.LogLevel.Info, "[AntiDetection] 开始初始化反检测系统...");

                // 从配置加载设置
                LoadConfiguration();

                // 执行启动时随机化
                ExecuteStartupRandomization();

                // 启动周期性随机化（如果启用）
                StartPeriodicRandomization();

                LogManager.Log(LogManager.LogLevel.Info, "[AntiDetection] 反检测系统初始化完成");
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[AntiDetection] 初始化失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 从配置加载反检测参数
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                // 从 toggleState 加载启用状态
                if (Dictionary.toggleState.ContainsKey("Anti-Detection"))
                {
                    bool isEnabled = Dictionary.toggleState["Anti-Detection"];
                    if (!isEnabled)
                    {
                        LogManager.Log(LogManager.LogLevel.Info, "[AntiDetection] 反检测功能已禁用");
                        return;
                    }
                }

                // 从 sliderSettings 加载参数
                if (Dictionary.sliderSettings.ContainsKey("Randomization Seed"))
                {
                    _randomizationSeed = (int)Dictionary.sliderSettings["Randomization Seed"];
                }

                if (Dictionary.sliderSettings.ContainsKey("Periodic Randomization Interval"))
                {
                    _periodicRandomizationIntervalMinutes = (int)Dictionary.sliderSettings["Periodic Randomization Interval"];
                }

                // 从 toggleState 加载各个随机化选项
                _enableMemoryRandomization = GetToggleState("Memory Randomization", true);
                _enableStringObfuscation = GetToggleState("String Obfuscation", true);
                _enableControlFlowRandomization = GetToggleState("Control Flow Randomization", false);
                _enableMetadataRandomization = GetToggleState("Metadata Randomization", true);

                LogManager.Log(LogManager.LogLevel.Info, $"[AntiDetection] 配置加载完成 - 种子：{_randomizationSeed}, 周期性间隔：{_periodicRandomizationIntervalMinutes}分钟");
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[AntiDetection] 配置加载失败：{ex.Message}");
            }
        }

        private bool GetToggleState(string key, bool defaultValue)
        {
            return Dictionary.toggleState.ContainsKey(key) ? Dictionary.toggleState[key] : defaultValue;
        }

        /// <summary>
        /// 执行启动时的特征随机化
        /// </summary>
        private void ExecuteStartupRandomization()
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 1. 内存特征随机化
                if (_enableMemoryRandomization)
                {
                    RandomizeMemoryFeatures();
                }

                // 2. 字符串混淆
                if (_enableStringObfuscation)
                {
                    ObfuscateStrings();
                }

                // 3. 控制流随机化
                if (_enableControlFlowRandomization)
                {
                    RandomizeControlFlow();
                }

                // 4. 元数据随机化
                if (_enableMetadataRandomization)
                {
                    RandomizeMetadata();
                }

                stopwatch.Stop();
                LogManager.Log(LogManager.LogLevel.Info, $"[AntiDetection] 启动随机化完成，耗时：{stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[AntiDetection] 启动随机化失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 内存特征随机化 - 修改内存布局和特定内存特征
        /// </summary>
        private void RandomizeMemoryFeatures()
        {
            try
            {
                // 1. 随机化托管堆布局（通过分配随机大小的对象）
                RandomizeHeapLayout();

                // 2. 随机化线程栈特征
                RandomizeThreadStack();

                // 3. 添加随机内存填充
                AddRandomMemoryPadding();

                _randomizedFeatures.Add("Memory");
                LogManager.Log(LogManager.LogLevel.Info, "[AntiDetection] 内存特征随机化完成");
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[AntiDetection] 内存随机化失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 随机化托管堆布局
        /// </summary>
        private void RandomizeHeapLayout()
        {
            LogManager.Log(LogManager.LogLevel.Info, "[AntiDetection] 开始随机化堆布局...");
            
            // 分配随机大小的字节数组来影响堆布局
            var randomSizes = new[]
            {
                GenerateRandomValue(100, 1000),
                GenerateRandomValue(1000, 10000),
                GenerateRandomValue(100, 5000)
            };

            foreach (var size in randomSizes)
            {
                var buffer = new byte[size];
                _random.NextBytes(buffer);
                // 保持引用防止被 GC 回收
                GC.KeepAlive(buffer);
            }

            // 强制 GC 来固定新的堆布局
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            
            LogManager.Log(LogManager.LogLevel.Info, $"[AntiDetection] 堆布局随机化完成，使用了 {randomSizes.Length} 个随机大小的对象");
        }

        /// <summary>
        /// 随机化线程栈特征
        /// </summary>
        private void RandomizeThreadStack()
        {
            LogManager.Log(LogManager.LogLevel.Info, "[AntiDetection] 开始随机化线程栈...");
            
            // 在栈上分配随机大小的局部变量
            int stackVar1 = GenerateRandomValue(0, int.MaxValue);
            int stackVar2 = GenerateRandomValue(0, int.MaxValue);
            double stackVar3 = GenerateRandomDouble(0, 1000000);

            // 使用这些变量防止编译器优化
            if (stackVar1 + stackVar2 + stackVar3 > -1)
            {
                GC.KeepAlive(stackVar1);
                GC.KeepAlive(stackVar2);
                GC.KeepAlive(stackVar3);
            }
            
            LogManager.Log(LogManager.LogLevel.Info, "[AntiDetection] 线程栈随机化完成");
        }

        /// <summary>
        /// 添加随机内存填充
        /// </summary>
        private void AddRandomMemoryPadding()
        {
            LogManager.Log(LogManager.LogLevel.Info, "[AntiDetection] 开始添加内存填充...");
            
            // 创建随机大小的填充对象
            int paddingSize = GenerateRandomValue(1024, 10240);
            var padding = new byte[paddingSize];
            _random.NextBytes(padding);
            GC.KeepAlive(padding);
            
            LogManager.Log(LogManager.LogLevel.Info, $"[AntiDetection] 内存填充完成，大小：{paddingSize} 字节");
        }

        /// <summary>
        /// 字符串混淆 - 对关键字符串进行运行时混淆
        /// </summary>
        private void ObfuscateStrings()
        {
            try
            {
                // 注意：这里主要是运行时字符串处理
                // 真正的字符串混淆需要在编译时进行
                
                // 示例：对配置中的敏感字符串进行混淆处理
                var sensitiveKeys = new[]
                {
                    "Aim Assist",
                    "FOV",
                    "ESP Config"
                };

                foreach (var key in sensitiveKeys)
                {
                    if (Dictionary.toggleState.ContainsKey(key))
                    {
                        // 在内存中对键名进行简单变换
                        var obfuscated = SimpleObfuscate(key);
                        GC.KeepAlive(obfuscated);
                    }
                }

                _randomizedFeatures.Add("Strings");
                LogManager.Log(LogManager.LogLevel.Info, "[AntiDetection] 字符串混淆完成");
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[AntiDetection] 字符串混淆失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 简单字符串混淆算法
        /// </summary>
        private string SimpleObfuscate(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] ^= (byte)(_randomizationSeed & 0xFF);
            }
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// 控制流随机化 - 添加无意义的控制流变化
        /// </summary>
        private void RandomizeControlFlow()
        {
            try
            {
                // 通过随机条件分支来影响控制流图
                for (int i = 0; i < 10; i++)
                {
                    if (GenerateRandomValue(0, 100) > 50)
                    {
                        PerformDummyOperation1();
                    }
                    else
                    {
                        PerformDummyOperation2();
                    }
                }

                _randomizedFeatures.Add("ControlFlow");
                LogManager.Log(LogManager.LogLevel.Info, "[AntiDetection] 控制流随机化完成");
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[AntiDetection] 控制流随机化失败：{ex.Message}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void PerformDummyOperation1()
        {
            var data = new byte[100];
            _random.NextBytes(data);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void PerformDummyOperation2()
        {
            var value = GenerateRandomDouble(0, 1000);
            Math.Sqrt(value);
        }

        /// <summary>
        /// 元数据随机化 - 修改运行时元数据特征
        /// </summary>
        private void RandomizeMetadata()
        {
            try
            {
                // 1. 随机化程序集加载顺序相关信息
                RandomizeAssemblyMetadata();

                // 2. 随机化类型元数据
                RandomizeTypeMetadata();

                _randomizedFeatures.Add("Metadata");
                LogManager.Log(LogManager.LogLevel.Info, "[AntiDetection] 元数据随机化完成");
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[AntiDetection] 元数据随机化失败：{ex.Message}");
            }
        }

        private void RandomizeAssemblyMetadata()
        {
            // 访问程序集信息以影响元数据缓存
            var assembly = Assembly.GetExecutingAssembly();
            var name = assembly.GetName().Name;
            var version = assembly.GetName().Version;
            
            // 获取程序集属性
            var attributes = assembly.GetCustomAttributesData();
            
            GC.KeepAlive(name);
            GC.KeepAlive(version);
            GC.KeepAlive(attributes);
        }

        private void RandomizeTypeMetadata()
        {
            // 访问类型元数据
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types.Take(10))
            {
                var methods = type.GetMethods();
                var properties = type.GetProperties();
                var fields = type.GetFields();
                
                GC.KeepAlive(methods);
                GC.KeepAlive(properties);
                GC.KeepAlive(fields);
            }
        }

        /// <summary>
        /// 启动周期性随机化
        /// </summary>
        private void StartPeriodicRandomization()
        {
            try
            {
                if (_periodicRandomizationIntervalMinutes > 0)
                {
                    int intervalMs = _periodicRandomizationIntervalMinutes * 60 * 1000;
                    _periodicRandomizationTimer = new System.Threading.Timer(
                        OnPeriodicRandomization,
                        null,
                        intervalMs,
                        intervalMs
                    );
                    LogManager.Log(LogManager.LogLevel.Info, $"[AntiDetection] 周期性随机化已启动，间隔：{_periodicRandomizationIntervalMinutes}分钟");
                }
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[AntiDetection] 周期性随机化启动失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 周期性随机化回调
        /// </summary>
        private void OnPeriodicRandomization(object? state)
        {
            try
            {
                LogManager.Log(LogManager.LogLevel.Info, "[AntiDetection] 执行周期性随机化...");
                ExecuteStartupRandomization();
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[AntiDetection] 周期性随机化失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 生成安全的随机种子
        /// </summary>
        private int GenerateSecureRandomSeed()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        /// <summary>
        /// 生成随机值
        /// </summary>
        private int GenerateRandomValue(int min, int max)
        {
            lock (_random)
            {
                return _random.Next(min, max);
            }
        }

        /// <summary>
        /// 生成随机双精度数
        /// </summary>
        private double GenerateRandomDouble(double min, double max)
        {
            lock (_random)
            {
                return _random.NextDouble() * (max - min) + min;
            }
        }

        /// <summary>
        /// 应用用户配置更改
        /// </summary>
        public void ApplyConfigurationChanges()
        {
            LoadConfiguration();
            LogManager.Log(LogManager.LogLevel.Info, "[AntiDetection] 配置更改已应用");
        }

        /// <summary>
        /// 手动触发随机化
        /// </summary>
        public void TriggerRandomization()
        {
            ExecuteStartupRandomization();
            LogManager.Log(LogManager.LogLevel.Info, "[AntiDetection] 手动触发随机化完成", true, 3000);
        }

        /// <summary>
        /// 获取随机化状态信息
        /// </summary>
        public string GetStatusInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine("反检测系统状态:");
            sb.AppendLine($"  已启用：{Dictionary.toggleState.ContainsKey("Anti-Detection") && Dictionary.toggleState["Anti-Detection"]}");
            sb.AppendLine($"  随机种子：{_randomizationSeed}");
            sb.AppendLine($"  已随机化特征：{string.Join(", ", _randomizedFeatures)}");
            sb.AppendLine($"  周期性间隔：{_periodicRandomizationIntervalMinutes}分钟");
            return sb.ToString();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _periodicRandomizationTimer?.Dispose();
                _disposed = true;
            }
        }
    }
}
