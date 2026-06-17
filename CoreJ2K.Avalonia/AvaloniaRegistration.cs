// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System.Runtime.CompilerServices;
using CoreJ2K.Util;

namespace CoreJ2K.Avalonia
{
    internal static class AvaloniaRegistration
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries", Justification = "Intentional auto-registration for plugin assembly.")]
        [ModuleInitializer]
        internal static void Register()
        {
            ImageFactory.Register(new AvaloniaImageCreator());
        }
    }
}
