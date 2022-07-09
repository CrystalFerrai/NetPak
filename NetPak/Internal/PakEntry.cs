// Copyright 2022 Crystal Ferrai
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

using System.Security.Cryptography;
using System.Text;

namespace NetPak.Internal
{
	/// <summary>
	/// Represents a single entry, or file, stored within a pak file
	/// </summary>
	internal class PakEntry
	{
		private readonly PakVersion mVersion;

		private PakCompressionBlock[]? mCompressedBlocks;

		private long mOffset;

		private CompressionMethod mCompressionMethod;

		private uint mCompressionBlockSize;

		private uint mCompressedSize;

		private uint mUncompressedSize;

		public FString Path { get; }

		public byte[]? Data { get; private set; }

		private PakEntry(FString path, PakVersion version)
		{
			Path = path;
			mVersion = version;
		}

		public static PakEntry Create(FString path, PakVersion version, CompressionMethod compression)
		{
			PakEntry instance = new PakEntry(path, version);
			instance.mCompressionMethod = compression;
			return instance;
		}

		public static PakEntry FromMeta(FString path, Stream stream, PakVersion version)
		{
			PakEntry instance = new PakEntry(path, version);
			instance.LoadMeta(stream);
			return instance;
		}

		public void SaveMeta(Stream stream)
		{
			if (Data == null) throw new InvalidOperationException("Data is null. Cannot save.");

			uint flags = 1u << 30 | 1u << 29; // Sizes 32 bit safe
			if (mOffset < uint.MaxValue) flags |= 1u << 31; // Offset 32 bit safe
			flags |= (uint)mCompressionMethod << 23;
			if (mCompressedBlocks?.Length > 0)
			{
				flags |= 0x3f; // Signifies compresison block size is present. Note: Older pak versions would put the block size here directly
				flags |= (uint)mCompressedBlocks.Length << 6;
			}

			using BinaryWriter writer = new(stream, Encoding.ASCII, true);

			writer.Write(flags);

			if (mCompressedBlocks?.Length > 0)
			{
				// Note: Older pak versions do not have this value here, but instead make it part of the flags
				writer.Write(mCompressionBlockSize);
			}

			{
				bool isOffset32BitSafe = (flags & 1 << 31) != 0;
				if (isOffset32BitSafe) writer.Write((uint)mOffset);
				else writer.Write(mOffset);
			}

			writer.Write(mUncompressedSize);

			if (mCompressedBlocks == null || mCompressedBlocks.Length == 0)
			{
				return;
			}

			writer.Write(mCompressedSize);

			if (mCompressedBlocks.Length < 2) return;

			bool isEncrypted = (flags & 1 << 22) != 0;

			long baseOffset = mVersion >= PakVersion.RelativeChunkOffsets ? 0 : mOffset;
			long blockOffset = baseOffset + GetSerializedSize();
			long blockAlignment = isEncrypted ? 16 : 1;
			foreach (PakCompressionBlock block in mCompressedBlocks)
			{
				writer.Write((int)(block.End - blockOffset));
				blockOffset += Align(block.End - block.Start, blockAlignment);
			}
		}

		internal void SetData(ReadOnlySpan<byte> data)
		{
			Data = data.ToArray();
			mUncompressedSize = (uint)Data.Length;

			if (mCompressionMethod == CompressionMethod.None)
			{
				mCompressionBlockSize = 0;
				mCompressedBlocks = null;
			}
			else
			{
				mCompressionBlockSize = Math.Min(mUncompressedSize, ushort.MaxValue);
				uint compressionBlockCount = mUncompressedSize / mCompressionBlockSize + (mUncompressedSize == mCompressionBlockSize ? 0u : 1u);
				mCompressedBlocks = new PakCompressionBlock[compressionBlockCount];
			}
		}

		public void LoadData(Stream stream)
		{
			// Skipping the header since we already got the same info from the index
			long headerSize = GetSerializedSize();
			stream.Seek(mOffset + headerSize, SeekOrigin.Begin);

			Data = new byte[mUncompressedSize];
			if (mCompressionMethod == CompressionMethod.None)
			{
				stream.Read(Data, 0, Data.Length);
			}
			else
			{
				int dataIndex = 0;
				foreach (PakCompressionBlock block in mCompressedBlocks!)
				{
					stream.Seek(mOffset + block.Start, SeekOrigin.Begin);
					dataIndex += Compressor.DecompressBlock(stream, Data, dataIndex, mCompressionMethod);
				}
			}
		}

		public void SaveData(Stream stream, long pakOffset)
		{
			if (Data == null) throw new InvalidOperationException("Cannot save data when no data is present");

			mOffset = pakOffset;

			using MemoryStream compressed = new();
			if (mCompressedBlocks != null)
			{
				int dataOffset = GetSerializedSize();
				for (int i = 0; i < mCompressedBlocks.Length; ++i)
				{
					int start = i * (int)mCompressionBlockSize;
					int size = Math.Min(Data.Length - start, (int)mCompressionBlockSize);

					mCompressedBlocks[i].Start = dataOffset + compressed.Position;
					Compressor.CompressBlock(Data, compressed, start, size, mCompressionMethod);
					mCompressedBlocks[i].End = dataOffset + compressed.Position;
				}
				compressed.Seek(0, SeekOrigin.Begin);
				mCompressedSize = (uint)compressed.Length;
			}
			else
			{
				mCompressedSize = mUncompressedSize;
			}

			using (BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII, true))
			{
				writer.Write(0L); // Offset (always 0. used to be different in older pak versions)
				writer.Write((long)mCompressedSize);
				writer.Write((long)mUncompressedSize);
				writer.Write((int)mCompressionMethod);

				using (SHA1 sha1 = SHA1.Create())
				{
					if (mCompressionMethod == CompressionMethod.None)
					{
						writer.Write(sha1.ComputeHash(Data));
					}
					else
					{
						writer.Write(sha1.ComputeHash(compressed));
						compressed.Seek(0, SeekOrigin.Begin);
					}
				}

				if (mCompressedBlocks != null)
				{
					writer.Write(mCompressedBlocks.Length);
					foreach (PakCompressionBlock block in mCompressedBlocks)
					{
						writer.Write(block.Start);
						writer.Write(block.End);
					}
				}

				writer.Write((byte)0);  // Flags. seems to always be 0
				writer.Write(mCompressionBlockSize);
			}

			if (mCompressionMethod == CompressionMethod.None)
			{
				stream.Write(Data);
			}
			else
			{
				compressed.CopyTo(stream);
			}
		}

		private void LoadMeta(Stream stream)
		{
			using BinaryReader reader = new BinaryReader(stream, Encoding.ASCII, true);

			// Reference: IPlatformFilePak.cpp - FPakFile::DecodePakEntry

			// Bit 31 = Offset 32-bit safe?
			// Bit 30 = Uncompressed size 32-bit safe?
			// Bit 29 = Size 32-bit safe?
			// Bits 28-23 = Compression method
			// Bit 22 = Encrypted
			// Bits 21-6 = Compression blocks count
			// Bits 5-0 = Compression block size, max value (0x3f) means block size stored separately
			uint flags = reader.ReadUInt32();

			mCompressionMethod = (CompressionMethod)(flags >> 23 & 0x3f);

			uint compressionBlockSize = 0;
			if ((flags & 0x3f) == 0x3f)
			{
				compressionBlockSize = reader.ReadUInt32();
			}
			else
			{
				compressionBlockSize = (flags & 0x3f) << 11;
			}

			{
				bool isOffset32BitSafe = (flags & 1 << 31) != 0;
				if (isOffset32BitSafe) mOffset = reader.ReadUInt32();
				else mOffset = reader.ReadInt64();
			}

			{
				bool isUncompressedSize32BitSafe = (flags & 1 << 30) != 0;
				if (!isUncompressedSize32BitSafe) throw new PakSerializerException("Entry in pak larger than maximum size supported by NetPak");
				mUncompressedSize = reader.ReadUInt32();
			}

			bool isEncrypted = (flags & 1 << 22) != 0;
			if (isEncrypted) throw new NotSupportedException("Pak encryption not currently supported");

			if (mCompressionMethod == CompressionMethod.None)
			{
				mCompressedSize = mUncompressedSize;
				return;
			}

			// If we get here, we have a compressed entry
			{
				bool isSize32BitSafe = (flags & 1 << 29) != 0;
				if (!isSize32BitSafe) throw new PakSerializerException("Entry in pak larger than maximum size supported by NetPak");
				mCompressedSize = reader.ReadUInt32();
			}

			uint compressionBlocksCount = flags >> 6 & 0xffff;
			if (compressionBlocksCount == 0)
			{
				return;
			}

			mCompressionBlockSize = compressionBlockSize > mUncompressedSize ? mUncompressedSize : compressionBlockSize;

			mCompressedBlocks = new PakCompressionBlock[compressionBlocksCount];

			long baseOffset = mVersion >= PakVersion.RelativeChunkOffsets ? 0 : mOffset;
			if (compressionBlocksCount == 1 && !isEncrypted)
			{
				mCompressedBlocks[0].Start = baseOffset + GetSerializedSize();
				mCompressedBlocks[0].End = mCompressedBlocks[0].Start + mCompressedSize;
			}
			else
			{
				long blockAlignment = isEncrypted ? 16 : 1;
				long blockOffset = baseOffset + GetSerializedSize();
				for (int blockIndex = 0; blockIndex < compressionBlocksCount; ++blockIndex)
				{
					mCompressedBlocks[blockIndex].Start = blockOffset;
					mCompressedBlocks[blockIndex].End = blockOffset + reader.ReadInt32();
					blockOffset += Align(mCompressedBlocks[blockIndex].End - mCompressedBlocks[blockIndex].Start, blockAlignment);
				}
			}
		}

		private int GetSerializedSize()
		{
			// Reference: IPlatformFilePak.h - FPakEntry::GetSerializedSize

			// Note: Pak version affects this method. Values are for the version we currently support.

			// sizeof(Offset) + sizeof(Size) + sizeof(UncompressedSize) + sizeof(Hash) + sizeof(CompressionMethodIndex) + sizeof(Flags) + sizeof(CompressionBlockSize)
			int size = 53;

			if (mCompressionMethod != CompressionMethod.None)
			{
				// sizeof(PakCompressedBlock) * CompressionBlocks.Count + sizeof(int32)
				size += 16 * mCompressedBlocks!.Length + 4;
			}

			return size;
		}

		private static long Align(long value, long alignment)
		{
			return value + alignment - 1 & ~(alignment - 1);
		}

		public override string ToString()
		{
			return Path;
		}

		private struct PakCompressionBlock
		{
			public long Start;

			public long End;

			public int Length => (int)(End - Start);
		}
	}
}
