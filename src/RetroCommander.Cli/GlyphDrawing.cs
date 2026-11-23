//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.IO;
using System.Text;
using RetroCommander.Core.Services;
using RetroIO.Storage.Core;

namespace RetroCommander.Cli
{
    /// <summary>
    /// Draws raw directory names as the machine draws them: each glyph's pixels, read from a
    /// character ROM file the user supplies (<see cref="GlyphRaster"/>), in Unicode braille cells of
    /// 2 by 4 dots.
    /// </summary>
    /// <remarks>
    /// An 8 by 8 glyph is 4 cells wide and 2 lines high, so a 16-byte name is 64 columns. Checked
    /// by eye in Windows Terminal (PowerShell) and in WSL on 2026-11-23. Without a ROM the CLI shows
    /// names as hex and ASCII only.
    /// </remarks>
    internal sealed class GlyphDrawing
    {
        /// <summary>
        /// The environment variable naming the character ROM file when <c>--charset</c> is not given.
        /// </summary>
        public const string CharsetVariable = "RETROCOMMANDER_CHARSET";

        // Braille dot bits by pixel row inside a cell (0 to 3), for the left and the right column:
        // dots 1,2,3,7 on the left, 4,5,6,8 on the right (Unicode U+2800 block).
        private static readonly int[] s_left = { 0x01, 0x02, 0x04, 0x40 };
        private static readonly int[] s_right = { 0x08, 0x10, 0x20, 0x80 };

        private readonly GlyphRaster _raster;

        private GlyphDrawing(GlyphRaster raster)
        {
            _raster = raster;
        }

        /// <summary>
        /// Gets the number of text lines one drawn name takes.
        /// </summary>
        public int Lines => (_raster.GlyphHeight + 3) / 4;

        /// <summary>
        /// Gets the character set the names are drawn in.
        /// </summary>
        public string SetName => _raster.SetName;

        /// <summary>
        /// Opens the character ROM named by <c>--charset</c> or <see cref="CharsetVariable"/>, for a
        /// disk whose driver gives a drawing.
        /// </summary>
        /// <param name="o">
        /// The command line; <c>--lower</c> picks the driver's second character set.
        /// </param>
        /// <param name="layout">
        /// The disk driver's raw directory layout.
        /// </param>
        /// <returns>
        /// The drawing, or null when no ROM is named or the driver gives no drawing.
        /// </returns>
        /// <exception cref="CliException">
        /// Thrown when the file is missing, has the wrong size, or the set asked for does not exist.
        /// </exception>
        public static GlyphDrawing? Open(GlobalOptions o, RawDirectoryLayout layout)
        {
            string? path = o.Value("--charset") ?? Environment.GetEnvironmentVariable(CharsetVariable);
            if (string.IsNullOrEmpty(path) || !GlyphRaster.CanDraw(layout))
            {
                return null;
            }

            int set = o.Flags.Contains("--lower") ? 1 : 0;
            try
            {
                return new GlyphDrawing(GlyphRaster.Load(path, layout, set));
            }
            catch (FileNotFoundException)
            {
                throw new CliException("no character ROM file: " + path);
            }
            catch (InvalidDataException e)
            {
                throw new CliException(e.Message);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new CliException("this disk's character ROM has one character set; --lower has nothing to pick", usage: true);
            }
        }

        /// <summary>
        /// Draws a run of raw bytes.
        /// </summary>
        /// <param name="bytes">
        /// The bytes as stored on the disk.
        /// </param>
        /// <returns>
        /// <see cref="Lines"/> lines of braille cells.
        /// </returns>
        public string[] Draw(ReadOnlySpan<byte> bytes)
        {
            int width = _raster.GlyphWidth;
            int height = _raster.GlyphHeight;
            int cells = (width + 1) / 2;
            StringBuilder[] lines = new StringBuilder[Lines];
            for (int l = 0; l < lines.Length; l++)
            {
                lines[l] = new StringBuilder(bytes.Length * cells);
            }

            for (int i = 0; i < bytes.Length; i++)
            {
                ReadOnlySpan<byte> glyph = _raster.Glyph(bytes[i]);
                for (int band = 0; band < lines.Length; band++)
                {
                    for (int cell = 0; cell < cells; cell++)
                    {
                        int dots = 0;
                        for (int r = 0; r < 4; r++)
                        {
                            int row = (band * 4) + r;
                            if (row >= height)
                            {
                                break;
                            }

                            int x = cell * 2;
                            if ((glyph[row] & (0x80 >> x)) != 0)
                            {
                                dots |= s_left[r];
                            }

                            if (x + 1 < width && (glyph[row] & (0x80 >> (x + 1))) != 0)
                            {
                                dots |= s_right[r];
                            }
                        }

                        lines[band].Append((char)(0x2800 + dots));
                    }
                }
            }

            string[] result = new string[lines.Length];
            for (int l = 0; l < lines.Length; l++)
            {
                result[l] = lines[l].ToString();
            }

            return result;
        }
    }
}
