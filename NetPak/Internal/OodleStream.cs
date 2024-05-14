// Copyright 2024 Crystal Ferrai
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using EpicGames.Compression;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;

namespace NetPak.Internal
{
	/// <summary>
	/// A stream for reading and writing data that is compressed using Epic Game's Oodle algorithm
	/// </summary>
	internal class OodleStream : Stream
	{
		private Stream? mStream;
		private long mPosition;

		private readonly CompressionMode mMode;
		private readonly OodleCompressorType mType;
		private readonly OodleCompressionLevel mLevel;

		private readonly bool mLeaveOpen;

		private byte[]? mDecompressedBuffer;
		private readonly long mDecompressedLength;

		public override bool CanSeek => mMode == CompressionMode.Decompress;

		public override bool CanRead
		{
			get
			{
				EnsureNotDisposed();
				return mMode == CompressionMode.Decompress && mStream.CanRead;
			}
		}

		public override bool CanWrite
		{
			get
			{
				EnsureNotDisposed();
				return mMode == CompressionMode.Compress && mStream.CanWrite;
			}
		}

		public override long Length
		{
			get
			{
				EnsureNotDisposed();
				
				if (CanSeek)
				{
					return mDecompressedLength;
				}

				throw new InvalidOperationException("Length only supported in Decompress mode");
			}
		}

		public override long Position
		{
			get
			{
				EnsureNotDisposed();

				if (CanSeek)
				{
					return mPosition;
				}

				throw new InvalidOperationException("Position only supported in Decompress mode");
			}
			set
			{
				EnsureNotDisposed();

				if (CanSeek)
				{
					mPosition = value;
				}

				throw new InvalidOperationException("Position only supported in Decompress mode");
			}
		}

		public Stream BaseStream
		{
			get
			{
				EnsureNotDisposed();
				return mStream;
			}
		}

		/// <summary>
		/// Creates an <see cref="OodleStream"/> in decompression read mode
		/// </summary>
		/// <param name="stream">The stream to read from</param>
		/// <param name="decompressedLength">The size of the data after decompression. Must be known ahead of time.</param>
		public OodleStream(Stream stream, long decompressedLength)
			: this(stream, CompressionMode.Decompress, OodleCompressorType.None, OodleCompressionLevel.None, decompressedLength, false)
		{
		}

		/// <summary>
		/// Creates an <see cref="OodleStream"/> in decompression read mode
		/// </summary>
		/// <param name="stream">The stream to read from</param>
		/// <param name="decompressedLength">The size of the data after decompression. Must be known ahead of time.</param>
		/// <param name="leaveOpen">Whether to leave the underlying stream open after closing this stream</param>
		public OodleStream(Stream stream, long decompressedLength, bool leaveOpen)
			: this(stream, CompressionMode.Decompress, OodleCompressorType.None, OodleCompressionLevel.None, decompressedLength, leaveOpen)
		{
		}

		/// <summary>
		/// Creates an <see cref="OodleStream"/> in compression write mode
		/// </summary>
		/// <param name="stream">The stream to write to</param>
		/// <param name="type">The compression type to use</param>
		public OodleStream(Stream stream, OodleCompressorType type)
			: this(stream, CompressionMode.Compress, type, OodleCompressionLevel.Normal, -1, false)
		{
		}

		/// <summary>
		/// Creates an <see cref="OodleStream"/> in compression write mode
		/// </summary>
		/// <param name="stream">The stream to write to</param>
		/// <param name="type">The compression type to use</param>
		/// <param name="level">The compression level</param>
		public OodleStream(Stream stream, OodleCompressorType type, OodleCompressionLevel level)
			: this(stream, CompressionMode.Compress, type, level, -1, false)
		{
		}

		/// <summary>
		/// Creates an <see cref="OodleStream"/> in compression write mode
		/// </summary>
		/// <param name="stream">The stream to write to</param>
		/// <param name="type">The compression type to use</param>
		/// <param name="leaveOpen">Whether to leave the underlying stream open after closing this stream</param>
		public OodleStream(Stream stream, OodleCompressorType type, bool leaveOpen)
			: this(stream, CompressionMode.Compress, type, OodleCompressionLevel.Normal, -1, leaveOpen)
		{
		}

		/// <summary>
		/// Creates an <see cref="OodleStream"/> in compression write mode
		/// </summary>
		/// <param name="stream">The stream to write to</param>
		/// <param name="type">The compression type to use</param>
		/// <param name="level">The compression level</param>
		/// <param name="leaveOpen">Whether to leave the underlying stream open after closing this stream</param>
		public OodleStream(Stream stream, OodleCompressorType type, OodleCompressionLevel level, bool leaveOpen)
			: this(stream, CompressionMode.Compress, type, level, -1, leaveOpen)
		{
		}

		private OodleStream(Stream stream, CompressionMode mode, OodleCompressorType type, OodleCompressionLevel level, long decompressedLength, bool leaveOpen)
		{
			if (stream is null) throw new ArgumentNullException(nameof(stream));

			mStream = stream;
			mMode = mode;
			mType = type;
			mLevel = level;
			mLeaveOpen = leaveOpen;
			mDecompressedLength = decompressedLength;

			switch (mMode)
			{
				case CompressionMode.Decompress:
					if (decompressedLength < 0) throw new ArgumentException("Must specify decompressed length for compression mode Decompress", nameof(decompressedLength));

					try
					{
						_ = stream.Length;
					}
					catch (NotSupportedException ex)
					{
						throw new ArgumentException("Input stream must support getting its length", ex);
					}

					break;
				case CompressionMode.Compress:
					break;
				default:
					throw new ArgumentException($"Invalid CompressionMode value: {mode}", nameof(mode));
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (mMode == CompressionMode.Compress) mStream?.Flush();
				if (!mLeaveOpen) mStream?.Dispose();
				mStream = null;
			}
			base.Dispose(disposing);
		}

		public override void Flush()
		{
			EnsureNotDisposed();
			EnsureCanWrite();

			mStream.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			EnsureNotDisposed();
			EnsureCanRead();
			EnsureDecompressedBuffer();

			if (mPosition >= mDecompressedBuffer.Length) throw new EndOfStreamException();

			if (count > mDecompressedBuffer.Length - mPosition)
			{
				count = (int)(mDecompressedLength - mPosition);
			}

			if (count > 0)
			{
				Array.Copy(mDecompressedBuffer, mPosition, buffer, offset, count);
				mPosition += count;
			}

			return count;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			EnsureNotDisposed();
			EnsureCanWrite();

			byte[] compressed = new byte[Oodle.MaximumOutputSize(mType, buffer.Length - offset)];
			int length = Oodle.Compress(mType, new ReadOnlySpan<byte>(buffer, offset, count), compressed, mLevel);
			mStream.Write(compressed, 0, length);
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			if (!CanSeek) throw new InvalidOperationException("Can only seek in Decompress mode");

			switch (origin)
			{
				case SeekOrigin.Begin:
					Position = offset;
					break;
				case SeekOrigin.Current:
					Position += offset;
					break;
				case SeekOrigin.End:
					Position = Length - offset;
					break;
			}
			return Position;
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		[MemberNotNull(nameof(mStream))]
		private void EnsureNotDisposed()
		{
			if (mStream == null) throw new ObjectDisposedException(nameof(OodleStream));
		}

		private void EnsureCanRead()
		{
			if (!CanRead) throw new InvalidOperationException("Stream cannot be read from.");
		}

		private void EnsureCanWrite()
		{
			if (!CanWrite) throw new InvalidOperationException("Stream cannot be written to.");
		}

		[MemberNotNull(nameof(mDecompressedBuffer))]
		private void EnsureDecompressedBuffer()
		{
			if (mDecompressedBuffer is not null) return;

			byte[] compressed = new byte[mStream!.Length];
			mStream.Read(compressed, 0, compressed.Length);

			mDecompressedBuffer = new byte[mDecompressedLength];
			Oodle.Decompress(compressed, mDecompressedBuffer);
		}
	}
}
