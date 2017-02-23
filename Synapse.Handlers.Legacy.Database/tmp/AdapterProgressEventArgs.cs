using System;

using Sentosa.CommandCenter.Api.Client;

namespace Sentosa.CommandCenter.Adapters.Core
{
	public interface ICancelEventArgs
	{
		bool Cancel { get; set; }
	}
	public class AdapterProgressEventArgs : EventArgs
	{
		public AdapterProgressEventArgs(string context, string message,
			PackageStatus status = PackageStatus.Running, int id = 0, int severity = 0, Exception ex = null)
		{
			Context = context;
			Message = message;
			Status = status;
			Id = id;
			Severity = severity;
			Exception = ex;
		}

		public string Context { get; protected set; }
		public string Message { get; protected set; }
		public PackageStatus Status { get; protected set; }
		public int Id { get; protected set; }
		public int Severity { get; protected set; }
		public Exception Exception { get; protected set; }
		public bool HasException { get { return this.Exception != null; } }
	}
	public class AdapterProgressCancelEventArgs : AdapterProgressEventArgs, ICancelEventArgs
	{
		public AdapterProgressCancelEventArgs(string context, string message,
			PackageStatus status = PackageStatus.Running, int id = 0, int severity = 0,
			bool cancel = false, Exception ex = null)
			: base( context, message, status, id, severity, ex )
		{
			Cancel = cancel;
		}

		public bool Cancel { get; set; }
	}
}