using Xunit;
using Auktionshuset.Application.Admin.Lots.Images;

namespace Auktionshuset.Tests;

public class LotImageValidatorTests
{
    /// <summary>
    /// Verifies that each supported image type is recognised from its header bytes.
    /// </summary>
    [Theory]
    [InlineData("jpeg", ".jpg")]
    [InlineData("png", ".png")]
    [InlineData("webp", ".webp")]
    public void TryValidate_WithSupportedImage_ReturnsExtension(string kind, string expectedExtension)
    {
        byte[] bytes = ImageBytes(kind);

        bool accepted = LotImageValidator.TryValidate(new MemoryStream(bytes), bytes.Length, out string? extension);

        Assert.True(accepted);
        Assert.Equal(expectedExtension, extension);
    }

    /// <summary>
    /// Verifies that a file that is not an image is rejected even when its name claims otherwise.
    /// </summary>
    [Theory]
    [InlineData("not an image at all")]
    [InlineData("GIF89a")]
    [InlineData("")]
    public void TryValidate_WithUnsupportedContent_IsRejected(string content)
    {
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(content);

        bool accepted = LotImageValidator.TryValidate(new MemoryStream(bytes), bytes.Length, out string? extension);

        Assert.False(accepted);
        Assert.Equal(string.Empty, extension);
    }

    /// <summary>
    /// Verifies that a file larger than the limit is rejected.
    /// </summary>
    [Fact]
    public void TryValidate_WithTooLargeFile_IsRejected()
    {
        byte[] bytes = ImageBytes("png");
        long size = LotImageValidator.MaxSizeInBytes + 1;

        bool accepted = LotImageValidator.TryValidate(new MemoryStream(bytes), size, out string? extension);

        Assert.False(accepted);
        Assert.Equal(string.Empty, extension);
    }

    /// <summary>
    /// Verifies that an empty upload is rejected.
    /// </summary>
    [Fact]
    public void TryValidate_WithEmptyFile_IsRejected()
    {
        bool accepted = LotImageValidator.TryValidate(new MemoryStream(), 0, out string? extension);

        Assert.False(accepted);
        Assert.Equal(string.Empty, extension);
    }

    /// <summary>
    /// Verifies that a stream that cannot seek is rejected, since the header cannot be inspected safely.
    /// </summary>
    [Fact]
    public void TryValidate_WithNonSeekableStream_IsRejected()
    {
        byte[] bytes = ImageBytes("jpeg");

        bool accepted = LotImageValidator.TryValidate(new NonSeekableStream(bytes), bytes.Length, out string? extension);

        Assert.False(accepted);
        Assert.Equal(string.Empty, extension);
    }

    /// <summary>
    /// Verifies that validation leaves the stream at the start, so it can be copied afterwards.
    /// </summary>
    [Fact]
    public void TryValidate_WithSupportedImage_RewindsStream()
    {
        byte[] bytes = ImageBytes("png");
        using var stream = new MemoryStream(bytes);

        LotImageValidator.TryValidate(stream, bytes.Length, out _);

        Assert.Equal(0, stream.Position);
    }

    /// <summary>
    /// Verifies that the size limit is the agreed 5 MB.
    /// </summary>
    [Fact]
    public void MaxSizeInBytes_IsFiveMegabytes()
    {
        Assert.Equal(5 * 1024 * 1024, LotImageValidator.MaxSizeInBytes);
    }

    /// <summary>
    /// Builds a minimal byte sequence whose header identifies the requested image type.
    /// </summary>
    internal static byte[] ImageBytes(string kind) => kind switch
    {
        "jpeg" => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01],
        "png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D],
        "webp" =>
        [
            0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00,
            0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown image kind.")
    };

    /// <summary>
    /// A read-only stream that cannot seek, used to verify the validator's guards.
    /// </summary>
    private sealed class NonSeekableStream(byte[] content) : Stream
    {
        private readonly MemoryStream inner = new(content);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
    }
}
