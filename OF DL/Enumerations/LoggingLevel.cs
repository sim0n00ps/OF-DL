using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Enumerations
{
    public enum LoggingLevel
    {
        //
        // Summary:
        //     Anything and everything you might want to know about a running block of code.
        Verbose,
        //
        // Summary:
        //     Internal system events that aren't necessarily observable from the outside.
        Debug,
        //
        // Summary:
        //     The lifeblood of operational intelligence - things happen.
        Information,
        //
        // Summary:
        //     Service is degraded or endangered.
        Warning,
        //
        // Summary:
        //     Functionality is unavailable, invariants are broken or data is lost.
        Error,
        //
        // Summary:
        //     If you have a pager, it goes off when one of these occurs.
        Fatal
    }
}
