using System;
using System.Collections.Generic;
using Mono.Debugging.Client;
using MonoDevelop.Core;
using MonoDevelop.Core.Assemblies;
using MonoDevelop.Core.Execution;
using MonoDevelop.Debugger;

namespace VSCodeDebugger
{
	public class VSCodeDebuggerEngine : DebuggerEngineBackend
	{
		static VSCodeDebuggerEngine()
		{
			DebuggerLoggingService.CustomLogger = new MDLogger();
		}

		public override bool CanDebugCommand(ExecutionCommand cmd)
		{
			var dotnetCmd = cmd as DotNetExecutionCommand;
			if (dotnetCmd == null)
				return false;
			var fxId = Runtime.SystemAssemblyService.GetTargetFrameworkForAssembly(null, dotnetCmd.Command);

			return fxId.Identifier == ".NETCoreApp";
		}

		public override bool IsDefaultDebugger(ExecutionCommand cmd)
		{
			return true;
		}

		public static bool CanDebugRuntime(TargetRuntime runtime)
		{
			return true;
		}

		public override DebuggerStartInfo CreateDebuggerStartInfo(ExecutionCommand c)
		{
			var cmd = (DotNetExecutionCommand)c;
			var runtime = (MonoTargetRuntime)cmd.TargetRuntime;
			var dsi = new DebuggerStartInfo {
				Command = cmd.Command,
				Arguments = cmd.Arguments,
				WorkingDirectory = cmd.WorkingDirectory
			};

			foreach (KeyValuePair<string, string> var in cmd.EnvironmentVariables)
				dsi.EnvironmentVariables[var.Key] = var.Value;

			return dsi;
		}

		public override DebuggerSession CreateSession()
		{
			return new VSCodeDebuggerSession();
		}

		class MDLogger : ICustomLogger
		{
			public string GetNewDebuggerLogFilename()
			{
				if (PropertyService.Get("MonoDevelop.Debugger.DebuggingService.DebuggerLogging", false)) {
					string filename;
					var logWriter = LoggingService.CreateLogFile("Debugger", out filename);
					logWriter.Dispose();
					return filename;
				} else {
					return null;
				}
			}

			public void LogError(string message, Exception ex)
			{
				LoggingService.LogError(message, ex);
			}

			public void LogAndShowException(string message, Exception ex)
			{
				MonoDevelop.Ide.MessageService.ShowError(message, ex);
			}

			public void LogMessage(string messageFormat, params object[] args)
			{
				LoggingService.LogInfo(messageFormat, args);
			}
		}
	}
}

