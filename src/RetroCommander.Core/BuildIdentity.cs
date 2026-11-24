//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Reflection;

namespace RetroCommander.Core;

/// <summary>
/// Which build of RetroCommander is running: version, git commit and branch, whether the tree had
/// uncommitted changes, and when it was built.
/// </summary>
/// <remarks>
/// <para><b>Where the values come from</b></para>
/// src/Directory.Build.props stamps them into every assembly at build time, by the same rules as
/// RetroTerm, so Help, About and <c>retrocommander-cli version</c> report a build the way RetroTerm
/// does. Nothing here holds a second copy of a version or a date.
/// <para><b>Which assembly is read</b></para>
/// The entry assembly, the program that was started, when it carries the stamp; otherwise this
/// library, which is what happens under a test runner. Both are built from the same tree by the
/// same rules. A value the build did not record reads <see cref="Unknown"/>.
/// </remarks>
public static class BuildIdentity
{
    /// <summary>
    /// What a value reads when the build did not record it.
    /// </summary>
    public const string Unknown = "unknown";

    private static readonly Assembly s_stamped = ChooseStampedAssembly();

    /// <summary>
    /// Gets the version, from Directory.Build.props, without the commit the SDK adds after a plus.
    /// </summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>
    /// Gets the copyright line, from Directory.Build.props.
    /// </summary>
    public static string Copyright { get; } = ReadCopyright();

    /// <summary>
    /// Gets the short git commit the build came from (12 hex digits).
    /// </summary>
    public static string Commit { get; } = ReadMetadata("GitCommit");

    /// <summary>
    /// Gets the branch the build came from.
    /// </summary>
    public static string Branch { get; } = ReadMetadata("GitBranch");

    /// <summary>
    /// Gets a value indicating whether the working tree had uncommitted changes at build time.
    /// </summary>
    /// <remarks>
    /// A commit alone claims the build is exactly that commit; during ordinary work it often is not.
    /// </remarks>
    public static bool IsDirty { get; } = string.Equals(ReadMetadata("GitDirty"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the date the build was made, as "dd.MMM.yyyy".
    /// </summary>
    public static string BuildDate { get; } = ReadMetadata("BuildDate");

    /// <summary>
    /// Gets the time the build was made, as "HH:mm:ss".
    /// </summary>
    public static string BuildTime { get; } = ReadMetadata("BuildTime");

    /// <summary>
    /// Gets the commit line, as Help, About shows it.
    /// </summary>
    /// <returns>
    /// For example "commit 3ff115b2a1c0 on main (uncommitted changes)".
    /// </returns>
    public static string CommitLine()
        => "commit " + Commit + " on " + Branch + (IsDirty ? " (uncommitted changes)" : string.Empty);

    /// <summary>
    /// Names the build in one line, for a log or a version command.
    /// </summary>
    /// <param name="program">
    /// The program name to start the line with.
    /// </param>
    /// <returns>
    /// For example "RetroCommander 0.1.0, commit 3ff115b2a1c0 on main, built 24.Nov.2026 10:12:03".
    /// </returns>
    public static string Describe(string program)
        => program + " " + Version + ", " + CommitLine() + ", built " + BuildDate + " " + BuildTime;

    private static Assembly ChooseStampedAssembly()
    {
        Assembly self = typeof(BuildIdentity).Assembly;
        Assembly? entry = Assembly.GetEntryAssembly();
        return entry != null && Find(entry, "GitCommit") != null ? entry : self;
    }

    private static string ReadVersion()
    {
        AssemblyInformationalVersionAttribute? informational = s_stamped.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (informational != null && !string.IsNullOrEmpty(informational.InformationalVersion))
        {
            // The SDK appends the full 40-digit commit after a plus; the commit is shown on its own.
            string text = informational.InformationalVersion;
            int plus = text.IndexOf('+');
            return plus > 0 ? text.Substring(0, plus) : text;
        }

        Version? version = s_stamped.GetName().Version;
        return version == null ? Unknown : version.ToString();
    }

    private static string ReadCopyright()
    {
        AssemblyCopyrightAttribute? copyright = s_stamped.GetCustomAttribute<AssemblyCopyrightAttribute>();
        return copyright == null || string.IsNullOrEmpty(copyright.Copyright) ? Unknown : copyright.Copyright;
    }

    private static string ReadMetadata(string key)
    {
        string? value = Find(s_stamped, key);
        return string.IsNullOrEmpty(value) ? Unknown : value;
    }

    private static string? Find(Assembly assembly, string key)
    {
        object[] all = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false);
        for (int i = 0; i < all.Length; i++)
        {
            AssemblyMetadataAttribute a = (AssemblyMetadataAttribute)all[i];
            if (string.Equals(a.Key, key, StringComparison.Ordinal))
            {
                return a.Value;
            }
        }

        return null;
    }
}
