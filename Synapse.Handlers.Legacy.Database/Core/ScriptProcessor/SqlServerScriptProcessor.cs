using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

using config = Synapse.Handlers.Legacy.Database.Properties.Settings;


namespace Synapse.Handlers.Legacy.Database
{
	public class SqlServerScriptProcessor : ScriptProcessorBase
	{
		static readonly string _exe = "sqlcmd.exe";
		Script _script = null;

		static public ConnectionValidationResult ValidateConnectionSettings(SqlServerConnectionSettings cs)
		{
			bool ok = true;
			string msg = "Connection ok.";
            string stdErr = string.Empty;

			string cmd = " -Q \"select getdate();\"";

			string arguments = string.Format( "-S {0} -d {1} -E {2}",
				cs.Instance, cs.Database, cmd );

			Process p = ScriptProcessorUtil.CreateProcess( _exe, arguments );

			try
			{
				p.Start();

				p.BeginErrorReadLine();
				string stdOut = p.StandardOutput.ReadToEnd();

				p.WaitForExit();

				if( Regex.Match( stdOut, config.Default.RegexErrorFilterSqlServer, RegexOptions.IgnoreCase ).Success )
				{
					ok = false;
					msg = stdOut;
				}
			}
			catch( Exception ex )
			{
				if( ex != null )
				{
//					msg = p.StandardError.ReadToEnd();                --This was old error information. Changed to OracleScriptProcessor version
                    msg = stdErr;
					msg += ex.Message;
				}

				ok = false;
			}

			return new ConnectionValidationResult( ok, msg );
		}

		override public void Start(Script script, IConnectionSettings cs, AuditProcedure auditProcedure, Dictionary<string, string> variables)
		{
			_script = script;
			_script.Success = true;
			_script.ExecutionStatus = ExecutionStatus.Executed;

			base.Start( script, cs, auditProcedure, variables );
		}

		override public void RunScript(string script, bool isFile, IConnectionSettings cs)
		{
			string cmd = isFile ? string.Format( "-i \"{0}\"", script ) :
				string.Format( "-Q \"{0}\"", script );

			string arguments = string.Format( "-S {0} -d {1} -E {2} {3} {4} {5}",
				cs.Instance, cs.Database,
				_script.ShowSqlInOutput ? "-e" : string.Empty,
				_script.FailBranchOnError ? "-b" : string.Empty,
                _script.SetQuotedIdentifier ? "-I" : string.Empty,
				cmd );


			Process p = ScriptProcessorUtil.CreateProcess( _exe, arguments );
			p.OutputDataReceived += p_OutputDataReceived;
			p.ErrorDataReceived += p_ErrorDataReceived;

			p.Start();

			#region read this
			// best practice information on accessing stdout/stderr from mdsn article:
			//  https://msdn.microsoft.com/en-us/library/system.diagnostics.processstartinfo.redirectstandardoutput%28v=vs.110%29.aspx
			// Do not wait for the child process to exit before reading to the end of its redirected stream.
			// Do not perform a synchronous read to the end of both redirected streams.
			// string output = p.StandardOutput.ReadToEnd();
			// string error = p.StandardError.ReadToEnd();
			// p.WaitForExit();
			// Use asynchronous read operations on at least one of the streams.
			#endregion
			p.BeginOutputReadLine();
            string error = p.StandardError.ReadToEnd();


			p.WaitForExit();
		}

		void p_OutputDataReceived(object sender, DataReceivedEventArgs e)
		{
			if( e.Data != null )
			{
				if( Regex.Match( e.Data, config.Default.RegexErrorFilterSqlServer, RegexOptions.IgnoreCase ).Success )
				{
					_script.Success = false;
				}

				OnStepProgress( string.Empty, e.Data );
			}
		}

		void p_ErrorDataReceived(object sender, DataReceivedEventArgs e)
		{
			if( e.Data != null )
			{
				_script.Success = false;
				OnStepProgress( string.Empty, e.Data );
			}
		}
	}
}