using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using config = Synapse.Handlers.Legacy.Database.Properties.Settings;


namespace Synapse.Handlers.Legacy.Database
{
	public class OracleScriptProcessor : ScriptProcessorBase
	{
		static readonly string _exe = config.Default.SqlPlusPath;
		Process _p = null;
		Script _script = null;
		OracleConnectionSettings _cs = null;
		List<string> _schemas = null;
		string _currSchema = string.Empty;

		public static string GetConnectionString(string schemaName, string instanceName, string instancePort, string databaseName, ref string connectStringMinusLogin)
		{
			string connectString = Utils.Decrypt( config.Default.ConnectionString );
			connectStringMinusLogin = connectString;
			int startIdx = connectStringMinusLogin.IndexOf( @"/" ) + 1;
			string mask = connectStringMinusLogin.Substring( startIdx, connectStringMinusLogin.IndexOf( "@" ) - startIdx );
			connectStringMinusLogin = connectStringMinusLogin.Replace( mask, "Password" );
			connectStringMinusLogin = string.Format( connectStringMinusLogin, schemaName, instanceName, instancePort, databaseName );

			return string.Format( connectString, schemaName, instanceName, instancePort, databaseName );
		}


		static public ConnectionValidationResult ValidateConnectionSettings(OracleConnectionSettings cs)
		{
			ConnectionValidationResult result = new ConnectionValidationResult( true, string.Empty );

			foreach( string schema in cs.Schema )
			{
				string connectionMinusPassword = string.Empty;
				string arguments = GetConnectionString( schema, cs.Instance, cs.Port, cs.Database, ref connectionMinusPassword );
                arguments = " -l " + arguments;
				ConnectionValidationResult r = ValidateConnectionSettings( arguments );
				result.IsValid &= r.IsValid;
				result.Message += string.Format( "\r\n{1}:{0}", r.Message, schema );
			}
			result.Message.Trim();

			return result;
		}

		static ConnectionValidationResult ValidateConnectionSettings(string arguments)
		{
			bool ok = true;
			string msg = "Connection ok.";
			string stdErr = string.Empty;

			string cmd = "\r\n\r\nselect * from global_name;\r\nexit;";

			Process p = ScriptProcessorUtil.CreateProcess( _exe, arguments );

			try
			{
				p.Start();
				p.StandardInput.WriteLine( cmd );

				//stdErr = p.StandardError.ReadToEnd();
				string stdOut = p.StandardOutput.ReadToEnd();

				p.WaitForExit();

				if( Regex.Match( stdOut, config.Default.RegexErrorFilterOracle, RegexOptions.IgnoreCase ).Success )
				{
					ok = false;
					msg = stdOut;
				}
			}
			catch( Exception ex )
			{
				if( ex != null )
				{
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
			_cs = (OracleConnectionSettings)cs;

			StringComparer sc = new StringComparer();

			_schemas = new List<string>();
			if( !string.IsNullOrWhiteSpace( script.Schema ) )
			{
				if( _cs.Schema.Contains( script.Schema, sc ) )
				{
					_schemas.Add( script.Schema );
				}
				else
				{
					throw new Exception( string.Format( "{0} not found in adapter parameters, possible invalid schema.", script.Schema ) );
				}
			}
			else
			{
				throw new Exception( string.Format( "{0}: Schema required in manifest.", cs.Database ) );
			}

			base.Start( script, cs, auditProcedure, variables );
		}

		override public void RunScript(string script, bool isFile, IConnectionSettings cs)
		{
			foreach( string schema in _schemas )
			{
				RunScript( script, schema, isFile, cs );
			}
		}

		public void RunScript(string script, string schema, bool isFile, IConnectionSettings cs)
		{
			OnStepProgress( "RunScript", string.Format( "On Schema {0}", schema ) );
			_currSchema = schema;
			OracleConnectionSettings ocs = (OracleConnectionSettings)cs;

			string cmd = isFile ? string.Format( " @\"{0}\"", script ) : string.Empty;

			string connectionMinusPassword = string.Empty;
			string arguments = GetConnectionString( schema, ocs.Instance, ocs.Port, ocs.Database, ref connectionMinusPassword );
			arguments = string.Format( "{0}{1}", arguments, cmd );

			_p = ScriptProcessorUtil.CreateProcess( _exe, arguments );
			_p.StartInfo.EnvironmentVariables.Add( "SQLPATH", Alphaleonis.Win32.Filesystem.Path.GetShort83Path( PathHelper.CachePath ) );
            _p.StartInfo.EnvironmentVariables.Add( "NLS_LANG", WorkflowParameters.NLS_LANG);

            _p.OutputDataReceived += p_OutputDataReceived;
			_p.ErrorDataReceived += p_ErrorDataReceived;
			_p.StartInfo.WorkingDirectory = Path.GetDirectoryName( Properties.Settings.Default.SqlPlusPath );

			if( _script.ShowSqlInOutput )
			{
				string scriptText = script;
				if( isFile )
				{
					scriptText = File.ReadAllText( script );
				}
				OnStepProgress( string.Empty, scriptText );
			}

			_p.Start();
			if( !isFile )
			{
				_p.StandardInput.WriteLine( script );
			}
			_p.StandardInput.WriteLine( "Exit" );

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
			_p.BeginOutputReadLine();
			_p.BeginErrorReadLine();


			_p.WaitForExit();
		}

		void p_OutputDataReceived(object sender, DataReceivedEventArgs e)
		{
			if( e.Data != null )
			{
				if( Regex.Match( e.Data, config.Default.RegexErrorFilterOracle, RegexOptions.IgnoreCase ).Success )
				{
					_script.Success = false;
					OnStepProgress( string.Empty, e.Data );
					if( Regex.Match( e.Data, config.Default.RegexErrorFilterOracleCompilation, RegexOptions.IgnoreCase ).Success )
					{
						string cmd = "set linesize 150\r\ncol name format a32\r\ncol line format 99999\r\ncol text format a90\r\n";
						cmd = string.Format( "{0}select name , line, Text\r\nfrom user_errors ue inner join user_objects uo on ue.name=uo.object_name \r\nwhere uo.last_ddl_time > sysdate - 1;\r\n", cmd );
						cmd = string.Format( "{0}exit;", cmd );

						string connectionMinusPassword = string.Empty;
						string arguments = GetConnectionString( _currSchema, _cs.Instance, _cs.Port, _cs.Database, ref connectionMinusPassword );
						arguments = string.Format( "-s -r 3 {0}", arguments );

						Process p = ScriptProcessorUtil.CreateProcess( _exe, arguments );
						p.OutputDataReceived += p_OutputDataReceived;
						p.ErrorDataReceived += p_ErrorDataReceived;
						p.Start();
						p.StandardInput.WriteLine( cmd );
						p.BeginOutputReadLine();
						p.BeginErrorReadLine();
						p.WaitForExit();

					}
				}
				else
				{
					OnStepProgress( string.Empty, e.Data );
				}
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