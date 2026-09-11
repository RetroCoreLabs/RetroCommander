//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

namespace RetroCommander.Core.Metadata
{
    /// <summary>
    /// Represents a single metadata property for UI display.
    /// Used to display filesystem-specific details in a generic way.
    /// </summary>
    public sealed class MetadataProperty
    {
        /// <summary>
        /// Category for grouping (e.g., "Disk Information", "Geometry", "Validation").
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable label (e.g., "Type Name", "Bytes per Sector").
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Formatted value string (e.g., "3,600 RPM", "Valid ✓").
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Optional icon/emoji for visual identification (e.g., "✓", "❌", "⚠️").
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// True if this property represents a section header (for UI rendering).
        /// </summary>
        public bool IsSectionHeader { get; set; }
    }
}
