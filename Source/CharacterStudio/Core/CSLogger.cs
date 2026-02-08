using System;
using System.Collections.Generic;
using System.Diagnostics;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    /// <summary>
    /// CharacterStudio 统一日志系统
    /// 提供分级日志、性能计时和上下文信息
    /// </summary>
    public static class CSLogger
    {
        // 配置
        private const string LogPrefix = "[CharacterStudio]";
        private static LogLevel minimumLevel = LogLevel.Info;
        private static bool enableDebugMode = false;
        
        // 性能计时
        private static readonly Dictionary<string, Stopwatch> timers = new Dictionary<string, Stopwatch>();
        
        // 最近日志缓存（用于调试）
        private static readonly Queue<string> recentLogs = new Queue<string>();
        private const int MaxRecentLogs = 100;

        /// <summary>
        /// 设置最低日志级别
        /// </summary>
        public static void SetMinimumLevel(LogLevel level)
        {
            minimumLevel = level;
        }

        /// <summary>
        /// 启用/禁用调试模式
        /// </summary>
        public static void SetDebugMode(bool enabled)
        {
            enableDebugMode = enabled;
            if (enabled)
            {
                minimumLevel = LogLevel.Debug;
            }
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        public static void Debug(string message, string? context = null)
        {
            if (!enableDebugMode) return;
            LogInternal(LogLevel.Debug, message, context);
        }

        /// <summary>
        /// 信息日志
        /// </summary>
        public static void Info(string message, string? context = null)
        {
            LogInternal(LogLevel.Info, message, context);
        }

        /// <summary>
        /// 警告日志
        /// </summary>
        public static void Warn(string message, string? context = null)
        {
            LogInternal(LogLevel.Warning, message, context);
        }

        /// <summary>
        /// 错误日志
        /// </summary>
        public static void Error(string message, Exception? ex = null, string? context = null)
        {
            string fullMessage = message;
            if (ex != null)
            {
                fullMessage += $"\n异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
            }
            LogInternal(LogLevel.Error, fullMessage, context);
        }

        /// <summary>
        /// 开始性能计时
        /// </summary>
        public static void StartTimer(string operationName)
        {
            if (!enableDebugMode) return;
            
            if (!timers.ContainsKey(operationName))
            {
                timers[operationName] = new Stopwatch();
            }
            timers[operationName].Restart();
        }

        /// <summary>
        /// 结束性能计时并记录
        /// </summary>
        public static void StopTimer(string operationName)
        {
            if (!enableDebugMode) return;
            
            if (timers.TryGetValue(operationName, out var stopwatch))
            {
                stopwatch.Stop();
                Debug($"操作 '{operationName}' 耗时: {stopwatch.ElapsedMilliseconds}ms", "Performance");
            }
        }

        /// <summary>
        /// 使用 using 语句的计时块
        /// </summary>
        public static IDisposable TimedOperation(string operationName)
        {
            return new TimerScope(operationName);
        }

        /// <summary>
        /// 获取最近的日志记录
        /// </summary>
        public static string[] GetRecentLogs()
        {
            return recentLogs.ToArray();
        }

        /// <summary>
        /// 清除日志缓存
        /// </summary>
        public static void ClearLogs()
        {
            recentLogs.Clear();
        }

        private static void LogInternal(LogLevel level, string message, string? context)
        {
            if (level < minimumLevel) return;

            string contextStr = string.IsNullOrEmpty(context) ? "" : $"[{context}] ";
            string fullMessage = $"{LogPrefix} {contextStr}{message}";

            // 添加到最近日志
            lock (recentLogs)
            {
                recentLogs.Enqueue($"[{DateTime.Now:HH:mm:ss}][{level}] {fullMessage}");
                while (recentLogs.Count > MaxRecentLogs)
                {
                    recentLogs.Dequeue();
                }
            }

            // 输出到 RimWorld 日志
            switch (level)
            {
                case LogLevel.Debug:
                case LogLevel.Info:
                    Log.Message(fullMessage);
                    break;
                case LogLevel.Warning:
                    Log.Warning(fullMessage);
                    break;
                case LogLevel.Error:
                    Log.Error(fullMessage);
                    break;
            }
        }

        /// <summary>
        /// 计时器作用域
        /// </summary>
        private class TimerScope : IDisposable
        {
            private readonly string operationName;
            private readonly Stopwatch stopwatch;

            public TimerScope(string name)
            {
                operationName = name;
                stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                stopwatch.Stop();
                if (enableDebugMode)
                {
                    Debug($"操作 '{operationName}' 耗时: {stopwatch.ElapsedMilliseconds}ms", "Performance");
                }
            }
        }
    }
}