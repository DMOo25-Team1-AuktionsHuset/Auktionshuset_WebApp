using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Abstraction.Auction
{
    public sealed class AuctionStateConflictException(string message) : Exception(message);

}
