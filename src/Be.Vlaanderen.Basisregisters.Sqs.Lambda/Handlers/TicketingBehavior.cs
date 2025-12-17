using System;

namespace Be.Vlaanderen.Basisregisters.Sqs.Lambda.Handlers;

[Flags]
public enum TicketingBehavior
{
    None = 0,
    Pending = 1,
    Complete = 2,

    All = Pending | Complete
}
