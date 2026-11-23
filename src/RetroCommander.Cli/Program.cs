//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Threading.Tasks;

namespace RetroCommander.Cli
{
    /// <summary>
    /// The retrocommander-cli entry point.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Runs one command and returns its exit code: 0 done, 1 the command failed, 2 bad usage.
        /// With no command it opens the interactive prompt on the keyboard.
        /// </summary>
        /// <param name="args">
        /// The command and its arguments.
        /// </param>
        /// <returns>
        /// The exit code.
        /// </returns>
        public static Task<int> Main(string[] args)
        {
            // UTF-8 out, so drawn names (Unicode braille, see GlyphDrawing) and non-ASCII file names
            // reach the terminal as characters; the Windows console's own code page turns them into '?'.
            Console.OutputEncoding = new System.Text.UTF8Encoding(false);
            return new CliApp(Console.Out, Console.Error, Console.In).RunAsync(args);
        }
    }
}
