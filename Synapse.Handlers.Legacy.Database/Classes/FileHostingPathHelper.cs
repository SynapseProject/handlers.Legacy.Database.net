using System;
using System.Text.RegularExpressions;

using settings = Synapse.Handlers.Legacy.Database.Properties.Settings;

using Alphaleonis.Win32.Filesystem;

namespace Synapse.Handlers.Legacy.Database
{
    public class FileHostingPathHelper
    {
        string _workPath = string.Empty;
        ScriptFolder _scriptFolder = null;

        public FileHostingPathHelper(string environment, string appName, string adapterName,
            string schemaFolder, string requestNumber,
            ScriptFolder scriptFolder, string sourceRoot, string cacheRoot,
            string stagedRoot, string processedRoot, string packageAdapterInstanceId)
        {
            _scriptFolder = scriptFolder;

            Environment = environment;
            ApplicationName = appName;
            AdapterName = adapterName;
            SchemaFolder = schemaFolder;
            RequestNumber = !string.IsNullOrWhiteSpace( requestNumber ) ? requestNumber : packageAdapterInstanceId;

            WorkRoot = scriptFolder.WorkRoot;
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
            ApplicationName = Regex.Replace( ApplicationName, "[\\\\/\\*<>\\|:\\?\"]", string.Empty );

            switch( _scriptFolder.Type )
            {
                case ScriptFolderType.DiskPackage:
                {
                    _workPath = Path.Combine( Environment, ApplicationName, AdapterName );
                    break;
                }
                case ScriptFolderType.DiskStatic:
                case ScriptFolderType.Nostromo:
                {
                    _workPath = Path.GetFileNameWithoutExtension( Path.GetTempFileName() );
                    break;
                }
            }

            if( string.IsNullOrWhiteSpace( SchemaFolder ) ) { SchemaFolder = string.Empty; }
            if( string.IsNullOrWhiteSpace( RequestNumber ) ) { RequestNumber = string.Empty; }

            switch( _scriptFolder.Type )
            {
                case ScriptFolderType.DiskPackage:
                {
                    SourcePath = Path.Combine( WorkRoot, SourceRoot, _workPath, SchemaFolder );
                    CachePath = Path.Combine( WorkRoot, CacheRoot, _workPath, RequestNumber, SchemaFolder );
                    StagedPath = Path.Combine( WorkRoot, StagedRoot, _workPath, RequestNumber, SchemaFolder );
                    ProcessedPath = Path.Combine( WorkRoot, ProcessedRoot, _workPath, RequestNumber, SchemaFolder );
                    break;
                }
                case ScriptFolderType.DiskStatic:
                case ScriptFolderType.Nostromo:
                {
                    SourcePath = Path.Combine( WorkRoot, _scriptFolder.ContainerName, SchemaFolder );

                    string folderRoot = settings.Default.FolderRoot;
                    CachePath = Path.Combine( WorkRoot, folderRoot, _scriptFolder.ContainerName, CacheRoot,
                         _workPath, RequestNumber, SchemaFolder );
                    StagedPath = Path.Combine( WorkRoot, folderRoot, _scriptFolder.ContainerName, StagedRoot,
                         _workPath, RequestNumber, SchemaFolder );
                    ProcessedPath = Path.Combine( WorkRoot, folderRoot, _scriptFolder.ContainerName, ProcessedRoot,
                         _workPath, RequestNumber, SchemaFolder );

                    Directory.CreateDirectory( CachePath, PathFormat.LongFullPath );
                    Directory.CreateDirectory( StagedPath, PathFormat.LongFullPath );
                    Directory.CreateDirectory( ProcessedPath, PathFormat.LongFullPath );

                    break;
                }
            }

            SourcePathIsValid = Directory.Exists( SourcePath );
            //CacheRootIsValid = Directory.Exists( CacheRoot );
            //StagedRootIsValid = Directory.Exists( StagedRoot );
            //ProcessedRootIsValid = Directory.Exists( ProcessedRoot );

            IsValid = SourcePathIsValid; // && CacheRootIsValid && StagedRootIsValid && ProcessedRootIsValid;
        }
    }
}