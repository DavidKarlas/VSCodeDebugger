using System;
using MonoDevelop.Components.Commands;
using System.Diagnostics;
using System.IO;
using Mono.Unix.Native;

namespace VSCodeDebugger
{
	public class StartupHandler : CommandHandler
	{
		protected override void Run()
		{
			var filesBasePath = Path.Combine(Path.GetDirectoryName(typeof(VSCodeDebuggerSession).Assembly.Location), "CoreClrAdaptor");
			var fileInfo = new Mono.Unix.UnixFileInfo(Path.Combine(filesBasePath, "OpenDebugAD7"));
			if ((fileInfo.FileAccessPermissions | Mono.Unix.FileAccessPermissions.OtherExecute) == Mono.Unix.FileAccessPermissions.OtherExecute)
				return;//We already set
			foreach (var file in Directory.GetFiles(filesBasePath, "*", SearchOption.AllDirectories)) {
				fileInfo = fileInfo = new Mono.Unix.UnixFileInfo(file);
				fileInfo.FileAccessPermissions = fileInfo.FileAccessPermissions | Mono.Unix.FileAccessPermissions.OtherExecute;
			}
		}
	}
}

