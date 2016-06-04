using System;
using Mono.Debugging.Client;
using System.Diagnostics;
using System.Text;
using VSCodeDebug;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Mono.Debugging.Backend;

namespace VSCodeDebugger
{
	public class VSCodeDebuggerSession : DebuggerSession
	{
		long currentThreadId;
		protected override void OnAttachToProcess(long processId)
		{
			throw new NotImplementedException();
		}

		protected override void OnContinue()
		{
			protocolClient.SendRequestAsync(new ContinueRequest(new ContinueRequestArguments {
				threadId = currentThreadId
			})).Wait();
		}

		protected override void OnDetach()
		{
			protocolClient.SendRequestAsync(new DisconnectRequest()).Wait();
		}

		protected override void OnEnableBreakEvent(BreakEventInfo eventInfo, bool enable)
		{
			throw new NotImplementedException();
		}

		protected override void OnExit()
		{
			//protocolClient.SendRequestAsync();
		}

		protected override void OnFinish()
		{
			protocolClient.SendRequestAsync(new StepOutRequest(new StepOutRequestArguments {
				threadId = currentThreadId
			})).Wait();
		}

		ProcessInfo[] processInfo = new ProcessInfo[] { new ProcessInfo(1, "debugee") };
		protected override ProcessInfo[] OnGetProcesses()
		{
			return processInfo;
		}

		protected override Backtrace OnGetThreadBacktrace(long processId, long threadId)
		{
			return GetThreadBacktrace(threadId);
		}

		protected override ThreadInfo[] OnGetThreads(long processId)
		{
			var threadsResponse = protocolClient.SendRequestAsync(new ThreadsRequest()).Result;
			var threads = new ThreadInfo[threadsResponse.threads.Length];
			for (int i = 0; i < threads.Length; i++) {
				threads[i] = new ThreadInfo(processId,
										  threadsResponse.threads[i].id,
										  threadsResponse.threads[i].name,
										  "not implemented");
			}
			return threads;
		}

		Dictionary<Breakpoint, BreakEventInfo> breakpoints = new Dictionary<Breakpoint, BreakEventInfo>();

		protected override BreakEventInfo OnInsertBreakEvent(BreakEvent breakEvent)
		{
			if (breakEvent is Breakpoint) {
				var breakEventInfo = new BreakEventInfo();
				breakpoints.Add((Breakpoint)breakEvent, breakEventInfo);
				UpdateBreakpoints();
				return breakEventInfo;
			}
			throw new NotImplementedException(breakEvent.GetType().FullName);
		}

		protected override void OnNextInstruction()
		{
			protocolClient.SendRequestAsync(new NextRequest(new NextRequestArguments {
				threadId = currentThreadId
			})).Wait();
		}

		protected override void OnNextLine()
		{
			protocolClient.SendRequestAsync(new NextRequest(new NextRequestArguments {
				threadId = currentThreadId
			})).Wait();
		}

		protected override void OnRemoveBreakEvent(BreakEventInfo eventInfo)
		{
			//breakpoints.Remove(breakpoints.Single(b => b.Value == eventInfo).Key);
			//UpdateBreakpoints(eventInfo.BreakEvent);
		}

		Process debugAgentProcess;
		ProtocolClient protocolClient;

		class VSCodeDebuggerBacktrace : IBacktrace
		{
			long threadId;
			VSCodeDebuggerSession vsCodeDebuggerSession;
			VSCodeDebug.StackFrame[] frames;
			Mono.Debugging.Client.StackFrame[] stackFrames;

			public VSCodeDebuggerBacktrace(VSCodeDebuggerSession vsCodeDebuggerSession, long threadId)
			{
				this.vsCodeDebuggerSession = vsCodeDebuggerSession;
				this.threadId = threadId;
				var body = vsCodeDebuggerSession.protocolClient.SendRequestAsync(new StackTraceRequest(new StackTraceArguments {
					threadId = threadId,
					startFrame = 0,
					levels = 20
				})).Result;
				frames = body.stackFrames;
			}

			public int FrameCount {
				get {
					return frames.Length;
				}
			}

			public AssemblyLine[] Disassemble(int frameIndex, int firstLine, int count)
			{
				throw new NotImplementedException();
			}

			public ObjectValue[] GetAllLocals(int frameIndex, EvaluationOptions options)
			{
				List<ObjectValue> results = new List<ObjectValue>();
				var scopeBody = vsCodeDebuggerSession.protocolClient.SendRequestAsync(new ScopesRequest(new ScopesArguments {
					frameId = frames[frameIndex].id
				})).Result;
				foreach (var variablesGroup in scopeBody.scopes) {
					var varibles = vsCodeDebuggerSession.protocolClient.SendRequestAsync(new VariablesRequest(new VariablesRequestArguments {
						variablesReference = variablesGroup.variablesReference
					})).Result;
					foreach (var variable in varibles.variables) {
						results.Add(ObjectValue.CreatePrimitive(null, new ObjectPath(variable.name), "unknown", new EvaluationResult(variable.value), ObjectValueFlags.None));
					}
				}
				return results.ToArray();
			}

			public ExceptionInfo GetException(int frameIndex, EvaluationOptions options)
			{
				throw new NotImplementedException();
			}

			public CompletionData GetExpressionCompletionData(int frameIndex, string exp)
			{
				throw new NotImplementedException();
			}

			class VSCodeObjectSource : IObjectValueSource
			{
				int variablesReference;
				VSCodeDebuggerSession vsCodeDebuggerSession;

				public VSCodeObjectSource(VSCodeDebuggerSession vsCodeDebuggerSession, int variablesReference)
				{
					this.vsCodeDebuggerSession = vsCodeDebuggerSession;
					this.variablesReference = variablesReference;
				}

				public ObjectValue[] GetChildren(ObjectPath path, int index, int count, EvaluationOptions options)
				{
					var children = vsCodeDebuggerSession.protocolClient.SendRequestAsync(new VariablesRequest(new VariablesRequestArguments {
						variablesReference = variablesReference
					})).Result.variables;
					return children.Select(c => VsCodeVariableToObjectValue(vsCodeDebuggerSession, c.name, c.value, c.variablesReference)).ToArray();
				}

				public object GetRawValue(ObjectPath path, EvaluationOptions options)
				{
					throw new NotImplementedException();
				}

				public ObjectValue GetValue(ObjectPath path, EvaluationOptions options)
				{
					throw new NotImplementedException();
				}

				public void SetRawValue(ObjectPath path, object value, EvaluationOptions options)
				{
					throw new NotImplementedException();
				}

				public EvaluationResult SetValue(ObjectPath path, string value, EvaluationOptions options)
				{
					throw new NotImplementedException();
				}
			}

			public ObjectValue[] GetExpressionValues(int frameIndex, string[] expressions, EvaluationOptions options)
			{
				var results = new List<ObjectValue>();
				foreach (var expr in expressions) {
					var responseBody = vsCodeDebuggerSession.protocolClient.SendRequestAsync(new EvaluateRequest(new EvaluateRequestArguments {
						expression = expr,
						frameId = frames[frameIndex].id
					})).Result;
					results.Add(VsCodeVariableToObjectValue(vsCodeDebuggerSession, expr, responseBody.result, responseBody.variablesReference));
				}
				return results.ToArray();
			}

			static ObjectValue VsCodeVariableToObjectValue(VSCodeDebuggerSession vsCodeDebuggerSession, string name, string value, int variablesReference)
			{
				if (variablesReference == 0)//This is some kind of primitive...
					return ObjectValue.CreatePrimitive(null, new ObjectPath(name), "unknown", new EvaluationResult(value), ObjectValueFlags.ReadOnly);
				else
					return ObjectValue.CreateObject(new VSCodeObjectSource(vsCodeDebuggerSession, variablesReference), new ObjectPath(name), "unknown", new EvaluationResult(value), ObjectValueFlags.ReadOnly, null);
			}

			public ObjectValue[] GetLocalVariables(int frameIndex, EvaluationOptions options)
			{
				throw new NotImplementedException();
			}

			public ObjectValue[] GetParameters(int frameIndex, EvaluationOptions options)
			{
				List<ObjectValue> results = new List<ObjectValue>();
				var scopeBody = vsCodeDebuggerSession.protocolClient.SendRequestAsync(new ScopesRequest(new ScopesArguments {
					frameId = frames[frameIndex].id
				})).Result;
				foreach (var variablesGroup in scopeBody.scopes) {
					var varibles = vsCodeDebuggerSession.protocolClient.SendRequestAsync(new VariablesRequest(new VariablesRequestArguments {
						variablesReference = variablesGroup.variablesReference
					})).Result;
					foreach (var variable in varibles.variables) {
						results.Add(ObjectValue.CreatePrimitive(null, new ObjectPath(variable.name), "unknown", new EvaluationResult(variable.value), ObjectValueFlags.None));
					}
				}
				return results.ToArray();
			}

			public Mono.Debugging.Client.StackFrame[] GetStackFrames(int firstIndex, int lastIndex)
			{
				if (stackFrames == null) {
					stackFrames = new Mono.Debugging.Client.StackFrame[Math.Min(lastIndex - firstIndex, frames.Length - firstIndex)];
					for (int i = firstIndex; i < stackFrames.Length + firstIndex; i++) {
						stackFrames[i] = new Mono.Debugging.Client.StackFrame(frames[i].id,
																			 new SourceLocation(
																			 frames[i].name,
																			 frames[i].source?.path,
																				 frames[i].line,
																				 frames[i].column,
																				 -1, -1),
																			  "C#");
					}
				}
				return stackFrames;
			}

			public ObjectValue GetThisReference(int frameIndex, EvaluationOptions options)
			{
				throw new NotImplementedException();
			}

			public ValidationResult ValidateExpression(int frameIndex, string expression, EvaluationOptions options)
			{
				throw new NotImplementedException();
			}
		}

		Backtrace GetThreadBacktrace(long threadId)
		{
			return new Backtrace(new VSCodeDebuggerBacktrace(this, threadId));
		}

		void HandleAction(VSCodeDebug.Event obj)
		{
			switch (obj.eventType) {
				case "initialized":
					//OnStarted();
					break;
				case "stopped":
					TargetEventArgs args;
					switch ((string)obj.body.reason) {
						case "breakpoint":
							args = new TargetEventArgs(TargetEventType.TargetHitBreakpoint);
							args.BreakEvent = breakpoints.Single(pair => pair.Key.FileName == (string)obj.body.source.path &&
																 pair.Key.Line == (int)obj.body.line).Key;
							break;
						case "step":
							args = new TargetEventArgs(TargetEventType.TargetStopped);
							break;
						default:
							throw new NotImplementedException((string)obj.body.reason);
					}
					currentThreadId = (long)obj.body.threadId;
					args.Process = OnGetProcesses()[0];
					args.Thread = GetThread(args.Process, (long)obj.body.threadId);
					args.Backtrace = GetThreadBacktrace((long)obj.body.threadId);

					OnTargetEvent(args);
					break;
			}
		}

		ThreadInfo GetThread(ProcessInfo process, long threadId)
		{
			foreach (var threadInfo in OnGetThreads(process.Id)) {
				if (threadInfo.Id == threadId)
					return threadInfo;
			}
			return null;
		}

		void UpdateBreakpoints()
		{
			var bks = breakpoints.Select(b => b.Key).OfType<Mono.Debugging.Client.Breakpoint>().GroupBy(b => b.FileName);
			foreach (var sourceFile in bks) {
				protocolClient.SendRequestAsync(new SetBreakpointsRequest(new SetBreakpointsRequestArguments {
					Source = new Source(sourceFile.Key),
					Breakpoints = sourceFile.Select(b => new SourceBreakpoint() {
						Line = b.Line,
						Column = b.Column,
						Condition = b.ConditionExpression
					}).ToList()
				}));
			}
		}

		void StartDebugAgent()
		{
			var startInfo = new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(typeof(VSCodeDebuggerSession).Assembly.Location), "CoreClrAdaptor", "OpenDebugAD7"));
			startInfo.RedirectStandardOutput = true;
			startInfo.RedirectStandardInput = true;
			startInfo.StandardOutputEncoding = Encoding.UTF8;
			startInfo.StandardOutputEncoding = Encoding.UTF8;
			startInfo.UseShellExecute = false;
			startInfo.EnvironmentVariables["PATH"] = "/usr/local/share/dotnet:" + Environment.GetEnvironmentVariable("PATH");
			debugAgentProcess = Process.Start(startInfo);
			protocolClient = new ProtocolClient();
			protocolClient.OnEvent += HandleAction;
			protocolClient.Start(debugAgentProcess.StandardOutput.BaseStream, debugAgentProcess.StandardInput.BaseStream)
						  .ContinueWith((task) => {
							  if (task.IsFaulted) {
								  Console.WriteLine(task.Exception);
							  }
						  });
			var initRequest = new InitializeRequest(new InitializeRequestArguments() {
				adapterID = "coreclr",
				linesStartAt1 = true,
				columnsStartAt1 = true,
				pathFormat = "path"
			});
			protocolClient.SendRequestAsync(initRequest).Wait();
		}

		protected override void OnRun(DebuggerStartInfo startInfo)
		{
			StartDebugAgent();
			var cwd = string.IsNullOrWhiteSpace(startInfo.WorkingDirectory) ? Path.GetDirectoryName(startInfo.Command) : startInfo.WorkingDirectory;
			var launchRequest = new LaunchRequest(new LaunchRequestArguments {
				Name = ".NET Core Launch (console)",
				Type = "coreclr",
				Request = "launch",
				PreLaunchTask = "build",
				Program = startInfo.Command,
				Args = startInfo.Arguments.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries),
				Cwd = cwd,
				NoDebug = false,
				StopAtEntry = false
			});
			var lal = protocolClient.SendRequestAsync(launchRequest).Result;
			OnStarted();
		}

		protected override void OnStarted(ThreadInfo t)
		{
			base.OnStarted(t);
			protocolClient.SendRequestAsync(new ConfigurationDoneRequest()).Wait();
		}

		protected override void OnSetActiveThread(long processId, long threadId)
		{
			currentThreadId = threadId;
		}

		protected override void OnStepInstruction()
		{
			protocolClient.SendRequestAsync(new StepInRequest(new StepInRequestArguments {
				threadId = currentThreadId
			})).Wait();
		}

		protected override void OnStepLine()
		{
			protocolClient.SendRequestAsync(new StepInRequest(new StepInRequestArguments {
				threadId = currentThreadId
			})).Wait();
		}

		protected override void OnStop()
		{
			protocolClient.SendRequestAsync(new PauseRequest(new PauseRequestArguments {
				threadId = currentThreadId
			})).Wait();
		}

		protected override void OnUpdateBreakEvent(BreakEventInfo eventInfo)
		{
			throw new NotImplementedException();
		}
	}
}

