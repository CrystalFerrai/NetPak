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

using System.Runtime.InteropServices;

namespace NetPak.Test
{
	/// <summary>
	/// Utility for binary comparing files
	/// </summary>
	internal static class BinaryCompare
	{
		/// <summary>
		/// Fast binary file comparison
		/// </summary>
		/// <returns>
		///   0  Files are not identical
		///   1  Files are identical
		///  -1  An error occured while trying to read files
		/// </returns>
		[DllImport("BinaryCompare.dll")]
		public static extern int CompareFiles([MarshalAs(UnmanagedType.LPWStr)] string aPath, [MarshalAs(UnmanagedType.LPWStr)] string bPath);
	}
}
