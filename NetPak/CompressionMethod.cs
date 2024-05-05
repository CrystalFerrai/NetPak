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

namespace NetPak
{
	/// <summary>
	/// Compresison methods supported by Unreal Engine.
	/// </summary>
	/// <remarks>
	/// Enum order must match list of strings stored in FPakInfo::CompressionMethods
	/// from IPlatformFilePak.h.
	/// </remarks>
	public enum CompressionMethod
	{
		None,
		Zlib,
		Gzip,
		Custom,
		Oodle,
		LZ4,
		Unknown
	}
}
