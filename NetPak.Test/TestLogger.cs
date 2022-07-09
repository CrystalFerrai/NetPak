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
	/// Console logger specialized for logging test results
	/// </summary>
	internal class TestLogger : ConsoleLogger
	{
		private MessageType mMessageType = MessageType.Default;

		/// <summary>
		/// Log a generic test pass message
		/// </summary>
		public void LogSuccess()
		{
			mMessageType = MessageType.Success;
			Log(LogLevel.Important, "[SUCCESS] Test passed");
		}

		/// <summary>
		/// Log a message related to a test pass
		/// </summary>
		public void LogSuccess(string message)
		{
			mMessageType = MessageType.Success;
			Log(LogLevel.Important, $"[SUCCESS] {message}");
		}

		/// <summary>
		/// Log a message related to skipping a test
		/// </summary>
		public void LogSkipping(string message)
		{
			mMessageType = MessageType.Skipping;
			Log(LogLevel.Important, $"[SKIPPING] {message}");
		}

		/// <summary>
		/// Log a message related to a test fail
		/// </summary>
		public void LogFailure(string message)
		{
			mMessageType = MessageType.Failure;
			Log(LogLevel.Important, $"[FAILED] {message}");
		}

		protected override void OnPreLog(LogLevel level, string message)
		{
			base.OnPreLog(level, message);

			switch (mMessageType)
			{
				case MessageType.Success:
					Console.ForegroundColor = ConsoleColor.Green;
					break;
				case MessageType.Skipping:
					Console.ForegroundColor = ConsoleColor.Yellow;
					break;
				case MessageType.Failure:
					Console.ForegroundColor = ConsoleColor.Red;
					break;
			}
		}

		protected override void OnPostLog(LogLevel level, string message)
		{
			base.OnPostLog(level, message);
			mMessageType = MessageType.Default;
		}

		private enum MessageType
		{
			Default,
			Success,
			Skipping,
			Failure
		}
	}
}
