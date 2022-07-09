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
	/// Extensions methods for binary readers and writers
	/// </summary>
	internal static class BinaryIOExtensions
	{
		/// <summary>
		/// Reads an FString
		/// </summary>
		/// <exception cref="FormatException">Invalid FString at current reader position</exception>
		public static FString? ReadFString(this BinaryReader reader, ulong hashSeed = 0)
		{
			var length = reader.ReadInt32();
			switch (length)
			{
				case 0:
					return null;
				case 1:
					reader.ReadByte();
					return FString.Empty;
				default:
					if (length < 0)
					{
						length = -length * 2;
						if (length > 131072 || length > reader.BaseStream.Length - reader.BaseStream.Position) throw new FormatException("Invalid FString");
						byte[] data = reader.ReadBytes(length);
						return new FString(Encoding.Unicode.GetString(data, 0, data.Length - 2), Encoding.Unicode, hashSeed);
					}
					else
					{
						if (length > 131072 || length > reader.BaseStream.Length - reader.BaseStream.Position) throw new FormatException("Invalid FString");
						byte[] data = reader.ReadBytes(length);
						return new FString(Encoding.ASCII.GetString(data, 0, data.Length - 1), Encoding.ASCII, hashSeed);
					}
			}
		}

		/// <summary>
		/// Writes an FString
		/// </summary>
		public static void WriteFString(this BinaryWriter writer, FString value)
		{
			if (value is null || value.Value is null)
			{
				writer.Write(0);
				return;
			}

			int len = value.Length + 1;
			if (value.Encoding.GetByteCount("A") > 1) len = -len;
			writer.Write(len);

			byte[] data = value.Encoding.GetBytes(value.Value);
			if (data.Length > 0)
			{
				writer.Write(data);
			}
			writer.Write(value.Encoding.GetBytes("\0"));
		}
	}
}
