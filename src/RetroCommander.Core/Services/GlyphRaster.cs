//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.IO;
using RetroIO.Storage.Core;

namespace RetroCommander.Core.Services;

/// <summary>
/// Reads the pixels of raw directory name bytes from a character ROM file the user supplies, the
/// way the disk's driver describes it (<see cref="RawDirectoryLayout"/>). Shared by the CLI's
/// braille drawing and the window's directory listing.
/// </summary>
/// <remarks>
/// No ROM ships with RetroCommander. Each glyph is <see cref="RawDirectoryLayout.GlyphHeight"/>
/// bytes, one per row, the top bit the left pixel. A reversed glyph is read from the ROM as it is,
/// never made by flipping bits: in the C64 ROM 901225-01 the reversed @ is not an exact flip of the
/// plain one (row 5, measured 2026-11-23).
/// </remarks>
public sealed class GlyphRaster
{
    private readonly byte[] _rom;
    private readonly RawDirectoryLayout _layout;
    private readonly int _setOffset;

    private GlyphRaster(byte[] rom, RawDirectoryLayout layout, int set)
    {
        _rom = rom;
        _layout = layout;
        _setOffset = layout.CharacterSets[set].Offset;
        SetName = layout.CharacterSets[set].Name;
    }

    /// <summary>
    /// Gets the name of the character set the glyphs are read from.
    /// </summary>
    public string SetName { get; }

    /// <summary>
    /// Gets the width of one glyph in pixels.
    /// </summary>
    public int GlyphWidth => _layout.GlyphWidth;

    /// <summary>
    /// Gets the height of one glyph in pixels.
    /// </summary>
    public int GlyphHeight => _layout.GlyphHeight;

    /// <summary>
    /// Tells whether a driver's layout gives a drawing at all.
    /// </summary>
    /// <param name="layout">
    /// The driver's raw directory layout.
    /// </param>
    /// <returns>
    /// True when the layout names a glyph for each byte and at least one character set.
    /// </returns>
    public static bool CanDraw(RawDirectoryLayout layout)
        => layout.GlyphIndex.Length == 256 && layout.CharacterSets.Length > 0
           && layout.GlyphWidth is > 0 and <= 8 && layout.GlyphHeight > 0;

    /// <summary>
    /// Reads a character ROM file for a layout.
    /// </summary>
    /// <param name="path">
    /// The ROM file.
    /// </param>
    /// <param name="layout">
    /// The driver's raw directory layout; it must give a drawing (<see cref="CanDraw"/>).
    /// </param>
    /// <param name="set">
    /// The character set, by its place in <see cref="RawDirectoryLayout.CharacterSets"/>.
    /// </param>
    /// <returns>
    /// The raster.
    /// </returns>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the file does not exist.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the file has the wrong size; the message names the ROM the layout expects.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="set"/> is not one of the layout's sets.
    /// </exception>
    public static GlyphRaster Load(string path, RawDirectoryLayout layout, int set)
    {
        if (!CanDraw(layout))
            throw new ArgumentException("This disk's driver gives no drawing.", nameof(layout));
        if (set < 0 || set >= layout.CharacterSets.Length)
            throw new ArgumentOutOfRangeException(nameof(set), "This disk's character ROM has " + layout.CharacterSets.Length + " character set(s).");
        if (!File.Exists(path))
            throw new FileNotFoundException("No character ROM file: " + path, path);

        byte[] rom = File.ReadAllBytes(path);
        if (rom.Length != layout.CharacterRomSize)
            throw new InvalidDataException(path + " is " + rom.Length + " bytes; this disk is drawn with " + layout.CharacterRomDescription);

        return new GlyphRaster(rom, layout, set);
    }

    /// <summary>
    /// Gets the pixel rows of the glyph one byte is drawn with.
    /// </summary>
    /// <param name="value">
    /// The byte as stored on the disk.
    /// </param>
    /// <returns>
    /// <see cref="GlyphHeight"/> bytes, one per row, the top bit the left pixel.
    /// </returns>
    public ReadOnlySpan<byte> Glyph(byte value)
        => _rom.AsSpan(_setOffset + (_layout.GlyphIndex[value] * _layout.GlyphHeight), _layout.GlyphHeight);

    /// <summary>
    /// Tells whether one pixel of a glyph is on.
    /// </summary>
    /// <param name="value">
    /// The byte as stored on the disk.
    /// </param>
    /// <param name="x">
    /// The column, 0 at the left.
    /// </param>
    /// <param name="y">
    /// The row, 0 at the top.
    /// </param>
    /// <returns>
    /// True when the pixel is set.
    /// </returns>
    public bool Pixel(byte value, int x, int y)
        => (Glyph(value)[y] & (0x80 >> x)) != 0;
}
