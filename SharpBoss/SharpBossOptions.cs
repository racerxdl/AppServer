using System;
using System.IO;

namespace SharpBoss;

/// <summary>
/// Configures application loading and serialization for a <see cref="SharpBoss"/> server.
/// </summary>
public sealed class SharpBossOptions
{
    /// <summary>
    /// Gets or initializes the directory containing application subdirectories.
    /// </summary>
    public string ApplicationsDirectory { get; init; } = Path.Combine(Environment.CurrentDirectory, "apps");

    /// <summary>
    /// Gets or initializes the directory used for isolated deployment copies.
    /// </summary>
    public string DeploymentDirectory { get; init; } = Path.Combine(Environment.CurrentDirectory, "deployed");

    /// <summary>
    /// Gets or initializes the delay used to coalesce file-system events.
    /// </summary>
    public TimeSpan ReloadDebounce { get; init; } = TimeSpan.FromMilliseconds(500);


    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApplicationsDirectory))
        {
            throw new ArgumentException("An applications directory is required.", nameof(ApplicationsDirectory));
        }

        if (string.IsNullOrWhiteSpace(DeploymentDirectory))
        {
            throw new ArgumentException("A deployment directory is required.", nameof(DeploymentDirectory));
        }

        if (ReloadDebounce <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ReloadDebounce), "Reload debounce must be positive.");
        }

    }
}
