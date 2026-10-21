using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace HapetFrontend.Syntax.Diagnostics
{
    public static class HapetAssert
    {
        // TODO:
        // public IMessageHandler CurrentMessageHandler { get; set; }

        /// <inheritdoc cref="Debug.Assert(bool)"/>
        public static void Assert([DoesNotReturnIf(false)] bool condition) => Assert(condition, string.Empty);

        /// <inheritdoc cref="Debug.Assert(bool, string)"/>
        public static void Assert([DoesNotReturnIf(false)] bool condition, string message)
        {
#if DEBUG
            Debug.Assert(condition, message);
#endif
            // TODO: 
            /*
            if (CurrentMessageHandler != null) 
            {
                CurrentMessageHandler.Send("Unknown compiler error");
            }
             */
        }

        public static void AssertNotNull<T>([NotNull] T value) => Assert(value is object, "Unexpected null reference");
    }
}
