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

using System.Text;

namespace NetPak.Internal
{
	/// <summary>
	/// Contains metadata about pak file contents, stored at the end of a pak file.
	/// </summary>
	internal class PakInfo
	{
		private const uint PakMagic = 0x5A6F12E1;

		private const int HeaderOffset = -221; // Varies based on Pak version and Unreal version

		private const int CompressionNameLength = 32;

		private readonly List<CompressionMethod> mCompressionMethods;

		public PakVersion Version { get; set; }

		public short SubVersion { get; set; }

		public long IndexOffset { get; set; }

		public long IndexSize { get; set; }

		public byte[] IndexHash { get; set; } = new byte[20];

		public bool EncryptedIndex { get; private set; }

		public Guid EncryptionKeyGuid { get; private set; }

		public IReadOnlyList<CompressionMethod> CompressionMethods => mCompressionMethods;

		public PakInfo(CompressionMethod compression = CompressionMethod.None)
		{
			mCompressionMethods = new() { CompressionMethod.None };
			if (compression != CompressionMethod.None) mCompressionMethods.Add(compression);
		}

		public static PakInfo Load(BinaryReader reader)
		{
			// Reference: IPlatformFilePak.h - FPakInfo::Serialize

			if (reader.BaseStream.Length + HeaderOffset < 0)
			{
				throw new PakSerializerException("Stream not long enough to be a pak file");
			}
			reader.BaseStream.Seek(HeaderOffset, SeekOrigin.End);

			PakInfo info = new PakInfo();
			info.EncryptionKeyGuid = new Guid(reader.ReadBytes(16));
			info.EncryptedIndex = reader.ReadByte() != 0;
			if (info.EncryptedIndex) throw new NotSupportedException("Pak encryption not supported");

			uint magic = reader.ReadUInt32();
			if (magic != PakMagic) throw new PakSerializerException("Pak format not readable");

			info.Version = (PakVersion)reader.ReadInt16();
			info.SubVersion = reader.ReadInt16();

			if (info.Version < PakVersion.Fnv64BugFix) throw new NotSupportedException("Pak file version older than what is currently supported");

			info.IndexOffset = reader.ReadInt64();
			info.IndexSize = reader.ReadInt64();
			reader.BaseStream.Read(info.IndexHash);

			for (int i = 0; i < 5; ++i)
			{
				byte[] methodBytes = reader.ReadBytes(CompressionNameLength);
				string methodName = Encoding.ASCII.GetString(methodBytes).Trim('\0');
				if (string.IsNullOrEmpty(methodName)) continue;

				if (Enum.TryParse(methodName, true, out CompressionMethod method))
				{
					info.mCompressionMethods.Add(method);
				}
				else
				{
					throw new NotSupportedException($"Unrecognized compression method {methodName}");
				}
			}

			return info;
		}

		public void Save(BinaryWriter writer)
		{
			// Assuming correct write position of incoming stream

			writer.Write(EncryptionKeyGuid.ToByteArray());
			writer.Write(EncryptedIndex ? (byte)1 : (byte)0);
			writer.Write(PakMagic);

			writer.Write((short)Version);
			writer.Write(SubVersion);

			writer.Write(IndexOffset);
			writer.Write(IndexSize);
			writer.Write(IndexHash);

			byte[] emptyMethod = new byte[CompressionNameLength];
			for (int i = 1; i <= 5; ++i)
			{
				if (i < CompressionMethods.Count)
				{
					string methodName = CompressionMethods[i].ToString().PadRight(CompressionNameLength, '\0');
					writer.Write(Encoding.ASCII.GetBytes(methodName));
				}
				else
				{
					writer.Write(emptyMethod);
				}
			}
		}

		public void AddCompressionMethod(CompressionMethod method)
		{
			if (!mCompressionMethods.Contains(method)) mCompressionMethods.Add(method);
		}
	}

	internal enum PakVersion : short
	{
		Initial = 1,
		NoTimestamps = 2,
		CompressionEncryption = 3,
		IndexEncryption = 4,
		RelativeChunkOffsets = 5,
		DeleteRecords = 6,
		EncryptionKeyGuid = 7,
		FNameBasedCompressionMethod = 8,
		FrozenIndex = 9,
		PathHashIndex = 10,
		Fnv64BugFix = 11,

		Last,
		Invalid,
		Latest = Last - 1
	}
}
