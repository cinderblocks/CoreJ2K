// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System.Runtime.CompilerServices;
using CoreJ2K.Util;

namespace CoreJ2K.ImageSharp
{
    internal static class ImageSharpRegistration
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries", Justification = "Intentional auto-registration for plugin assembly.")]
        [ModuleInitializer]
        internal static void Register()
        {
            ImageFactory.Register(new ImageSharpImageCreator());
        }
    }
}
