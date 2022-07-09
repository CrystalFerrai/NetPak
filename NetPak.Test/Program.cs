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
	/// Console app for testing the NetPak library
	/// </summary>
	internal class Program
	{
		/// <summary>
		/// Entry point
		/// </summary>
		private static int Main(string[] args)
		{
			TestLogger logger = new TestLogger();

			if (args.Length != 1)
			{
				logger.Log(LogLevel.Important, "Usage: NetPak.Test [test data directory]");
				return 0;
			}

			string pakDirectory;
			try
			{
				pakDirectory = Path.GetFullPath(args[0]);
			}
			catch (Exception ex)
			{
				logger.Log(LogLevel.Fatal, $"Error locating test data directory \"{args[0]}\". [{ex.GetType().FullName}] {ex.Message}");
				return 1;
			}
			if (!Directory.Exists(pakDirectory))
			{
				logger.Log(LogLevel.Fatal, $"Error locating test data directory \"{args[0]}\".");
				return 1;
			}

			TestRunner runner = new TestRunner(logger);
			if (!runner.Run(pakDirectory))
			{
				return 1;
			}

			if (System.Diagnostics.Debugger.IsAttached)
			{
				Console.ReadKey(true);
			}

			return 0;
		}
	}
}
