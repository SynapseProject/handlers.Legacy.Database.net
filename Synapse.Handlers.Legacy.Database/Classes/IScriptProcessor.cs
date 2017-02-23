using System;
using System.Collections.Generic;

namespace Synapse.Handlers.Legacy.Database
{
	public interface IScriptProcessor
	{
		event EventHandler<AdapterProgressEventArgs> StepProgress;
        FileHostingPathHelper PathHelper { get; set; }
        WorkflowParameters WorkflowParameters { get; set; }
        
        void Start(Script s, IConnectionSettings cs, AuditProcedure auditProcedure, Dictionary<string, string> variables);
		void RunScript(string script, bool isFile, IConnectionSettings cs);
	}

	public abstract class ScriptProcessorBase : IScriptProcessor
	{
		public event EventHandler<AdapterProgressEventArgs> StepProgress;

        public FileHostingPathHelper PathHelper { get; set; }
        public WorkflowParameters WorkflowParameters { get; set; }

        public virtual void Start(Script script, IConnectionSettings cs, AuditProcedure auditProcedure, Dictionary<string, string> variables)
		{
			bool hasAudit = auditProcedure != null;

			if( hasAudit && auditProcedure.HasPreRun )
			{
				variables["{MetaData}"] = script.MetaData;
				string s = ScriptProcessorUtil.ReplaceVariablesToString( auditProcedure.PreRun, variables );
				RunScript( s, false, cs );
			}

			RunScript( script.Path, true, cs );
			variables["{Success}"] = script.Success.ToString();

			if( hasAudit && auditProcedure.HasPostRun )
			{
				string s = ScriptProcessorUtil.ReplaceVariablesToString( auditProcedure.PostRun, variables );
				RunScript( s, false, cs );
			}
		}


		abstract public void RunScript(string script, bool isFile, IConnectionSettings cs);


		/// <summary>
		/// Notify of step progress.
		/// </summary>
		/// <param name="context">The method name or workflow activty.</param>
		/// <param name="message">Descriptive message.</param>
		/// <param name="status">Overall Package status indicator.</param>
		/// <param name="id">Message Id.</param>
		/// <param name="severity">Message/error severity.</param>
		/// <param name="ex">Current exception (optional).</param>
		protected virtual void OnStepProgress(string context, string message, PackageStatus status = PackageStatus.Running, int id = 0, int severity = 0, Exception ex = null)
		{
			if( StepProgress != null )
			{
				StepProgress( this, new AdapterProgressEventArgs( context, message, status, id, severity, ex ) );
			}
		}
	}

	public class ConnectionValidationResult
	{
		public ConnectionValidationResult(bool isValid, string message)
		{
			IsValid = isValid;
			Message = message;
		}
		public bool IsValid { get; set; }
		public string Message { get; set; }
	}
}