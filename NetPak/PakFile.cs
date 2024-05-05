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

using NetPak.Internal;
using System.Security.Cryptography;
using System.Text;

namespace NetPak
{
	/// <summary>
	/// Represents an Unreal Engine content pak file
	/// </summary>
	public class PakFile : IDisposable
	{
		private const string RootMountPoint = "../../../";

		private Stream? mReadStream;

		private PakInfo? mInfo;

		private FString? mMountPoint;

		private string? mRelativeMountPoint;

		private ulong mPathHashSeed;

		private readonly OrderedDictionary<FString, PakEntry> mEntries;

		/// <summary>
		/// Gets the runtime entry mount point.
		/// </summary>
		/// <remarks>
		/// This cannot be changed on an existing file, only when creating a new one.
		/// </remarks>
		public FString MountPoint => mMountPoint!;

		/// <summary>
		/// Gets the compression method used in the file.
		/// </summary>
		/// <remarks>
		/// If the file uses different compression methods for different entries, this will only return one of them. This API is likely to change in the
		/// future to better support multiple compression types.
		/// </remarks>
		public CompressionMethod CompressionMethod => mInfo?.CompressionMethods.Last() ?? CompressionMethod.None;

		/// <summary>
		/// Gets the relative paths of all of the entries in this pak file.
		/// </summary>
		public IReadOnlyList<FString> Entries => mEntries.Keys;

		private PakFile()
		{
			mEntries = new OrderedDictionary<FString, PakEntry>();
		}

		/// <summary>
		/// Dispose this instance
		/// </summary>
		public void Dispose()
		{
			GC.SuppressFinalize(this);

			if (mReadStream != null)
			{
				mReadStream.Dispose();
				mReadStream = null;
			}
		}

		~PakFile()
		{
			Dispose();
		}

		/// <summary>
		/// Create a new pak file in memory. It will not be written to disk until <see cref="Save"/> or <see cref="SaveTo"/> is called
		/// </summary>
		/// <param name="fileName">
		/// The name of the pak file. This is not used for saving the file, only as a seed for hashing entry paths. Can technically be any string.
		/// Using the file name is only a convention following what UnrealPak does.
		/// </param>
		/// <param name="mountPoint">
		/// The runtime mount point for entries. Needs to be correct for the game that will be loading the pak file. A typical mount point looks
		/// like "../../../MyGame/". It is recommended that you check the mount point in a pak file the game already uses.
		/// </param>
		/// <param name="compression">
		/// Optional compression method to compress entries with. Currently, this method will be used to compress all entries. In the future,
		/// this might be improved to only compress entries where the size savings is significant, similar to what UnrealPak does.
		/// </param>
		/// <returns>The created pak file</returns>
		public static PakFile Create(FString fileName, FString mountPoint, CompressionMethod compression = CompressionMethod.None)
		{
			PakFile instance = new();
			instance.mMountPoint = mountPoint;
			instance.mRelativeMountPoint = mountPoint.Value.StartsWith(RootMountPoint) ? mountPoint[RootMountPoint.Length..] : mountPoint;
			instance.mPathHashSeed = Crc.StrCrc32(new FString(instance.PathGetFileName(fileName).Value.ToLowerInvariant(), fileName.Encoding));
			instance.mInfo = new PakInfo(compression)
			{
				Version = PakVersion.Latest,
				SubVersion = 0
			};
			return instance;
		}

		/// <summary>
		/// Mounts an existing pak file and reads its index.
		/// </summary>
		/// <param name="path">The path to the file to mount.</param>
		/// <remarks>
		/// The mounted file will remain open until this instance is disposed or saved. Data for individual entries will be loaded as requested.
		/// </remarks>
		/// <returns>The mounted pak file</returns>
		public static PakFile Mount(string path)
		{
			PakFile instance = new();
			instance.mReadStream = File.OpenRead(path);
			try
			{
				instance.ReadMetadata();
			}
			catch
			{
				instance.Dispose();
				throw;
			}
			return instance;
		}

		/// <summary>
		/// Mounts an existing pak file and reads its index.
		/// </summary>
		/// <param name="stream">A stream containing the pak file</param>
		/// <remarks>
		/// The passed in stream will be used by this instance for its lifetime. Do not dispose the stream before disposing the instance.
		/// </remarks>
		/// <returns>The mounted pak file</returns>
		public static PakFile Mount(Stream stream)
		{
			PakFile instance = new();
			instance.mReadStream = stream;
			try
			{
				instance.ReadMetadata();
			}
			catch
			{
				instance.Dispose();
				throw;
			}
			return instance;
		}

		/// <summary>
		/// Saves this pak file to a location on disk
		/// </summary>
		/// <param name="path">The path to wher ethe file should be saved</param>
		/// <remarks>
		/// The caller is responsible for ensuring the target directory exists.
		/// </remarks>
		public void Save(string path)
		{
			using FileStream file = File.Create(path);
			SaveTo(file);
		}

		/// <summary>
		/// Save this pak file to a stream
		/// </summary>
		/// <param name="stream">The stream to write to</param>
		public void SaveTo(Stream stream)
		{
			foreach (PakEntry entry in mEntries.Values)
			{
				if (mReadStream != null && entry.Data == null)
				{
					entry.LoadData(mReadStream);
				}
				if (entry.Data == null) throw new PakSerializerException($"Pak file serialization failed. Entry \"{entry.Path}\" has no data.");
			}

			// Now that all data has been loaded, close the reader (if it exists) as it is no longer needed and will be a problem if we are writing
			// to the same target the reader is reading from.
			mReadStream?.Dispose();
			mReadStream = null;

			foreach (PakEntry entry in mEntries.Values)
			{
				entry.SaveData(stream, stream.Position);
			}

			WriteMetadata(stream);
		}

		/// <summary>
		/// Adds a new entry to this pak file
		/// </summary>
		/// <param name="path">The path for the entry. Must be unique within this file.</param>
		/// <param name="data">The entry's content</param>
		public void AddEntry(FString path, ReadOnlySpan<byte> data)
		{
			path = new FString(path.Value, path.Encoding, mPathHashSeed);
			FString entryPath = path;
			if (entryPath.Value.StartsWith(RootMountPoint)) entryPath = entryPath[RootMountPoint.Length..];
			if (!string.IsNullOrEmpty(mRelativeMountPoint) && entryPath.Value.StartsWith(mRelativeMountPoint)) entryPath = entryPath[mRelativeMountPoint.Length..];

			PakEntry entry = PakEntry.Create(entryPath, mInfo!.Version, mInfo!.CompressionMethods.Last());
			entry.SetData(data);
			mEntries.Add(path, entry);
		}

		/// <summary>
		/// Removes an entry from this pak file
		/// </summary>
		/// <param name="path">The path of the entry to remove</param>
		public void RemoveEntry(FString path)
		{
			path = new FString(path.Value, path.Encoding, mPathHashSeed);
			mEntries.Remove(path);
		}

		/// <summary>
		/// Returns whether entry witht he specified path is present in this pak file
		/// </summary>
		/// <param name="path">The path to check</param>
		public bool HasEntry(FString path)
		{
			path = new FString(path.Value, path.Encoding, mPathHashSeed);
			return FindEntry(path) != null;
		}

		/// <summary>
		/// Reads the data for an entry, loading it from disk if necessary.
		/// </summary>
		/// <param name="path">the path of the entry to read</param>
		/// <param name="data">The data that was read</param>
		/// <returns>True of the entry exists, else false</returns>
		public bool ReadEntryData(FString path, out ReadOnlySpan<byte> data)
		{
			path = new FString(path.Value, path.Encoding, mPathHashSeed);

			data = null;

			PakEntry? entry = FindEntry(path);
			if (entry == null) return false;

			if (mReadStream != null && entry.Data == null)
			{
				entry.LoadData(mReadStream);
			}
			data = entry.Data;

			return true;
		}

		/// <summary>
		/// Reads all entries associated with a spefific asset, loading them from disk if necessy.
		/// </summary>
		/// <param name="path">The path to the primary asset entry.</param>
		/// <param name="data">Outputs the data for the primary asset entry</param>
		/// <param name="exportPath">Outputs the path to asset's export entry, if it exists. Otherwise outputs null.</param>
		/// <param name="exportData">Outputs the contents of asset's export entry, if it exists. Otherwise outputs null.</param>
		/// <param name="bulkPath">Outputs the path to asset's bulk data entry, if it exists. Otherwise outputs null.</param>
		/// <param name="bulkData">Outputs the contents of the asset's bulk data entry, if it exists. Otherwise outputs null.</param>
		/// <returns>True of the entry exists, else false</returns>
		public bool GetAssetData(FString path, out ReadOnlySpan<byte> data, out FString? exportPath, out ReadOnlySpan<byte> exportData, out FString? bulkPath, out ReadOnlySpan<byte> bulkData)
		{
			path = new FString(path.Value, path.Encoding, mPathHashSeed);

			string extension = Path.GetExtension(path);
			if (extension.Equals(".uexp", StringComparison.InvariantCultureIgnoreCase) ||
				extension.Equals(".ubulk", StringComparison.InvariantCultureIgnoreCase) ||
				extension.Equals(".uptnl", StringComparison.InvariantCultureIgnoreCase))
			{
				throw new ArgumentException("Requesting data for a bulk file is invalid. Request the corresponding asset file or use the GetEntryData method instead.", nameof(path));
			}

			exportPath = null;
			exportData = null;
			bulkPath = null;
			bulkData = null;

			if (!ReadEntryData(path, out data)) return false;

			FString checkExportPath = new FString(Path.ChangeExtension(path, ".uexp"), path.Encoding, mPathHashSeed);
			if (ReadEntryData(checkExportPath, out exportData)) exportPath = checkExportPath;

			FString checkBulkPath = new FString(Path.ChangeExtension(path, ".ubulk"), path.Encoding, mPathHashSeed);
			if (FindEntry(checkBulkPath) == null) checkBulkPath = new FString(Path.ChangeExtension(path, ".uptnl"), path.Encoding, mPathHashSeed);
			if (ReadEntryData(checkBulkPath, out bulkData)) bulkPath = checkBulkPath;

			return true;
		}

		/// <summary>
		/// Overwrites the content of an entry. The entry must exist.
		/// </summary>
		/// <param name="path">The path to the entry</param>
		/// <param name="data">The new content for the entry.</param>
		public void WriteEntryData(FString path, ReadOnlySpan<byte> data)
		{
			path = new FString(path.Value, path.Encoding, mPathHashSeed);

			if (!mEntries.TryGetValue(path, out PakEntry? entry)) throw new InvalidOperationException("Cannot set data on an entry that does not exist.");

			entry.SetData(data);
		}

		private void ReadMetadata()
		{
			// Reference: IPlatformFilePak.cpp - FPakFile::Initialize and FPakFile::LoadIndexInternal

			using BinaryReader reader = new BinaryReader(mReadStream!, Encoding.ASCII, true);

			mInfo = PakInfo.Load(reader);
			reader.BaseStream.Position = mInfo.IndexOffset;

			mMountPoint = reader.ReadFString() ?? throw new PakSerializerException("Could not read mount point from pak file");
			if (mMountPoint.Value.StartsWith(RootMountPoint))
			{
				mRelativeMountPoint = mMountPoint[RootMountPoint.Length..];
			}
			else if (Path.IsPathRooted(mMountPoint))
			{
				mRelativeMountPoint = string.Empty;
			}
			else
			{
				mRelativeMountPoint = mMountPoint;
			}

			int numEntries = reader.ReadInt32();

			// Path hash index header
			mPathHashSeed = reader.ReadUInt64();
			int hasPathHashIndex = reader.ReadInt32();
			long pathHashIndexOffset = 0;
			if (hasPathHashIndex == 1)
			{
				pathHashIndexOffset = reader.ReadInt64();
				long pathHashIndexSize = reader.ReadInt64();
				byte[] pathHashIndexHash = reader.ReadBytes(20);
			}

			// Directory index header
			int hasFullDirectoryIndex = reader.ReadInt32();
			if (hasFullDirectoryIndex != 1) throw new NotSupportedException("NetPak does not currently support pak files without a full directory index");
			long fullDirectoryIndexOffset = reader.ReadInt64();
			long fullDirectoryIndexSize = reader.ReadInt64();
			byte[] directoryIndexHash = reader.ReadBytes(20);

			int encodedEntriesSize = reader.ReadInt32();
			byte[] encodedEntries = reader.ReadBytes(encodedEntriesSize);
			using MemoryStream pakEntryStream = new(encodedEntries, false);

			int unencodedEntriesCount = reader.ReadInt32();
			if (unencodedEntriesCount != 0) throw new NotSupportedException("NetPak does not currently support pak files with unencoded file index entries");

			// The following code block reads the path hash index and directory index. These maps do not seem to be useful to us for now, so skipping this block.
			// In most cases seen so far, a pak file does not contain a directory index here, and in cases where it does, it is not a complete index of all the
			// files. The later "full directory index" seems to always have everything.
#if false
			// Path hash index
			Dictionary<ulong, int>? pathHashes = null;
			Dictionary<FString, Dictionary<FString, PakEntry>>? directoryMap = null;
			if (hasPathHashIndex == 1)
			{
				if (reader.BaseStream.Position != pathHashIndexOffset) throw new PakSerializerException("Unexpected metadata in pak file");

				int hashCount = reader.ReadInt32();
				pathHashes = new(hashCount);
				for (int i = 0; i < hashCount; ++i)
				{
					ulong hash = reader.ReadUInt64();
					int index = reader.ReadInt32();
					pathHashes.Add(hash, index);
				}

				int directoryCount = reader.ReadInt32();
				directoryMap = new(directoryCount);
				Dictionary<FString, PakEntry> currentDirectory;
				for (int dirIndex = 0; (dirIndex < directoryCount); ++dirIndex)
				{
					FString directoryName = reader.ReadFString(mPathHashSeed)!;
					int contentCount = reader.ReadInt32();
					currentDirectory = new Dictionary<FString, PakEntry>(contentCount);
					directoryMap.Add(directoryName, currentDirectory);
					for (int fileIndex = 0; fileIndex < contentCount; ++fileIndex)
					{
						FString fileName = reader.ReadFString(mPathHashSeed)!;
						FString path = PathCombine(directoryName, fileName);

						int offset = reader.ReadInt32();
						if (offset == int.MinValue) continue;

						pakEntryStream.Seek(offset, SeekOrigin.Begin);
						PakEntry entry = new PakEntry(path);
						entry.LoadMeta(pakEntryStream, mInfo.Version);

						currentDirectory.Add(fileName, entry);
					}
				}
			}
#else
			reader.BaseStream.Seek(fullDirectoryIndexOffset, SeekOrigin.Begin);
#endif

			// Full directory index
			if (reader.BaseStream.Position != fullDirectoryIndexOffset) throw new PakSerializerException("Unexpected metadata in pak file");

			int fullDirectoryCount = reader.ReadInt32();
			for (int fileIndex = 0; fileIndex < fullDirectoryCount; ++fileIndex)
			{
				FString directoryName = reader.ReadFString(mPathHashSeed)!;
				bool appendDir = directoryName != "/";
				int directoryContentCount = reader.ReadInt32();
				for (int contentIndex = 0; contentIndex < directoryContentCount; ++contentIndex)
				{
					FString fileName = reader.ReadFString(mPathHashSeed)!;
					FString path = appendDir ? PathCombine(directoryName, fileName) : fileName;

					int offset = reader.ReadInt32();
					if (offset == int.MinValue) continue;

					pakEntryStream.Seek(offset, SeekOrigin.Begin);
					PakEntry entry = PakEntry.FromMeta(path, pakEntryStream, mInfo);

					if (!string.IsNullOrEmpty(mRelativeMountPoint)) path = PathCombine((FString)mRelativeMountPoint, path);
					mEntries.Add(path, entry);
				}
			}
		}

		private void WriteMetadata(Stream stream)
		{
			IReadOnlyList<PakEntry> orderedEntries = mEntries.Values;

			using SHA1 sha1 = SHA1.Create();

			// Key = Directory path, Value = List of files in the directory - each as file name and data offset
			Dictionary<FString, List<Tuple<FString, int>>> directoryMap = new()
			{
				{ new FString("/", mPathHashSeed), new List<Tuple<FString, int>>() }
			};

			byte[] encodedEntries;
			using (MemoryStream entriesStream = new())
			{
				for (int i = 0; i < orderedEntries.Count; ++i)
				{
					FString dirPath = PathGetDirectory(orderedEntries[i].Path);
					FString fileName = PathGetFileName(orderedEntries[i].Path);
					List<Tuple<FString, int>>? dir;
					if (!directoryMap.TryGetValue(dirPath, out dir))
					{
						// Using a stack to reverse order such that nested directories each appear after their parent directory
						Stack<FString> parentDirs = new Stack<FString>();
						for (FString currentDir = PathGetDirectory(dirPath); currentDir.Length > 1; currentDir = PathGetDirectory(currentDir))
						{
							if (directoryMap.ContainsKey(currentDir)) break;
							parentDirs.Push(currentDir);
						}

						while (parentDirs.Count > 0)
						{
							directoryMap.Add(parentDirs.Pop(), new List<Tuple<FString, int>>());
						}

						dir = new List<Tuple<FString, int>>();
						directoryMap.Add(dirPath, dir);
					}
					dir.Add(new Tuple<FString, int>(fileName, (int)entriesStream.Position));

					orderedEntries[i].SaveMeta(entriesStream);
				}

				encodedEntries = entriesStream.ToArray();
			}

			long pathHashHeaderOffset, directoryHeaderOffset;

			using BinaryWriter writer = new BinaryWriter(stream);

			// Primary index
			{
				mInfo!.IndexOffset = stream.Position;

				writer.WriteFString(mMountPoint!);
				writer.Write(orderedEntries.Count);

				writer.Write(mPathHashSeed);
				writer.Write(1); // Has path hash index

				// Block out space for path hash index header
				pathHashHeaderOffset = stream.Position;
				writer.Write(new byte[36]);

				writer.Write(1); // Has full directory index

				// Block out space for directory index header
				directoryHeaderOffset = stream.Position;
				writer.Write(new byte[36]);

				writer.Write(encodedEntries.Length);
				writer.Write(encodedEntries);

				writer.Write(0); // Unencoded entries count

				mInfo!.IndexSize = stream.Position - mInfo!.IndexOffset;
			}

			// Path hash index
			long pathHashIndexOffset = stream.Position;
			{
				writer.Write(orderedEntries.Count);
				for (int i = 0; i < orderedEntries.Count; ++i)
				{
					writer.Write(orderedEntries[i].Path.GetFullHash());
					writer.Write(i);
				}
				writer.Write(0); // Path hash directory count
			}

			// Full directory index
			long directoryIndexOffset = stream.Position;
			{
				writer.Write(directoryMap.Count);
				foreach (var pair in directoryMap)
				{
					writer.WriteFString(pair.Key);
					writer.Write(pair.Value.Count);
					foreach (var entry in pair.Value)
					{
						writer.WriteFString(entry.Item1);
						writer.Write(entry.Item2);
					}
				}
			}
			long directoryIndexEndOffset = stream.Position;

			// Path hash index header
			{
				byte[] pathHashIndexBytes = new byte[directoryIndexOffset - pathHashIndexOffset];
				stream.Seek(pathHashIndexOffset, SeekOrigin.Begin);
				stream.Read(pathHashIndexBytes);

				stream.Seek(pathHashHeaderOffset, SeekOrigin.Begin);
				writer.Write(pathHashIndexOffset);
				writer.Write(directoryIndexOffset - pathHashIndexOffset);
				writer.Write(sha1.ComputeHash(pathHashIndexBytes));
			}

			// Directory index header
			{
				byte[] directoryIndexBytes = new byte[directoryIndexEndOffset - directoryIndexOffset];
				stream.Seek(directoryIndexOffset, SeekOrigin.Begin);
				stream.Read(directoryIndexBytes);

				stream.Seek(directoryHeaderOffset, SeekOrigin.Begin);
				writer.Write(directoryIndexOffset);
				writer.Write(directoryIndexEndOffset - directoryIndexOffset);
				writer.Write(sha1.ComputeHash(directoryIndexBytes));
			}

			// Primary index hash
			{
				byte[] indexHeader = new byte[mInfo!.IndexSize];
				stream.Seek(mInfo!.IndexOffset, SeekOrigin.Begin);
				stream.Read(indexHeader);
				mInfo.IndexHash = sha1.ComputeHash(indexHeader);
			}

			stream.Seek(0, SeekOrigin.End);
			mInfo!.Save(writer);
		}

		private PakEntry? FindEntry(FString path)
		{
			if (mEntries.TryGetValue(path, out PakEntry? entry)) return entry;
			if (mMountPoint == null) return null;

			string root = mMountPoint;
			if (path.Value.StartsWith(root))
			{
				path = new FString(path.Value[root.Length..], path.Encoding, mPathHashSeed);
				if (mEntries.TryGetValue(path, out entry)) return entry;
			}

			if (string.IsNullOrEmpty(mRelativeMountPoint)) return null;

			root = mRelativeMountPoint;
			if (path.Value.StartsWith(root))
			{
				path = new FString(path.Value[root.Length..], path.Encoding, mPathHashSeed);
				if (mEntries.TryGetValue(path, out entry)) return entry;
			}

			return null;
		}

		private FString PathCombine(params FString[] parts)
		{
			if (parts.Length == 0) return FString.Empty;

			StringBuilder builder = new(parts[0]);
			for (int i = 1; i < parts.Length; ++i)
			{
				FString part = parts[i];

				if (builder[builder.Length - 1] != '/')
				{
					builder.Append('/');
				}

				if (part.Value.StartsWith('/'))
				{
					builder.Append(part.Value.Substring(1));
				}
				else
				{
					builder.Append(part);
				}
			}

			return new FString(builder.ToString(), parts[0].Encoding, mPathHashSeed);
		}

		private FString PathGetDirectory(FString path)
		{
			string dir = path.Value;
			if (dir.Equals("/")) return new FString("/", path.Encoding);
			if (dir.StartsWith(RootMountPoint))
			{
				dir = dir[RootMountPoint.Length..];
				if (dir.Length == 0) return new FString("/", path.Encoding);
			}
			if (!string.IsNullOrEmpty(mRelativeMountPoint) && dir.StartsWith(mRelativeMountPoint))
			{
				dir = dir[mRelativeMountPoint.Length..];
			}

			int lastSlash = dir.LastIndexOf('/');
			if (lastSlash == path.Length - 1) lastSlash = dir.LastIndexOf('/', lastSlash - 1);

			if (lastSlash < 0) return new FString("/", path.Encoding);

			dir = dir[0..(lastSlash + 1)];

			if (mMountPoint!.Length > 0 && dir.StartsWith(mMountPoint!))
			{
				dir = "/" + dir[mMountPoint!.Length..];
			}
			return new FString(dir, path.Encoding, mPathHashSeed);
		}

		private FString PathGetFileName(FString path)
		{
			string file = path.Value;

			int lastSlash = file.LastIndexOf('/');
			if (lastSlash < 0) return new FString(path);

			return new FString(file[(lastSlash + 1)..], path.Encoding, mPathHashSeed);
		}
	}
}
