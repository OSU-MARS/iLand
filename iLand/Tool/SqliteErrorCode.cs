namespace iLand.Tool
{
    internal enum SqliteErrorCode
    {
        OK = 0,
        Error = 1,
        Internal = 2,
        PermissionDenied = 3,
        Abort = 4,
        Busy = 5,
        Locked = 6,
        Nomem = 7,
        Readonly = 8,
        Interrupt = 9,
        IOError = 10,
        Corrupt = 11,
        NotFound = 12,
        Full = 13,
        CantOpen = 14,
        LockProtocol = 15,
        Empty = 16,
        Schema = 17,
        TooBig = 18,
        ConstraintViolation = 19,
        DataTypeMismatch = 20,
        LibraryMisuse = 21,
        OSFeatureNotSupportedOnHost = 22,
        AuthorizationDenied = 23,
        BindParameterOutOfRange = 25,
        NotDatabaseFile = 26,
        Notice = 27,
        Warning = 28,
        RowReady = 100,
        ExecutionDone = 101
    }
}
