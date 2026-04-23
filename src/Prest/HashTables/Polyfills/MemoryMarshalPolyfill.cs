#if !NET6_0_OR_GREATER

namespace System.Runtime.InteropServices;

// ---------------------------------------------------------------------------
// Polyfill for System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference
// on netstandard2.0 / netstandard2.1.
//
// On net6+ this file compiles to nothing — the BCL's intrinsic is used.
// On older TFMs we add the missing static via C# 14 extension-member syntax,
// falling back to the Span-based equivalent.
// ---------------------------------------------------------------------------

/// <summary>C# 14 extension members adding the missing
/// <c>MemoryMarshal.GetArrayDataReference&lt;T&gt;</c> overload for pre-net6 TFMs.</summary>
public static class MemoryMarshalPolyfill
{
    extension(MemoryMarshal)
    {
        /// <summary>
        /// Returns a reference to the 0th element of <paramref name="array" /> without
        /// bounds-checking. Equivalent to the BCL intrinsic on net6+.
        /// </summary>
        public static ref T GetArrayDataReference<T>(T[] array) =>
            ref MemoryMarshal.GetReference(array.AsSpan());
    }
}

#endif
