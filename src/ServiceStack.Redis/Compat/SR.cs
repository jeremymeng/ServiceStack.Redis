#if DNXCORE50
// Temporary workaround for the missing BufferedStream in .Net Core.  It is being added back in RC2.
// This class just makes the code build and run, with English strings for the resource strings with the following key.
public static class SR
{
    public static string ArgumentNull_Buffer { get; } = "Buffer cannot be null.";

    public static string ArgumentOutOfRange_MustBePositive { get; } = "'{0}' must be greater than zero.";

    public static string ArgumentOutOfRange_NeedNonNegNum { get; } = "Non-negative number required.";

    public static string InvalidOperation_CannotSetStreamSizeCannotWrite { get; } = "Cannot set the size of this stream because it cannot be written to.";

    public static string NotSupported_CannotWriteToBufferedStreamIfReadBufferCannotBeFlushed { get; } = "Cannot write to a BufferedStream while the read buffer is not empty if the underlying stream is not seekable. Ensure that the stream underlying this BufferedStream can seek or avoid interleaving read and write operations on this BufferedStream.";

    public static string Argument_InvalidOffLen { get; } = "Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.";

    public static string NotSupported_UnseekableStream { get; } = "Stream does not support seeking.";

    public static string NotSupported_UnreadableStream { get; } = "Stream does not support reading.";

    public static string NotSupported_UnwritableStream { get; } = "Stream does not support writing.";

    public static string ObjectDisposed_StreamClosed { get; } = "Cannot access a closed Stream.";

    public static string Format(string formatString, params string[] args)
    {
        return string.Format(formatString, args);
    }
    //
    //public static string NotSupported_UnseekableStream { get; } = "Stream does not support seeking.";
    //
    //public static string ArgumentOutOfRange_MustBePositive { get; } = "'{0}' must be greater than zero.";
    //
    //public static string InvalidOperation_CannotSetStreamSizeCannotWrite { get; } = "Cannot set the size of this stream because it cannot be written to.";
    //
    //public static string NotSupported_CannotWriteToBufferedStreamIfReadBufferCannotBeFlushed { get; } = "Cannot write to a BufferedStream while the read buffer is not empty if the underlying stream is not seekable. Ensure that the stream underlying this BufferedStream can seek or avoid interleaving read and write operations on this BufferedStream.";

}
#endif