using System;
using System.Configuration;
using System.Diagnostics;

using NLog;
using NLog.Config;
using NLog.Targets;

namespace SharpBoss.Logging
{
    /// <summary>
    /// Logger class
    /// </summary>
    public static class Logger
    {
        private static readonly string _datePattern = "yyyy-MM-dd";
        private static readonly string _filenamePattern = "sharpboss_{0}.txt";
        private static readonly string _environmentVariable = "SHARPBOSS_CONFIG_FILENAME";
        private static readonly string _configKey = "SHARPBOSS_CONFIG_FILENAME";

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Retrieve Logger
        /// </summary>
        /// <returns>ILogger</returns>
        private static ILogger GetLogger()
        {
            //var stackFrame = new StackFrame (2, true);
            //var method = stackFrame.GetMethod ();
            //var assembly = method.DeclaringType;

            //LogManager.Configuration = GetConfig ();

            //var loggingName = string.Format ("{0}::{1}", assembly.FullName, method.Name);

            //return LogManager.GetLogger (loggingName);
            return Log;
        }

        /// <summary>
        /// Retrieve filename for Logging output
        /// </summary>
        /// <returns>Retrieve filename for Logging output</returns>
        private static string GetFileName()
        {
            var dateTime = DateTime.Now;
            var filename = string.Format(_filenamePattern, dateTime.ToString(_datePattern));
            var environmentVariable = Environment.GetEnvironmentVariable(_environmentVariable);
            var configValue = ConfigurationManager.AppSettings[_configKey];

            if (environmentVariable != null)
            {
                return environmentVariable;
            }
            else if (configValue != null)
            {
                return configValue;
            }

            return filename;
        }

        /// <summary>
        /// Get configuration for Log target
        /// </summary>
        /// <returns>NLog Configuration</returns>
        private static LoggingConfiguration GetConfig()
        {
            var config = new LoggingConfiguration();
            var target = new FileTarget
            {
                FileName = GetFileName(),
                Layout = "${longdate} ${level:lowercase=true} [${logger}] ${message}",
            };

            config.AddRuleForAllLevels(target);

            return config;
        }

        /// <summary>
        /// Retrieve message with format
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <returns>Formatted message</returns>
        private static string GetMessage(string message)
        {
            return string.Format("{0}", message);
        }

        /// <summary>
        /// Log message with info level
        /// </summary>
        /// <param name="message">Message to log</param>
        public static void Info(string message)
        {
            var logger = GetLogger();
            logger.Info(GetMessage(message));
        }

        /// <summary>
        /// Log message with debug level
        /// </summary>
        /// <param name="message">Message to log</param>
        public static void Debug(string message)
        {
            var logger = GetLogger();
            logger.Debug(GetMessage(message));
        }

        /// <summary>
        /// Log message with warning level
        /// </summary>
        /// <param name="message">Message to log</param>
        public static void Warn(string message)
        {
            var logger = GetLogger();
            logger.Warn(GetMessage(message));
        }

        /// <summary>
        /// Log message with error level
        /// </summary>
        /// <param name="message">Message to log</param>
        public static void Error(string message)
        {
            var logger = GetLogger();
            logger.Error(GetMessage(message));
        }
    }
}