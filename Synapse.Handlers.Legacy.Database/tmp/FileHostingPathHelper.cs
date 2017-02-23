using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Alphaleonis.Win32.Filesystem;
using Sentosa.CommandCenter.Api.Client;

namespace Sentosa.CommandCenter.Adapters.Core
{
	public class FileHostingPathHelper
	{
		string _workPath = string.Empty;

		public FileHostingPathHelper(string environment, string appName, string adapterName, string schemaFolder, string requestNumber,
			string workRoot, string sourceRoot, string cacheRoot, string stagedRoot, string processedRoot)
		{
			Environment = environment;
			ApplicationName = appName;
			AdapterName = adapterName;
			SchemaFolder = schemaFolder;
			RequestNumber = requestNumber;

			WorkRoot = workRoot;
			SourceRoot = sourceRoot;
			CacheRoot = cacheRoot;
			StagedRoot = stagedRoot;
			ProcessedRoot = processedRoot;

			PrepareAndValidate();
		}

		#region properties
		public string Environment { get; private set; }
		public string ApplicationName { get; private set; }
		public string AdapterName { get; private set; }
		public string RequestNumber { get; private set; }

		public string WorkRoot { get; private set; }

		public string SourceRoot { get; private set; }
		public string SchemaFolder { get; private set; }
		public string SourcePath { get; private set; }
		public bool SourcePathIsValid { get; private set; }

		public string CacheRoot { get; private set; }
		public bool CacheRootIsValid { get; private set; }
		public string CachePath { get; private set; }
		public string StagedRoot { get; private set; }
		public bool StagedRootIsValid { get; private set; }
		public string StagedPath { get; private set; }

		public string ProcessedRoot { get; private set; }
		public bool ProcessedRootIsValid { get; private set; }
		public string ProcessedPath { get; private set; }

		public bool IsValid { get; private set; }
		#endregion

		public void PrepareAndValidate()
		{
			_workPath = Path.Combine( Environment, ApplicationName, AdapterName );

			if( string.IsNullOrWhiteSpace( SchemaFolder ) ) { SchemaFolder = string.Empty; }

			SourcePath = Path.Combine( WorkRoot, SourceRoot, _workPath, SchemaFolder );
			CachePath = Path.Combine( WorkRoot, CacheRoot, _workPath, RequestNumber, SchemaFolder );
			StagedPath = Path.Combine( WorkRoot, StagedRoot, _workPath, RequestNumber, SchemaFolder );
			ProcessedPath = Path.Combine( WorkRoot, ProcessedRoot, _workPath, RequestNumber, SchemaFolder );

			SourcePathIsValid = Directory.Exists( SourcePath );
			//CacheRootIsValid = Directory.Exists( CacheRoot );
			//StagedRootIsValid = Directory.Exists( StagedRoot );
			//ProcessedRootIsValid = Directory.Exists( ProcessedRoot );

			IsValid = SourcePathIsValid; // && CacheRootIsValid && StagedRootIsValid && ProcessedRootIsValid;
		}
	}
}