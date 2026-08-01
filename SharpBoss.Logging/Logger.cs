using System;
using System.IO;
using NLog;

namespace SharpBoss.Logging;

/// <summary>
/// SharpBoss logging facade with a deterministic file fallback.
/// </summary>
public static class Logger
{
    private const string ConfigurationFileEnvironmentVariable = "SHARPBOSS_CONFIG_FILENAME";
    private static readonly NLog.Logger Log;

    static Logger()
    {
        Log = LogManager.GetCurrentClassLogger();
        EnsureConfigured();
    }

    public static void Info(string message)
    {
        Log.Info(message);
    }

    public static void Debug(string message)
    {
        Log.Debug(message);
    }

    public static void Warn(string message)
    {
        Log.Warn(message);
    }

    public static void Error(string message)
    {
        Log.Error(message);
    }

    public static void Error(string message, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Log.Error(exception, message);
    }

    public static void Flush()
    {
        LogManager.Flush(TimeSpan.FromSeconds(5));
    }

    private static void EnsureConfigured()
    {
        if (LogManager.Configuration is { AllTargets.Count: > 0 })
        {
            return;
        }

        LogManager.Setup().LoadConfiguration(builder => builder
            .ForLogger()
            .FilterMinLevel(LogLevel.Debug)
            .WriteToFile(
                fileName: GetFileName(),
                layout: "${longdate} ${level:lowercase=true} [${logger}] ${message} ${exception:format=tostring}"));
    }

    private static string GetFileName()
    {
        var configuredFileName = Environment.GetEnvironmentVariable(ConfigurationFileEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredFileName))
        {
            return Path.GetFullPath(configuredFileName);
        }

        return Path.Combine(AppContext.BaseDirectory, $"sharpboss_{DateTime.UtcNow:yyyy-MM-dd}.log");
    }
}
