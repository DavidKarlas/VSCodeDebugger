/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace VSCodeDebug
{
	public class ProtocolMessage
	{
		public int seq;
		public string type { get; }

		public ProtocolMessage(string typ)
		{
			type = typ;
		}

		public ProtocolMessage(string typ, int sq)
		{
			type = typ;
			seq = sq;
		}
	}

	public abstract class Request : ProtocolMessage
	{
		public Request()
			: base("request")
		{

		}
		[JsonIgnore]
		object objectBody;

		[JsonIgnore]
		public object ObjectBody {
			get {
				return objectBody;
			}

			set {
				objectBody = value;
				ValueSet();
			}
		}

		protected abstract void ValueSet();
		[JsonIgnore]
		string errorMessage;

		[JsonIgnore]
		public string ErrorMessage {
			get {
				return errorMessage;
			}

			set {
				errorMessage = value;
				ValueSet();
			}
		}

		public abstract Type GetResponseBodyType();
	}

	public class Request<RequestArguments, ResponseBody> : Request where ResponseBody : new()
	{
		[JsonProperty("arguments", NullValueHandling = NullValueHandling.Ignore)]
		public RequestArguments Arguments { get; set; }
		[JsonIgnore]
		public ResponseBody Response {
			get {
				return (ResponseBody)ObjectBody;
			}
		}

		public override Type GetResponseBodyType()
		{
			return typeof(ResponseBody);
		}

		protected override void ValueSet()
		{
			if (ErrorMessage != null)
				WaitingResponse.SetException(new Exception(ErrorMessage));
			else
				WaitingResponse.SetResult(Response);
		}

		[JsonIgnore]
		public TaskCompletionSource<ResponseBody> WaitingResponse { get; } = new TaskCompletionSource<ResponseBody>();

		public string command;

		public Request(string cmd, RequestArguments args)
		{
			command = cmd;
			Arguments = args;
		}
	}

	/** Arguments for "initialize" request. */
	public class InitializeRequestArguments
	{
		/** The ID of the debugger adapter. Used to select or verify debugger adapter. */
		public string adapterID { get; set; }
		/** If true all line numbers are 1-based (default). */
		public bool linesStartAt1 { get; set; }
		/** If true all column numbers are 1-based (default). */
		public bool columnsStartAt1 { get; set; }
		/** Determines in what format paths are specified. Possible values are 'path' or 'uri'. The default is 'path', which is the native format. */
		public string pathFormat { get; set; }
	}

	public class LaunchRequest : Request<LaunchRequestArguments, object>
	{
		public LaunchRequest(LaunchRequestArguments args)
		: base("launch", args)
		{

		}
	}

	public class LaunchRequestArguments
	{

		[JsonProperty("name")]
		public string Name { get; set; }

		[JsonProperty("type")]
		public string Type { get; set; }

		[JsonProperty("request")]
		public string Request { get; set; }

		[JsonProperty("preLaunchTask")]
		public string PreLaunchTask { get; set; }

		[JsonProperty("program")]
		public string Program { get; set; }

		[JsonProperty("args")]
		public IList<object> Args { get; set; }

		[JsonProperty("cwd")]
		public string Cwd { get; set; }

		[JsonProperty("stopAtEntry")]
		public bool StopAtEntry { get; set; }

		[JsonProperty("noDebug")]
		public bool NoDebug { get; set; }
	}

	/** Next request; value of command field is "next".
		The request starts the debuggee to run again for one step.
		penDebug will respond with a StoppedEvent (event type 'step') after running the step.
	*/
	public class NextRequest : Request<NextRequestArguments, object>
	{
		public NextRequest(NextRequestArguments args)
		: base("next", args)
		{

		}
	}
	/** Arguments for "next" request. */
	public class NextRequestArguments
	{
		/** Continue execution for this thread. */
		public long threadId { get; set; }
	}

	/** StepIn request; value of command field is "stepIn".
		The request starts the debuggee to run again for one step.
		The debug adapter will respond with a StoppedEvent (event type 'step') after running the step.
	*/
	public class StepInRequest : Request<StepInRequestArguments, object>
	{
		public StepInRequest(StepInRequestArguments args)
		: base("stepIn", args)
		{

		}
	}
	/** Arguments for "stepIn" request. */
	public class StepInRequestArguments
	{
		/** Continue execution for this thread. */
		public long threadId { get; set; }
	}
	/** StepOut request; value of command field is "stepOut".
		The request starts the debuggee to run again for one step.
		penDebug will respond with a StoppedEvent (event type 'step') after running the step.
	*/
	public class StepOutRequest : Request<StepOutRequestArguments, object>
	{
		public StepOutRequest(StepOutRequestArguments args)
		: base("stepOut", args)
		{

		}
	}
	/** Arguments for "stepIn" request. */
	public class StepOutRequestArguments
	{
		/** Continue execution for this thread. */
		public long threadId { get; set; }
	}
	/** Pause request; value of command field is "pause".
		The request suspenses the debuggee.
		penDebug will respond with a StoppedEvent (event type 'pause') after a successful 'pause' command.
	*/
	public class PauseRequest : Request<PauseRequestArguments, object>
	{
		public PauseRequest(PauseRequestArguments args)
		: base("pause", args)
		{

		}
	}
	/** Arguments for "pause" request. */
	public class PauseRequestArguments
	{
		/** Pause execution for this thread. */
		public long threadId { get; set; }
	}

	/** Continue request; value of command field is "continue".
		The request starts the debuggee to run again.
	*/
	public class ContinueRequest : Request<ContinueRequestArguments, ContinueResponse>
	{
		public ContinueRequest(ContinueRequestArguments args) :
		base("continue", args)
		{

		}
	}
	/** Arguments for "continue" request. */
	public class ContinueRequestArguments
	{
		/** Continue execution for the specified thread (if possible). If the backend cannot continue on a single thread but will continue on all threads, it should set the allThreadsContinued attribute in the response to true. */
		public long threadId { get; set; }
	}
	/** Response to "continue" request. */
	public class ContinueResponse
	{
		public bool? allThreadsContinued { get; set; }
	}

	/// <summary>
	/// Initialize request; value of command field is "initialize".
	/// </summary>
	public class InitializeRequest : Request<InitializeRequestArguments, Capabilities>
	{
		public InitializeRequest(InitializeRequestArguments args)
			: base("initialize", args)
		{

		}
	}

	public class ThreadsRequest : Request<object, ThreadsResponseBody>
	{
		public ThreadsRequest()
			: base("threads", null)
		{
		}
	}

	/** Evaluate request; value of command field is "evaluate".
	Evaluates the given expression in the context of the top most stack frame.
	The expression has access to any variables and arguments that are in scope.
*/
	public class EvaluateRequest : Request<EvaluateRequestArguments, EvaluateResponseBody>
	{
		public EvaluateRequest(EvaluateRequestArguments args)
		: base("evaluate", args)
		{
		}
	}
	/** Arguments for "evaluate" request. */
	public class EvaluateRequestArguments
	{
		/** The expression to evaluate. */
		public string expression { get; set; }
		/** Evaluate the expression in the scope of this stack frame. If not specified, the expression is evaluated in the global scope. */
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public long? frameId { get; set; }
		/** The context in which the evaluate request is run. Possible values are 'watch' if evaluate is run in a watch, 'repl' if run from the REPL console, or 'hover' if run from a data hover. */
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string context { get; set; }
	}

	public class SetBreakpointsRequest : Request<SetBreakpointsRequestArguments, SetBreakpointsResponseBody>
	{
		public SetBreakpointsRequest(SetBreakpointsRequestArguments args)
		: base("setBreakpoints", args)
		{

		}
	}

	/** SetExceptionBreakpoints request; value of command field is "setExceptionBreakpoints".
		Enable that the debuggee stops on exceptions with a StoppedEvent (event type 'exception').
	*/
	public class SetExceptionBreakpointsRequest : Request<SetExceptionBreakpointsArguments, object>
	{
		public SetExceptionBreakpointsRequest(SetExceptionBreakpointsArguments args)
			: base("setExceptionBreakpoints", args)
		{
		}
	}

	/** Arguments for "setExceptionBreakpoints" request. */
	public class SetExceptionBreakpointsArguments
	{
		/** Names of enabled exception breakpoints. */
		public string[] filters { get; set; }
	}

	/** Properties of a breakpoint passed to the setBreakpoints request.
	*/
	public class SourceBreakpoint
	{
		/** The source line of the breakpoint. */
		[JsonProperty("line")]
		public int Line { get; set; }
		/** An optional source column of the breakpoint. */
		[JsonProperty("column")]
		public int? Column { get; set; }
		/** An optional expression for conditional breakpoints. */
		[JsonProperty("condition", NullValueHandling = NullValueHandling.Ignore)]
		public string Condition { get; set; }
	}

	public class SetBreakpointsRequestArguments
	{
		[JsonProperty("source")]
		public Source Source { get; set; }

		[JsonProperty("breakpoints")]
		public IList<SourceBreakpoint> Breakpoints { get; set; }
	}

	/** ConfigurationDone request; value of command field is "configurationDone".
	The client of the debug protocol must send this request at the end of the sequence of configuration requests (which was started by the InitializedEvent)
*/
	public class ConfigurationDoneRequest : Request<object, object>
	{
		public ConfigurationDoneRequest()
			: base("configurationDone", null)
		{
		}
	}

	/** Disconnect request; value of command field is "disconnect".
	*/
	public class DisconnectRequest : Request<object, object>
	{
		public DisconnectRequest()
			: base("disconnect", null)
		{
		}
	}

	/** Scopes request; value of command field is "scopes".
	The request returns the variable scopes for a given stackframe ID.
*/
	public class ScopesRequest : Request<ScopesArguments, ScopesResponseBody>
	{
		public ScopesRequest(ScopesArguments args)
		: base("scopes", args)
		{

		}
	}

	/** Arguments for "scopes" request. */
	public class ScopesArguments
	{
		/** Retrieve the scopes for this stackframe. */
		public int frameId { get; set; }
	}

	/** Variables request; value of command field is "variables".
		Retrieves all children for the given variable reference.
	*/
	public class VariablesRequest : Request<VariablesRequestArguments, VariablesResponseBody>
	{
		public VariablesRequest(VariablesRequestArguments args)
			: base("variables", args)
		{

		}
	}
	/** Arguments for "variables" request. */
	public class VariablesRequestArguments
	{
		/** The Variable reference. */
		public int variablesReference { get; set; }
	}

	/** StackTrace request; value of command field is "stackTrace".
		The request returns a stacktrace from the current execution state.
	*/
	public class StackTraceRequest : Request<StackTraceArguments, StackTraceResponseBody>
	{
		public StackTraceRequest(StackTraceArguments args)
			: base("stackTrace", args)
		{
		}
	}

	/** Arguments for "stackTrace" request. */
	public class StackTraceArguments
	{
		/** Retrieve the stacktrace for this thread. */
		public long threadId;
		/** The index of the first frame to return; if omitted frames start at 0. */
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int? startFrame;
		/** The maximum number of frames to return. If levels is not specified or 0, all frames are returned. */
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int? levels;
	}

	/// <summary>
	/// Subclasses of ResponseBody are serialized as the body of a response.
	/// Don't change their instance variables since that will break the debug protocol.
	/// </summary>
	public class ResponseBody
	{
		// empty
	}

	public class Response : ProtocolMessage
	{
		public bool success { get; set; }
		public string message { get; set; }
		public int request_seq { get; }
		public string command { get; }
		public ResponseBody body { get; set; }

		public Response()
			: base("response")
		{
		}

		public void SetBody(ResponseBody bdy)
		{
			success = true;
			body = bdy;
		}

		public void SetErrorBody(string msg, ResponseBody bdy = null)
		{
			success = false;
			message = msg;
			body = bdy;
		}
	}

	public class Event : ProtocolMessage
	{
		[JsonProperty(PropertyName = "event")]
		public string eventType { get; set; }
		public dynamic body { get; set; }

		public Event(string type, dynamic bdy = null) : base("event")
		{
			eventType = type;
			body = bdy;
		}
	}

	// ---- Types -------------------------------------------------------------------------

	public class Message
	{
		public int id { get; }
		public string format { get; }
		public dynamic variables { get; }
		public dynamic showUser { get; }
		public dynamic sendTelemetry { get; }

		public Message(int id, string format, dynamic variables = null, bool user = true, bool telemetry = false)
		{
			this.id = id;
			this.format = format;
			this.variables = variables;
			this.showUser = user;
			this.sendTelemetry = telemetry;
		}
	}

	public class StackFrame
	{
		public int id { get; set; }
		public Source source { get; set; }
		public int line { get; set; }
		public int column { get; set; }
		public string name { get; set; }

		public StackFrame()
		{

		}

		public StackFrame(int id, string name, Source source, int line, int column)
		{
			this.id = id;
			this.name = name;
			this.source = source;
			this.line = line;
			this.column = column;
		}
	}

	public class Scope
	{
		public string name { get; set; }
		public int variablesReference { get; set; }
		public bool expensive { get; set; }

		public Scope()
		{

		}

		public Scope(string name, int variablesReference, bool expensive = false)
		{
			this.name = name;
			this.variablesReference = variablesReference;
			this.expensive = expensive;
		}
	}

	public class Variable
	{
		public string name { get; set; }
		public string value { get; set; }
		public int variablesReference { get; set; }
		public Variable()
		{

		}
		public Variable(string name, string value, int variablesReference = 0)
		{
			this.name = name;
			this.value = value;
			this.variablesReference = variablesReference;
		}
	}

	public class Thread
	{
		public int id { get; set; }
		public string name { get; set; }

		public Thread()
		{

		}

		public Thread(int id, string name)
		{
			this.id = id;
			if (name == null || name.Length == 0) {
				this.name = string.Format("Thread #{0}", id);
			} else {
				this.name = name;
			}
		}
	}

	public class Source
	{
		/// <summary>
		/// The short name of the source. Every source returned from the debug adapter has a name. When specifying a source to the debug adapter this name is optional.
		/// </summary>
		public string name { get; set; }
		/// <summary>
		/// The long (absolute) path of the source. It is not guaranteed that the source exists at this location.
		/// </summary>
		public string path { get; set; }
		/// <summary>
		/// If sourceReference > 0 the contents of the source can be retrieved through the SourceRequest. A sourceReference is only valid for a session, so it must not be used to persist a source.
		/// </summary>
		public int sourceReference { get; set; }
		/// <summary>
		/// The (optional) origin of this source: possible values "internal module", "inlined content from source map"
		/// </summary>
		[JsonProperty("origin", NullValueHandling = NullValueHandling.Ignore)]
		public string origin { get; set; }

		public Source()
		{

		}

		public Source(string name, string path, int sourceReference = 0)
		{
			this.name = name;
			this.path = path;
			this.sourceReference = sourceReference;
		}

		public Source(string path, int sourceReference = 0)
		{
			this.name = Path.GetFileName(path);
			this.path = path;
			this.sourceReference = sourceReference;
		}
	}

	public class BreakpointStatus
	{
		public bool verified { get; }
		public int line { get; }

		public BreakpointStatus(bool verified, int line)
		{
			this.verified = verified;
			this.line = line;
		}
	}

	// ---- Events -------------------------------------------------------------------------

	public class InitializedEvent : Event
	{
		public InitializedEvent()
			: base("initialized") { }
	}

	public class StoppedEvent : Event
	{
		public StoppedEvent(int tid, string reasn, string txt = null)
			: base("stopped", new {
				threadId = tid,
				reason = reasn,
				text = txt
			})
		{ }
	}

	public class ExitedEvent : Event
	{
		public ExitedEvent(int exCode)
			: base("exited", new { exitCode = exCode }) { }
	}

	public class TerminatedEvent : Event
	{
		public TerminatedEvent()
			: base("terminated") { }
	}

	public class ThreadEvent : Event
	{
		public ThreadEvent(string reasn, int tid)
			: base("thread", new {
				reason = reasn,
				threadId = tid
			})
		{ }
	}

	public class OutputEvent : Event
	{
		public OutputEvent(string cat, string outpt)
			: base("output", new {
				category = cat,
				output = outpt
			})
		{ }
	}

	// ---- Response -------------------------------------------------------------------------

	public class Capabilities : ResponseBody
	{
		public bool supportsConfigurationDoneRequest;
		public bool supportsFunctionBreakpoints;
		public bool supportsConditionalBreakpoints;
		public bool supportsEvaluateForHovers;
		public ExceptionBreakpointsFilter[] exceptionBreakpointFilters;
	}

	/** An ExceptionBreakpointsFilter is shown in the UI as an option for configuring how exceptions are dealt with. */
	public class ExceptionBreakpointsFilter
	{
		/** The internal ID of the filter. This value is passed to the setExceptionBreakpoints request. */
		[JsonProperty("filter")]
		public string Filter { get; set; }
		/** The name of the filter. This will be shown in the UI. */
		[JsonProperty("label")]
		public string Label { get; set; }
		/** Initial value of the filter. If not specified a value 'false' is assumed. */
		[JsonProperty("default", NullValueHandling = NullValueHandling.Ignore)]
		public bool? Default { get; set; }
	}

	public class ErrorResponseBody : ResponseBody
	{

		public Message error { get; }

		public ErrorResponseBody(Message error)
		{
			this.error = error;
		}
	}

	public class StackTraceResponseBody : ResponseBody
	{
		public StackFrame[] stackFrames { get; set; }
		public StackTraceResponseBody()
		{

		}

		public StackTraceResponseBody(List<StackFrame> frames)
		{
			if (frames == null)
				stackFrames = new StackFrame[0];
			else
				stackFrames = frames.ToArray<StackFrame>();
		}
	}

	public class ScopesResponseBody : ResponseBody
	{
		public Scope[] scopes { get; set; }

		public ScopesResponseBody()
		{

		}

		public ScopesResponseBody(List<Scope> scps)
		{
			if (scps == null)
				scopes = new Scope[0];
			else
				scopes = scps.ToArray<Scope>();
		}
	}

	public class VariablesResponseBody : ResponseBody
	{
		public Variable[] variables { get; set; }

		public VariablesResponseBody()
		{

		}

		public VariablesResponseBody(List<Variable> vars)
		{
			if (vars == null)
				variables = new Variable[0];
			else
				variables = vars.ToArray<Variable>();
		}
	}

	public class ThreadsResponseBody : ResponseBody
	{
		public Thread[] threads { get; set; }

		public ThreadsResponseBody()
		{

		}

		public ThreadsResponseBody(List<Thread> vars)
		{
			if (vars == null)
				threads = new Thread[0];
			else
				threads = vars.ToArray<Thread>();
		}
	}

	public class EvaluateResponseBody : ResponseBody
	{
		public string result { get; set; }
		public int variablesReference { get; set; }
	}

	public class SetBreakpointsResponseBody : ResponseBody
	{
		public BreakpointStatus[] breakpoints { get; set; }

		public SetBreakpointsResponseBody()
		: this(null)
		{
		}

		public SetBreakpointsResponseBody(List<BreakpointStatus> bpts)
		{
			if (bpts == null)
				breakpoints = new BreakpointStatus[0];
			else
				breakpoints = bpts.ToArray<BreakpointStatus>();
		}
	}


	///*
	//    * The ProtocolServer can be used to implement a server that uses the VSCode debug protocol.
	//    */
	//public abstract class ProtocolServer : ProtocolEndpoint
	//{
	//	protected abstract void DispatchRequest(string command, dynamic args, Response response);

	//	protected override void Dispatch(string message)
	//	{
	//		var request = JsonConvert.DeserializeObject<Request>(message);
	//		if (request != null && request.type == "request") {
	//			if (Trace.HasFlag(TraceLevel.Requests))
	//				Console.Error.WriteLine(string.Format("C {0}: {1}", request.command, JsonConvert.SerializeObject(request.arguments)));

	//			var response = new Response(request);

	//			DispatchRequest(request.command, request.arguments, response);

	//			SendMessage(response);
	//		}
	//	}

	//	public void SendEvent(Event e)
	//	{
	//		SendMessage(e);
	//	}
	//}

	public class ProtocolClient : ProtocolEndpoint
	{
		Dictionary<int, Request> Requests = new Dictionary<int, Request>();
		protected override void Dispatch(string message)
		{
			var jObject = Newtonsoft.Json.Linq.JObject.Parse(message);
			var type = jObject.Value<string>("type");
			if (type == "response") {
				var success = jObject.Value<bool>("success");
				var request_seq = jObject.Value<int>("request_seq");
				var request = Requests[request_seq];
				if (success) {
					request.ObjectBody = jObject.GetValue("body").ToObject(request.GetResponseBodyType());
				} else {
					request.ErrorMessage = jObject.Value<string>("message");
				}
				//if (Trace.HasFlag(TraceLevel.Responses))
				//	Console.Error.WriteLine(string.Format("R {0}: {1}", response.command, JsonConvert.SerializeObject(response.body)));
			} else if (type == "event") {
				OnEvent?.Invoke(JsonConvert.DeserializeObject<Event>(message));
			}
		}

		public Action<Event> OnEvent;

		public Task<T2> SendRequestAsync<T1, T2>(Request<T1, T2> request) where T2 : new()
		{
			request.seq = _sequenceNumber++;
			Requests.Add(request.seq, request);
			SendMessage(request);
			return request.WaitingResponse.Task;
		}
	}

	[Flags]
	public enum TraceLevel
	{
		None = 0,
		Events = 1,
		Responses = 2,
		Requests = 4,
		All = 7
	}

	public abstract class ProtocolEndpoint
	{
		public TraceLevel Trace = TraceLevel.None;

		protected const int BUFFER_SIZE = 4096;
		protected const string TWO_CRLF = "\r\n\r\n";
		protected static readonly Regex CONTENT_LENGTH_MATCHER = new Regex(@"Content-Length: (\d+)");

		protected static readonly Encoding Encoding = System.Text.Encoding.UTF8;

		protected int _sequenceNumber;

		private Stream _outputStream;

		private ByteBuffer _rawData;
		private int _bodyLength;

		private bool _stopRequested;


		public ProtocolEndpoint()
		{
			_sequenceNumber = 1;
			_bodyLength = -1;
			_rawData = new ByteBuffer();
		}

		public async Task Start(Stream inputStream, Stream outputStream)
		{
			_outputStream = outputStream;

			byte[] buffer = new byte[BUFFER_SIZE];

			_stopRequested = false;
			while (!_stopRequested) {
				var read = await inputStream.ReadAsync(buffer, 0, buffer.Length);

				if (read == 0) {
					// end of stream
					break;
				}

				if (read > 0) {
					_rawData.Append(buffer, read);
					ProcessData();
				}
			}
		}

		public void Stop()
		{
			_stopRequested = true;
		}

		// ---- private ------------------------------------------------------------------------

		private void ProcessData()
		{
			while (true) {
				if (_bodyLength >= 0) {
					if (_rawData.Length >= _bodyLength) {
						var buf = _rawData.RemoveFirst(_bodyLength);

						_bodyLength = -1;
						var str = Encoding.GetString(buf);
						Task.Run(() => {
							Dispatch(str);
						}).ContinueWith(t => {
							if (t.IsFaulted)
								Console.WriteLine(t.Exception);
						});

						continue;   // there may be more complete messages to process
					}
				} else {
					string s = _rawData.GetString(Encoding);
					var idx = s.IndexOf(TWO_CRLF, StringComparison.Ordinal);
					if (idx != -1) {
						Match m = CONTENT_LENGTH_MATCHER.Match(s);
						if (m.Success && m.Groups.Count == 2) {
							_bodyLength = Convert.ToInt32(m.Groups[1].ToString());

							_rawData.RemoveFirst(idx + TWO_CRLF.Length);

							continue;   // try to handle a complete message
						}
					}
				}
				break;
			}
		}

		protected abstract void Dispatch(string message);

		protected void SendMessage(ProtocolMessage message)
		{

			if (Trace.HasFlag(TraceLevel.Responses) && message.type == "response") {
				Console.Error.WriteLine(string.Format(" R: {0}", JsonConvert.SerializeObject(message)));
			}
			if (Trace.HasFlag(TraceLevel.Requests) && message.type == "request") {
				Console.Error.WriteLine(string.Format(" Q: {0}", JsonConvert.SerializeObject(message)));
			}
			if (Trace.HasFlag(TraceLevel.Events) && message.type == "event") {
				Event e = (Event)message;
				Console.Error.WriteLine(string.Format("E {0}: {1}", e.eventType, JsonConvert.SerializeObject(e.body)));
			}

			var data = ConvertToBytes(message);
			try {
				_outputStream.Write(data, 0, data.Length);
				_outputStream.Flush();
			} catch (Exception) {
				// ignore
			}
		}

		private static byte[] ConvertToBytes(ProtocolMessage request)
		{
			var asJson = JsonConvert.SerializeObject(request);
			byte[] jsonBytes = Encoding.GetBytes(asJson);

			string header = string.Format("Content-Length: {0}{1}", jsonBytes.Length, TWO_CRLF);
			byte[] headerBytes = Encoding.GetBytes(header);

			byte[] data = new byte[headerBytes.Length + jsonBytes.Length];
			System.Buffer.BlockCopy(headerBytes, 0, data, 0, headerBytes.Length);
			System.Buffer.BlockCopy(jsonBytes, 0, data, headerBytes.Length, jsonBytes.Length);

			return data;
		}
	}

	//--------------------------------------------------------------------------------------

	class ByteBuffer
	{
		private byte[] _buffer;

		public ByteBuffer()
		{
			_buffer = new byte[0];
		}

		public int Length {
			get { return _buffer.Length; }
		}

		public string GetString(Encoding enc)
		{
			return enc.GetString(_buffer);
		}

		public void Append(byte[] b, int length)
		{
			byte[] newBuffer = new byte[_buffer.Length + length];
			System.Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _buffer.Length);
			System.Buffer.BlockCopy(b, 0, newBuffer, _buffer.Length, length);
			_buffer = newBuffer;
		}

		public byte[] RemoveFirst(int n)
		{
			byte[] b = new byte[n];
			System.Buffer.BlockCopy(_buffer, 0, b, 0, n);
			byte[] newBuffer = new byte[_buffer.Length - n];
			System.Buffer.BlockCopy(_buffer, n, newBuffer, 0, _buffer.Length - n);
			_buffer = newBuffer;
			return b;
		}
	}
}
