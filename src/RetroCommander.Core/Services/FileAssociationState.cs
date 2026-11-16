//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.Services
{
    /// <summary>
    /// What Windows currently does with one file extension, as far as RetroCommander is concerned.
    /// </summary>
    public enum FileAssociationState
    {
        /// <summary>
        /// The extension is not linked to RetroCommander.
        /// </summary>
        NotLinked,

        /// <summary>
        /// The extension is linked to RetroCommander at the exe path given.
        /// </summary>
        Linked,

        /// <summary>
        /// The extension is linked to RetroCommander, but to an exe at a different path (the
        /// program was moved or a different copy was linked). Opening a file would start that copy.
        /// </summary>
        LinkedToOtherCopy
    }
}
