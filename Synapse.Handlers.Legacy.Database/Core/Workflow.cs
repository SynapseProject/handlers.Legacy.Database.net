using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Synapse.Core;

using Alphaleonis.Win32.Filesystem;

using config = Synapse.Handlers.Legacy.Database.Properties.Settings;


namespace Synapse.Handlers.Legacy.Database
{
	public class Workflow
	{
		WorkflowParameters _wfp = null;
		Dictionary<string, string> _variables = new Dictionary<string, string>();
        HandlerStartInfo _startInfo = null;

        public Action<string, string, LogLevel, Exception> OnLogMessage;
        public Func<string, string, StatusType, long, int, bool, Exception, bool> OnProgress;

        FileHostingPathHelper _ph = null;

		//required for reflection-based invocation, used in Legacy Software
		public Workflow() { }

		public Workflow(WorkflowParameters wfp)
		{
			_wfp = wfp;
			_wfp.InitIsValid();
		}

		public Workflow Initialize(ref WorkflowParameters wfp, HandlerStartInfo startInfo)
		{
            _startInfo = startInfo;
			((WorkflowParameters)wfp).InitIsValid();
            ((WorkflowParameters)wfp).PackageAdapterInstanceId = _startInfo.InstanceId + "";
			((WorkflowParameters)wfp).RequestNumber = _startInfo.RequestNumber;

			return this;
		}

		public WorkflowParameters Parameters { get { return _wfp; } set { _wfp = value as WorkflowParameters; } }

		public void ExecuteAction()
		{
			string context = "ExecuteAction";

			string msg = Utils.GetHeaderMessage(
				string.Format( "Synapse, Legacy Database Adapter. {0}, Entering Main Workflow.", Utils.GetBuildDateVersion() ) );
			if( OnStepStarting( context, msg ) )
			{
				return;
			}

            OnStepProgress(context, Utils.CompressXml(_startInfo.Parameters));

            Stopwatch clock = new Stopwatch();
            clock.Start();

            Exception ex = null;
            try
            {
                #region initialize global variable dictionary
                _variables["{RequestNumber}"] = _wfp.RequestNumber;
                _variables["{PackageAdapterInstanceId}"] = _wfp.PackageAdapterInstanceId;
                _variables["{MetaData}"] = string.Empty;
                _variables["{Success}"] = true.ToString();
                #endregion

                try
                {
                    _wfp.ScriptFolder.Resolve();
                    OnStepProgress(context, Utils.GetMessagePadRight("ScriptFolder", _wfp.ScriptFolder, 50));
                }
                catch (Exception rex)
                {
                    string err = GetExceptionMessageRecursive(rex);
                    throw new Exception(
                    string.Format("Unable to resolve ScriptFolder.  Exception: {0}\r\n{1}",
                    err, _wfp.SchemaFolder));
                }

                _ph = new FileHostingPathHelper(
                    _wfp.Environment, _wfp.ApplicationName,
                    config.Default.FolderPath,
                    _wfp.SchemaFolder, _wfp.RequestNumber,
                    _wfp.ScriptFolder, config.Default.SourceRoot,
                    config.Default.CachedRoot, config.Default.StagedRoot,
                    config.Default.ProcessedRoot, _wfp.PackageAdapterInstanceId);

                if (_ph.IsValid && _wfp.PrepareAndValidateConnections())
                {
                    if (!_startInfo.IsDryRun)
                    {
                        string source = _ph.SourcePath;
                        if (_wfp.IsControlledMigration && _wfp.MigrationType == MigrationType.Prod)
                        {
                            source = _ph.StagedPath;
                        }

                        bool useSqlImpersonation = _wfp.DatabaseType == DatabaseType.SqlServer && !_wfp.SuppressSqlServerImpersonation;
                        string workPath = Path.Combine(_ph.CachePath, "work");

                        //copy content to cache, then reset the script folder
                        //  from a relative to full(resolved) path
                        CopyFolder(source, _ph.CachePath);
                        _wfp.ScriptFolder.SourcePath = _ph.CachePath;
                        if (useSqlImpersonation)
                        {
                            CopyFolder(source, workPath);
                            _wfp.ScriptFolder.SourcePath = workPath;
                        }

                        //load and validates the section info (RollIn/RollBack)
                        _wfp.PrepareAndValidateSections();

                        //give some output
                        WriteParameterInfo(_ph);

                        if (_wfp.Sections.IsValid)
                        {
                            //delete the staged source, it's in cache and valid, so let's roll (so to speak)
                            Task.Factory.StartNew((object obj) => { DeleteFolder(source, true); }, null);


                            if (useSqlImpersonation)
                            {
                                _wfp.Sections.RewriteScripts();
                                IterateSections();
                                DeleteFolder(workPath, false);
                            }
                            else
                            {
                                IterateSections();
                            }

                            if (_wfp.IsControlledMigration && _wfp.MigrationType == MigrationType.PreProd)
                            {
                                CopyFolder(_ph.CachePath, _ph.StagedPath);
                            }

                            MoveFolder(_ph.CachePath, _ph.ProcessedPath);
                        }
                        else
                        {
                            //sections not valid, delete the cache (housekeeping)
                            DeleteFolder(_ph.CachePath, false);
                            throw new Exception("No valid manifest files found");
                        }
                    }
                }
                else
                {
                    WriteParameterInfo(_ph);
                }
            }
            catch (Exception exception)
            {
                ex = exception;
            }
            if (_wfp.Sections != null)
            {
                WriteWorkflowSectionDetail(true);
            }

            StatusType ps = GetPackageStatusFromErrorState(ex);
            bool ok = ps == StatusType.Complete;
            msg = Utils.GetHeaderMessage(string.Format("End Main Workflow: {0}, Total Execution Time: {1}",
                ok ? "Complete." : "One or more steps failed.", clock.ElapsedSeconds()));
            OnProgress(context, msg, ps, _startInfo.InstanceId, int.MaxValue, false, ex);

        }
        string GetExceptionMessageRecursive(Exception exception)
        {
            System.Text.StringBuilder msg = new System.Text.StringBuilder();

            Stack<Exception> exceptions = new Stack<Exception>();
            exceptions.Push(exception);

            while (exceptions.Count > 0)
            {
                Exception ex = exceptions.Pop();
                if (ex.InnerException != null) { exceptions.Push(ex.InnerException); }

                msg.AppendFormat("- {0}\r\n", ex.Message);
            }

            return msg.ToString();
        }

        StatusType GetPackageStatusFromErrorState(Exception ex)
		{
            StatusType ps = StatusType.Complete;

			if( !_wfp.IsValid || ex != null )
			{
				ps = StatusType.Failed;
			}
			else if( !_wfp.Sections.WorkflowSuccess )
			{
				if( !_wfp.Sections.RollIn.Success )
				{
					ps = _wfp.Sections.RollIn.FailWorkflowOnError ? StatusType.Failed : StatusType.CompletedWithErrors;

					if( _wfp.Sections.RollBack.HasScripts && !_wfp.Sections.RollBack.Success )
					{
						ps = _wfp.Sections.RollBack.FailWorkflowOnError ? StatusType.Failed : StatusType.CompletedWithErrors;
					}
				}
			}

			return ps;
		}

		void WriteParameterInfo(FileHostingPathHelper ph)
		{
			string context = "ExecuteAction";
			const int padding = 50;

			OnStepProgress( context, Utils.GetHeaderMessage( "Begin [PrepareAndValidate]" ) );

			//_wfp.PrepareAndValidate();

			OnStepProgress( context, Utils.GetMessagePadRight( "ScriptFolder", _wfp.ScriptFolder, padding ) );
			OnStepProgress( context, Utils.GetMessagePadRight( "SqlPlusPath", config.Default.SqlPlusPath, padding ) );
			OnStepProgress( context, Utils.GetMessagePadRight( "RequestNumber", _wfp.RequestNumber, padding ) );
			OnStepProgress( context, Utils.GetMessagePadRight( "PackageAdapterInstanceID", _wfp.PackageAdapterInstanceId, padding ) );
			OnStepProgress( context, Utils.GetMessagePadRight( "SchemaFolder", ph.SchemaFolder, padding ) );
            OnStepProgress( context, Utils.GetMessagePadRight( "WorkRoot", ph.WorkRoot, padding ) );
            OnStepProgress( context, Utils.GetMessagePadRight( "Environment", ph.Environment, padding));
            OnStepProgress( context, Utils.GetMessagePadRight( "ApplicationName", ph.ApplicationName, padding));
            OnStepProgress( context, Utils.GetMessagePadRight( "ManifestFolder", ph.SourcePath, padding));
			
            if( _wfp.Sections != null )
			{
				OnStepProgress( context, Utils.GetMessagePadRight( "Manifest", _wfp.Sections, padding ) );
				OnStepProgress( context, Utils.GetMessagePadRight( "Manifest", _wfp.Sections.ToXml( false ), padding ) );
				WriteWorkflowSectionDetail( false );
			}
			else
			{
				OnStepProgress( context, Utils.GetMessagePadRight( "Manifest", "(null)", padding ) );
			}

			foreach( IConnectionSettings cs in _wfp.ConnectionSettings )
			{
				OnStepProgress( context, Utils.GetMessagePadRight( "ConnectionSettings", cs, padding ) );
			}
		}

		void WriteWorkflowSectionDetail(bool forStatus)
		{
			WriteSectionDetail( _wfp.Sections.RollIn, forStatus );
			WriteSectionDetail( _wfp.Sections.RollBack, forStatus );
		}

		void WriteSectionDetail(Section section, bool forStatus)
		{
			const int padding = 50;
			List<Script> scripts = null;

			string msg = forStatus ? "ExecutionStatus: {0}" : "IsValid: {0}";
			bool value = true;

			Stack<List<Script>> list = new Stack<List<Script>>();
			list.Push( section.Scripts );
			while( list.Count > 0 )
			{
				scripts = list.Pop();
				foreach( Script script in scripts )
				{
					value = script.IsValid;
					if( forStatus )
					{
						value = script.Success;

						//note: override the section status for better error reporting
						if( !script.Success )
						{
							section.Success = false;
						}
					}

					string headerMsg = string.Format( msg, value );
					if( forStatus )
					{
						if( script.ExecutionStatus == ExecutionStatus.NotExecuted )
						{
							headerMsg = string.Format( headerMsg, "Not Executed" );
						}
						else
						{
							headerMsg = string.Format( headerMsg, value ? "Success" : "Failed" );
						}
					}
					OnStepProgress( section.Name,
						Utils.GetMessagePadRight( headerMsg, script.Path, padding ) );

					if( script.Scripts.Count > 0 )
					{
						list.Push( script.Scripts );
					}
				}
			}
		}


		#region IterateSections
		/// <summary>
		/// Iterates all the sections per ConnectionSetting
		/// </summary>
		void IterateSections()
		{
			foreach( IConnectionSettings cs in _wfp.ConnectionSettings )
			{
				IterateSections( cs );
			}
		}

		/// <summary>
		/// For a given database connection, executes the PreWork, RollIn [RollBack/RollIn], PostWork process
		/// </summary>
		void IterateSections(IConnectionSettings cs)
		{
			//ProcessSection( _wfp.Sections.PreWork, cs );
			//if( _wfp.Sections.PreWork.Success )
			//{
			//}

			ProcessSection( _wfp.Sections.RollIn, cs );
			if( !_wfp.Sections.RollIn.Success )
			{
				ProcessSection( _wfp.Sections.RollBack, cs );
			}


			//if any of the above sections have failed the workflow, skip this (WorkflowSuccess == false)
			if( _wfp.Sections.WorkflowSuccess && _wfp.IsTraditional )
			{
				ProcessSection( _wfp.Sections.RollBack, cs );
				if( _wfp.Sections.RollBack.Success )
				{
					ProcessSection( _wfp.Sections.RollIn, cs );
					if( !_wfp.Sections.RollIn.Success )
					{
						ProcessSection( _wfp.Sections.RollBack, cs );
					}
				}
			}

			//if any of the above sections have failed the workflow, skip this (WorkflowSuccess == false)
			//if( _wfp.Sections.WorkflowSuccess )
			//{
			//	ProcessSection( _wfp.Sections.PostWork, cs );
			//}
		}

		/// <summary>
		/// Recurse the scripts of a section; set Section.Success && _wfp.Sections.WorkflowSuccess
		/// </summary>
		/// <param name="section">The sectipon to process.</param>
		void ProcessSection(Section section, IConnectionSettings cs)
		{
			if( !section.HasScripts ) //nothing to do
			{
				section.Success = true;
				return;
			}

			OnStepProgress( "ProcessSection", string.Format( "Starting Section:{0}, Connection:{1}", section.Name, cs.ToString() ) );

			section.Success = true;
			RecurseScripts( section.Scripts, section, cs );
			if( !section.Success && section.FailWorkflowOnError )
			{
				//this is where a section will fail the workflow
				_wfp.Sections.WorkflowSuccess = false;
			}

			OnStepProgress( "ProcessSection", string.Format( "Finished Section:{0}, SectionSuccess:{1}, WorkflowSuccess:{2}",
				section.Name, section.Success, _wfp.Sections.WorkflowSuccess ) );
		}

		/// <summary>
		/// Recursively execute a list of scripts; will set section.Success
		/// </summary>
		/// <param name="scripts">The section.Scripts</param>
		/// <param name="section">The section itself, used to set section.Success</param>
		void RecurseScripts(List<Script> scripts, Section section, IConnectionSettings cs)
		{
			foreach( Script s in scripts )
			{
				OnStepProgress( "RecurseScripts", string.Format( "Starting Script:{0}", s.Path ) );

				IScriptProcessor p = ScriptProcessorUtil.GetProcessorInstance( _wfp.DatabaseType );
                p.PathHelper = _ph;
                p.WorkflowParameters = _wfp;
				p.StepProgress += p_StepProgress;
				p.Start( s, cs, _wfp.AuditProcedures, _variables );

				OnStepProgress( "RecurseScripts", string.Format( "Finished Script:{0}, Success:{1}", s.Path, s.Success ) );

				if( s.Success )
				{
					RecurseScripts( s.SuccessScripts, section, cs );
				}
				else
				{
					if( s.FailBranchOnError )
					{
						if( s.FailSectionOnError ) { section.Success = false; }
						break;
					}
					else
					{
						RecurseScripts( s.FailScripts, section, cs );
					}
				}
			}
		}
		#endregion


		#region File/Directory Stuff
		/// <summary>
		/// Callback handler from Directory.Copy/Move operations.
		/// </summary>
		/// <returns>Returns 'Continue' to localized error handling (in the source caller).</returns>
		CopyMoveProgressResult CopyMoveProgressHandler(long totalFileSize, long totalBytesTransferred,
			long streamSize, long streamBytesTransferred, int streamNumber,
			CopyMoveProgressCallbackReason callbackReason, object userData)
		{
			if( userData != null )
			{
				string[] files = userData.ToString().Split( '|' );
                OnProgress("CopyMoveProgress",
                    string.Format("Copied file: {0}  [to]  {1}", files[0], files[1]),
                    StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, null);
			}

			return CopyMoveProgressResult.Continue;
		}

		/// <summary>
		/// Copies a directory and its contents to a new location.  Notifies of each file processed via "CopyMoveProgressHandler".
		/// </summary>
		/// <param name="source">The source directory path.</param>
		/// <param name="destination">The destination directory path.</param>
		void CopyFolder(string source, string destination)
		{
			try
			{
				//CopyOptions.None overrides CopyOptions.FailIfExists, meaning, overwrite any existing files
				Directory.Copy( source, destination, CopyOptions.None, CopyMoveProgressHandler, null, PathFormat.FullPath );
			}
			catch( Exception ex )
			{
				string msg = string.Format( "CopyFolder failed on: source:[{0}], destination:[{1}]", source, destination );
                OnProgress(msg, ex.Message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, ex);
				throw ex;
			}
		}

		/// <summary>
		/// Moves all children within source folder to the destination path.  Does not move the source folder itself.
		/// </summary>
		/// <param name="source">The source directory path.</param>
		/// <param name="destination">The destination directory path.</param>
		void CopyFolderContent(string source, string destination)
		{
			string context = "CopyFolderContent";

			string msg = Utils.GetHeaderMessage(
				string.Format( "Moving content to next environment staging folder: [{0}  [to]  {1}]", source, destination ) );
			if( OnStepStarting( context, msg ) )
			{
				return;
			}

			Stopwatch clock = new Stopwatch();
			clock.Start();

			try
			{
				string[] dirs = Directory.GetDirectories( source );

				if( !Directory.Exists( destination ) )
				{
					Directory.CreateDirectory( destination, PathFormat.FullPath );
				}

				foreach( string dir in dirs )
				{
					string folder = Path.GetDirectoryNameWithoutRoot( dir + @"\\" );
					string dst = Path.Combine( destination, folder );
					Directory.Copy( dir, dst, CopyOptions.None, PathFormat.FullPath );
				}

				string[] files = Directory.GetFiles( source );
				foreach( string file in files )
				{
					string dst = Path.Combine( destination, Path.GetFileName( file ) );
					File.Copy( file, dst, CopyOptions.None, PathFormat.FullPath );
				}

				clock.Stop();
				msg = Utils.GetHeaderMessage( string.Format( "End move for: [{0}  [to]  {1}]: Total Execution Time: {2}",
					source, destination, clock.ElapsedSeconds() ) );
				OnStepFinished( context, msg );
			}
			catch( Exception ex )
			{
				msg = string.Format( "CopyFolderContent failed on: source:[{0}], destination:[{1}]", source, destination );
                OnProgress(msg, ex.Message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, ex);
				throw ex;
			}
		}

		/// <summary>
		/// Copies a directory and its contents to a new location.  Notifies of each file processed via "CopyMoveProgressHandler".
		/// </summary>
		/// <param name="source">The source directory path.</param>
		/// <param name="destination">The destination directory path.</param>
		void MoveFolder(string source, string destination)
		{
			try
			{
				if( !Directory.Exists( destination ) )
				{
					Directory.CreateDirectory( destination, PathFormat.FullPath );
				}

				//CopyOptions.None overrides CopyOptions.FailIfExists, meaning, overwrite any existing files
				Directory.Move( source, destination, MoveOptions.ReplaceExisting, CopyMoveProgressHandler, null, PathFormat.FullPath );
			}
			catch( Exception ex )
			{
				string msg = string.Format( "MoveFolder failed on: source:[{0}], destination:[{1}]", source, destination );
                OnProgress(msg, ex.Message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, ex);
				throw ex;
			}
		}

		/// <summary>
		/// Moves all children within source folder to the destination path.  Does not move the source folder itself.
		/// </summary>
		/// <param name="source">The source directory path.</param>
		/// <param name="destination">The destination directory path.</param>
		void MoveFolderContent(string source, string destination)
		{
			string context = "MoveFolderContent";

			string msg = Utils.GetHeaderMessage(
				string.Format( "Moving content to next environment staging folder: [{0}  [to]  {1}]", source, destination ) );
			if( OnStepStarting( context, msg ) )
			{
				return;
			}

			Stopwatch clock = new Stopwatch();
			clock.Start();

			try
			{
				if( !Directory.Exists( destination ) )
				{
					Directory.CreateDirectory( destination, PathFormat.FullPath );
				}

				string[] dirs = Directory.GetDirectories( source );
				foreach( string dir in dirs )
				{
					string folder = Path.GetDirectoryNameWithoutRoot( dir + @"\\" );
					string dst = Path.Combine( destination, folder );
					Directory.Move( dir, dst,
						MoveOptions.ReplaceExisting | MoveOptions.WriteThrough, PathFormat.FullPath );
				}

				string[] files = Directory.GetFiles( source );
				foreach( string file in files )
				{
					string dst = Path.Combine( destination, Path.GetFileName( file ) );
					File.Move( file, dst,
						MoveOptions.ReplaceExisting | MoveOptions.WriteThrough, PathFormat.FullPath );
				}

				clock.Stop();
				msg = Utils.GetHeaderMessage( string.Format( "End move for: [{0}  [to]  {1}]: Total Execution Time: {2}",
					source, destination, clock.ElapsedSeconds() ) );
				OnStepFinished( context, msg );
			}
			catch( Exception ex )
			{
				msg = string.Format( "MoveFolderContent failed on: source:[{0}], destination:[{1}]", source, destination );
                OnProgress(msg, ex.Message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, ex);
				throw ex;
			}
		}

		/// <summary>
		/// Deletes the specified directory and its children, or deletes only the children.
		/// </summary>
		/// <param name="path">The name of the directory to delete.</param>
		/// <param name="enumerateChildrenForDelete">Pass true to delete only the children, false to delete the specified directory and all children.</param>
		void DeleteFolder(string path, bool enumerateChildrenForDelete)
		{
			try
			{
				if( enumerateChildrenForDelete )
				{
					string[] dirs = Directory.GetDirectories( path );
					foreach( string dir in dirs )
					{
						//true->recursive, true->ignoreReadOnly
						Directory.Delete( dir, true, true, PathFormat.FullPath );
					}

					string[] files = Directory.GetFiles( path );
					foreach( string file in files )
					{
						//true->ignoreReadOnly
						File.Delete( file, true, PathFormat.FullPath );
					}
				}
				else
				{
					//true->recursive, true->ignoreReadOnly
					Directory.Delete( path, true, true, PathFormat.FullPath );
				}
			}
			catch( Exception ex )
			{
				string msg = string.Format( "DeleteFolder failed on: path:[{0}], enumerateChildrenForDelete:[{1}]", path, enumerateChildrenForDelete );
                OnProgress(msg, ex.Message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, ex);
				throw ex;
			}
		}
		#endregion


		#region NotifyProgress Events
		int _cheapSequence = 0;
		bool _globalCancel = false;

		void p_StepProgress(object sender, AdapterProgressEventArgs e)
		{
            OnProgress(e.Context, e.Message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, e.Exception);
		}

		/// <summary>
		/// Notify of step beginning. If return value is True, then cancel operation.
		/// Defaults: PackageStatus.Running, Id = _cheapSequence++, Severity = 0, Exception = null.
		/// </summary>
		/// <param name="context">The method name.</param>
		/// <param name="message">Descriptive message.</param>
		/// <returns>AdapterProgressCancelEventArgs.Cancel value.</returns>
		bool OnStepStarting(string context, string message)
		{
            OnProgress(context, message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, null);
            return true;
		}

		/// <summary>
		/// Notify of step progress.
		/// Defaults: PackageStatus.Running, Id = _cheapSequence++, Severity = 0, Exception = null.
		/// </summary>
		/// <param name="context">The method name.</param>
		/// <param name="message">Descriptive message.</param>
		void OnStepProgress(string context, string message)
		{
            OnProgress(context, message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, null);
        }

        /// <summary>
        /// Notify of step completion.
        /// Defaults: PackageStatus.Running, Id = _cheapSequence++, Severity = 0, Exception = null.
        /// </summary>
        /// <param name="context">The method name or workflow activty.</param>
        /// <param name="message">Descriptive message.</param>
        void OnStepFinished(string context, string message)
		{
            OnProgress(context, message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, null);
        }
        #endregion

    }
}