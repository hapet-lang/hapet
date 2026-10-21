// This file is ported and adapted from the Akbura

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace HapetFrontend.Syntax.Diagnostics
{
    /// <summary>
    /// Describes how severe a diagnostic is.
    /// </summary>
    public enum HapetDiagnosticSeverity
    {
        /// <summary>
        /// Something that is an issue, as determined by some authority,
        /// but is not surfaced through normal means.
        /// There may be different mechanisms that act on these issues.
        /// </summary>
        Hidden = 0,

        /// <summary>
        /// Information that does not indicate a problem (i.e. not prescriptive).
        /// </summary>
        Info = 1,

        /// <summary>
        /// Something suspicious but allowed.
        /// </summary>
        Warning = 2,

        /// <summary>
        /// Something not allowed by the rules of the language or other authority.
        /// </summary>
        Error = 3,
    }

    public class HapetDiagnostic
    {
        public HapetDiagnostic()
        {

        }

        [SetsRequiredMembers]
        public HapetDiagnostic(ImmutableArray<object> parameters, MessageNumber code, HapetDiagnosticSeverity severity)
        {
            Parameters = parameters;
            Code = code;
            Severity = severity;
        }

        public required ImmutableArray<object> Parameters
        {
            get; init;
        }

        public IXmlMessage Message
        {
            get
            {
                var message = MessageCode.Get(Code);
                if (message == null)
                {
                    HapetAssert.Assert(false);
                    return null;
                }
                return message;
            }
        }

        public required MessageNumber Code
        {
            get; init;
        }

        public required HapetDiagnosticSeverity Severity
        {
            get; init;
        }
    }
}
