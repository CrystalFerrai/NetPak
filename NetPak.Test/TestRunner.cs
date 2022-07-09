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

namespace NetPak.Test
{
	/// <summary>
	/// Main class for running tests on the NetPak library
	/// </summary>
	internal class TestRunner
	{
		private readonly TestLogger mLogger;

		public TestRunner(TestLogger logger)
		{
			mLogger = logger;
		}

		/// <summary>
		/// Run all tests
		/// </summary>
		/// <param name="testDataDirectory">Path to the directory containing data to use when testing</param>
		public bool Run(string testDataDirectory)
		{
			bool allPassed = true;

			string inPaksDirectory = Path.Combine(testDataDirectory, "Paks\\Input");
			string outPaksDirectory = Path.Combine(testDataDirectory, "Paks\\Output");
			string outDataDirectory = Path.Combine(testDataDirectory, "OutContent");

			mLogger.Log(LogLevel.Information, $"Searching \"{inPaksDirectory}\" for pak files to test...");

			string[] pakFilePaths;
			try
			{
				pakFilePaths = Directory.GetFiles(inPaksDirectory, "*.pak", SearchOption.TopDirectoryOnly);
			}
			catch (Exception ex)
			{
				mLogger.Log(LogLevel.Error, $"Error getting files from directory \"{inPaksDirectory}\". [{ex.GetType().FullName}] {ex.Message}");
				return false;
			}

			if (pakFilePaths.Length == 0)
			{
				mLogger.Log(LogLevel.Error, $"No pak files located in directory \"{inPaksDirectory}\"");
				return false;
			}

			mLogger.Log(LogLevel.Information, $"Found {pakFilePaths.Length} files.");

			try
			{
				Directory.CreateDirectory(outPaksDirectory);
			}
			catch (Exception ex)
			{
				mLogger.Log(LogLevel.Error, $"Error creating output directory within \"{testDataDirectory}\". [{ex.GetType().FullName}] {ex.Message}");
				return false;
			}

			// TODO: Switch to a real unit test framework at some point
			int testCount = 0;
			int testSkips = 0;
			int testFails = 0;

			foreach (string pakFilePath in pakFilePaths)
			{
				FString pakFileName = (FString)Path.GetFileName(pakFilePath);
				string outPakFilePath = Path.Combine(outPaksDirectory, Path.GetFileName(pakFilePath));

				// Mount file
				++testCount;
				mLogger.Log(LogLevel.Information, $"Attempting to mount file {pakFileName}...");

				PakFile? inFile = null;
				try
				{
					inFile = PakFile.Mount(pakFilePath);
					mLogger.LogSuccess();
				}
				catch (Exception ex)
				{
					++testFails;
					mLogger.LogFailure($"Mount file failed. [{ex.GetType().FullName}] {ex.Message}");
				}

				// Create new pak and copy data to it
				++testCount;
				mLogger.Log(LogLevel.Information, $"Creating a new pak file and copying entries from {pakFileName}...");

				if (inFile != null)
				{
					try
					{
						using PakFile outFile = PakFile.Create(pakFileName, inFile.MountPoint, inFile.CompressionMethod);
						foreach (FString entry in inFile.Entries)
						{
							inFile.ReadEntryData(entry, out ReadOnlySpan<byte> data);
							outFile.AddEntry(entry, data);
						}
						outFile.Save(outPakFilePath);

						mLogger.LogSuccess();
					}
					catch (Exception ex)
					{
						++testFails;
						mLogger.LogFailure($"[{ex.GetType().FullName}] {ex.Message}");
					}
					finally
					{
						inFile.Dispose();
						inFile = null;
					}
				}
				else
				{
					++testSkips;
					mLogger.LogSkipping("Test skipped because of previous failure.");
				}

				// Read output file back in, extract files, and compare against originals
				++testCount;
				mLogger.Log(LogLevel.Information, $"Extracting and verifying files from new {pakFileName}...");

				try
				{
					using (PakFile reloadPakFile = PakFile.Mount(outPakFilePath))
					{
						int previousFails = testFails;
						foreach (FString entry in reloadPakFile.Entries)
						{
							if (!reloadPakFile.ReadEntryData(entry, out var data))
							{
								++testFails;
								mLogger.LogFailure($"Failed to read entry {entry}");
								break;
							}

							string outPath = entry;
							if (outPath.StartsWith("TestGame/")) outPath = outPath["TestGame/".Length..];
							if (outPath.StartsWith("Content/")) outPath = outPath["Content/".Length..];

							string inPath = Path.Combine(testDataDirectory, "Content", outPath);
							outPath = Path.Combine(outDataDirectory, outPath);

							Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
							using (FileStream outFile = File.Create(outPath))
							{
								outFile.Write(data);
							}

							int test = BinaryCompare.CompareFiles(inPath, outPath);
							if (test != 1)
							{
								++testFails;
								mLogger.LogFailure($"Input and output files do not match for {Path.GetFileName(inPath)}");
								break;
							}
						}
						if (previousFails == testFails)	mLogger.LogSuccess();
					}
				}
				catch (Exception ex)
				{
					++testFails;
					mLogger.LogFailure($"[{ex.GetType().FullName}] {ex.Message}");
				}

				Directory.Delete(outDataDirectory, true);
			}

			mLogger.Log(LogLevel.Important, $"\nTEST SUMMARY\n{testCount} tests attempted.");
			if (testFails == 0 && testSkips == 0) mLogger.Log(LogLevel.Important, "All tests passed.");
			else
			{
				mLogger.Log(LogLevel.Important, $"{testCount - testFails - testSkips} passed.");
				LogLevel level = testFails > 0 ? LogLevel.Error : LogLevel.Warning;
				if (testFails > 0) mLogger.Log(LogLevel.Error, $"{testFails} failed.");
				if (testSkips > 0) mLogger.Log(LogLevel.Warning, $"{testSkips} skipped.");
			}

			return allPassed;
		}
	}
}
