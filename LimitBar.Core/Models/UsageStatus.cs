namespace LimitBar.Core.Models;

public enum UsageStatus
{
    Available,
    CliNotInstalled,
    NotAuthenticated,
    UnsupportedVersion,
    ParseError,
    TimedOut,
    UnknownError
}
