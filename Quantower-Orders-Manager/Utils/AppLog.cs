using System;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.Utils
{
    public static class AppLog
    {
        private static volatile bool _useBridgeLogging = false;

        /// <summary>
        /// Flag placeholder for future bridge-based logging.
        /// Currently unused and hardcoded to false in production flow.
        /// </summary>
        public static bool UseBridgeLogging
        {
            get => _useBridgeLogging;
            set => _useBridgeLogging = value;
        }

        private static void Write(string component, string reason, string message, LoggingLevel level)
        {
            var prefix = string.IsNullOrWhiteSpace(component) ? "General" : component.Trim();
            var tag = string.IsNullOrWhiteSpace(reason) ? "General" : reason.Trim();

            // Bridge forwarding will be added in subsequent iterations when UseBridgeLogging is honoured.
            Core.Instance.Loggers.Log($"[{prefix}][{tag}] {message}", level);
        }

        public static void Log(string component, string reason, string message, LoggingLevel level) => Write(component, reason, message, level);
        public static void Info(string component, string reason, string message) => Write(component, reason, message, LoggingLevel.System);
        public static void System(string component, string reason, string message) => Write(component, reason, message, LoggingLevel.System);
        public static void Trading(string component, string reason, string message) => Write(component, reason, message, LoggingLevel.Trading);
        public static void Error(string component, string reason, string message) => Write(component, reason, message, LoggingLevel.Error);
        public static void Error(string component, string reason, string message, Exception ex) => Write(component, reason, $"{message} | Exception: {ex.Message}", LoggingLevel.Error);
    }
}
