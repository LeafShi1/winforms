﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copied from:
// https://github.com/dotnet/runtime/blob/218ef0f7776c2c20f6c594e3475b80f1fe2d7d08/src/libraries/System.Private.CoreLib/src/System/Runtime/CompilerServices/IsExternalInit.cs

#if NET6_0_OR_GREATER

using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(IsExternalInit))]

#else

using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    ///  Reserved to be used by the compiler for tracking metadata.
    ///  This class should not be used by developers in source code.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}

#endif
