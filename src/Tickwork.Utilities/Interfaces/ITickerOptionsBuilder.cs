using System;

namespace Tickwork.Utilities.Interfaces;

internal interface ITickerOptionsBuilder
{
    internal TimeSpan TimeOutChecker { get; }
}