using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml;
using System.Xml.Serialization;

using Alphaleonis.Win32.Filesystem;

using nostromo = Synapse.Internal.Nostromo;

using settings = Synapse.Handlers.Legacy.Database.Properties.Settings;

namespace Synapse.Handlers.Legacy.Database
{
    [Serializable, XmlRoot( "Database" )]
    public class WorkflowParameters
    {
        //required for reflection-based invocation, used in Legacy Software
        public WorkflowParameters() { }

        const string _preworkManifestFile = "manifest_prework.txt";
        const string _rollInManifestFile = "manifest.txt";
        const string _rollBackManifestFile = "manifest_rollback.txt";
        const string _postworkManifestFile = "manifest_postwork.txt";
        const string _workflowSectionsManifestFile = "manifest.xml";

        [XmlElement]
        public DatabaseType DatabaseType { get; set; }

        [XmlIgnore]
        private string PreworkManifestFile { get; set; } //internal
        [XmlIgnore]
        public string RollInManifestFile { get; internal set; }
        [XmlIgnore]
        public string RollBackManifestFile { get; internal set; }
        [XmlIgnore]
        private string PostworkManifestFile { get; set; } //internal

        [XmlIgnore]
        public string WorkflowSectionsManifestFile { get; internal set; }
        //[XmlIgnore]
        //public bool HasWorkflowSectionsManifestFile { get { return !string.IsNullOrWhiteSpace( WorkflowSectionsManifestFile ); } }
        [XmlIgnore]
        public WorkflowSections Sections { get; internal set; }

        //script-level flags
        [XmlElement( "ContinueOnErrorFlag" )]
        public bool ContinueOnError { get; set; }
        [XmlElement( "DisplaySQLFlag" )]
        public bool ShowSqlInOutput { get; set; }
        [XmlElement]
        public bool? SetQuotedIdentifier { get; set; }

        //workflow-level flags
        [XmlElement( "RollInRollOutRollInFlag" )]
        public bool IsTraditional { get; set; }
        //[XmlElement( "RollbackOnErrorFlag" )]
        //public bool RollbackOnError { get; set; }

        public MigrationType MigrationType { get; set; }
        [XmlElement( "StagedMigrationFlag" )]
        public bool IsControlledMigration { get; set; }

        [XmlElement( "SupressSQLServerImpersonation" )]
        public bool SuppressSqlServerImpersonation { get; set; }
        public string NLS_LANG { get; set; }

        [XmlElement]
        public AuditProcedure AuditProcedures { get; set; }
        [XmlIgnore]
        public bool HasAuditProcedures { get { return AuditProcedures != null; } }


        [XmlElement( "EnvironmentFolder" )]
        public string Environment { get; set; }
        public string ApplicationName { get; set; }

        public ScriptFolder ScriptFolder { get; set; }
        public string ScriptCacheFolder { get; set; }
        public string ScriptStagedFolder { get; set; }
        public string ScriptProcessedFolder { get; set; }
        //public string ScriptExecutionFolder { get; set; }
        public string SchemaFolder { get; set; }

        public DatabaseInstances DatabaseInstances { get; set; }

        internal ConnectionSettingsCollection ConnectionSettings { get; set; }

        public string RequestNumber { get; set; }
        public string PackageAdapterInstanceId { get; set; }


        [XmlIgnore]
        public bool IsValid { get; protected set; }
        public void InitIsValid()
        {
            IsValid = true;
            if (!SetQuotedIdentifier.HasValue)
            { SetQuotedIdentifier = false; }
            if (!string.IsNullOrWhiteSpace(NLS_LANG))
            { NLS_LANG = "AMERICAN_AMERICA.US7ASCII"; }
        }

        public virtual void PrepareAndValidate()
        {
            PrepareAndValidateScriptFolder();
            PrepareAndValidateSections();
            PrepareAndValidateConnections();
        }

        public virtual void PrepareAndValidateScriptFolder()
        {
            ScriptFolder.Resolve();
        }

        public virtual bool PrepareAndValidateSections()
        {
            Sections = new WorkflowSections();

            if( Directory.Exists( ScriptFolder.SourcePath ) )
            {
                PreworkManifestFile = Path.Combine( ScriptFolder.SourcePath, _preworkManifestFile );
                RollInManifestFile = Path.Combine( ScriptFolder.SourcePath, _rollInManifestFile );
                RollBackManifestFile = Path.Combine( ScriptFolder.SourcePath, _rollBackManifestFile );
                PostworkManifestFile = Path.Combine( ScriptFolder.SourcePath, _postworkManifestFile );
                WorkflowSectionsManifestFile = Path.Combine( ScriptFolder.SourcePath, _workflowSectionsManifestFile );
            }
            else
            {
                IsValid = false;
                return false;
            }


            if( File.Exists( WorkflowSectionsManifestFile, PathFormat.FullPath ) )//HasWorkflowSectionsManifestFile
            {
                Sections = WorkflowSections.LoadAndValidateManifestFile( WorkflowSectionsManifestFile );
            }
            else
            {
                Sections = WorkflowSections.LoadAndValidateManifestFile(
                    PreworkManifestFile, RollInManifestFile, RollBackManifestFile, PostworkManifestFile,
                    ContinueOnError, ShowSqlInOutput, SetQuotedIdentifier.Value);
            }
            Sections.NameSections();
            IsValid &= Sections.IsValid;

            return Sections.IsValid;
        }

        public virtual bool PrepareAndValidateConnections()
        {
            ConnectionSettings = new ConnectionSettingsCollection();

            switch( DatabaseType )
            {
                case DatabaseType.Oracle:
                {
                    ConnectionSettings = this.DatabaseInstances.ToOracleConnectionSettings();
                    break;
                }
                case DatabaseType.SqlServer:
                {
                    ConnectionSettings = this.DatabaseInstances.ToSqlServerConnectionSettings();
                    break;
                }
            }

            IsValid &= ConnectionSettings.IsValid;
            return ConnectionSettings.IsValid;
        }


        public virtual void Serialize(string filePath)
        {
            Utils.Serialize<WorkflowParameters>( this, true, filePath );
        }

        public virtual String Serialize(bool indented = true)
        {
            return Utils.Serialize<WorkflowParameters>( this, indented );
        }

        public static WorkflowParameters Deserialize(string filePath)
        {
            return Utils.DeserializeFile<WorkflowParameters>( filePath );
        }

        public static WorkflowParameters Deserialize(XmlElement el)
        {
            XmlSerializer s = new XmlSerializer( typeof( WorkflowParameters ) );
            return (WorkflowParameters)s.Deserialize( new System.IO.StringReader( el.OuterXml ) );
        }


        public WorkflowParameters FromXmlElement(XmlElement el)
        {
            XmlSerializer s = new XmlSerializer( typeof( WorkflowParameters ) );
            return (WorkflowParameters)s.Deserialize( new System.IO.StringReader( el.OuterXml ) );
        }
    }


    [Serializable()]
    public class AuditProcedure
    {
        [XmlAttribute]
        public string PreRun { get; set; }
        [XmlIgnore]
        public bool HasPreRun { get { return !string.IsNullOrWhiteSpace( PreRun ); } }
        [XmlAttribute]
        public string PostRun { get; set; }
        [XmlIgnore]
        public bool HasPostRun { get { return !string.IsNullOrWhiteSpace( PostRun ); } }
    }


    //note from Steve: this class layout is awkward, but I didn't change it in order
    //to not have to overhaul the existing packages.  I abstracted it through ConnectionSettingsCollection.

    [Serializable()]
    public class DatabaseInstances
    {
        [XmlElement]
        public List<DatabaseInstance> DatabaseInstance { get; set; }

        public ConnectionSettingsCollection ToOracleConnectionSettings()
        {
            //optimistic initialization (IsValid = true)
            ConnectionSettingsCollection list = new ConnectionSettingsCollection()
            {
                IsValid = true
            };

            foreach( DatabaseInstance i in this.DatabaseInstance )
            {
                foreach( Database db in i.Database )
                {
                    OracleConnectionSettings cs = new OracleConnectionSettings()
                    {
                        Instance = i.Name,
                        Database = db.Name,
                        //Schema = db.Schema.Name,
                        Port = i.PortNumber
                    };
                    foreach( string schema in db.Schema )
                    {
                        cs.Schema.Add( schema );
                    }


                    cs.Validate();
                    if( !cs.IsValid ) { list.IsValid = false; }

                    list.Add( cs );
                }
            }

            if( list.Count == 0 )
            {
                list.IsValid = false;
            }

            return list;
        }

        public ConnectionSettingsCollection ToSqlServerConnectionSettings()
        {
            //optimistic initialization (IsValid = true)
            ConnectionSettingsCollection list = new ConnectionSettingsCollection()
            {
                IsValid = true
            };

            foreach( DatabaseInstance i in this.DatabaseInstance )
            {
                foreach( Database db in i.Database )
                {
                    SqlServerConnectionSettings cs = new SqlServerConnectionSettings()
                    {
                        Instance = i.Name,
                        Database = db.Name
                    };

                    cs.Validate();
                    if( !cs.IsValid ) { list.IsValid = false; }

                    list.Add( cs );
                }
            }

            if( list.Count == 0 )
            {
                list.IsValid = false;
            }

            return list;
        }
    }

    [Serializable()]
    public class DatabaseInstance
    {
        [XmlAttribute]
        public string Name { get; set; }
        [XmlElement]
        public List<Database> Database { get; set; }
        [XmlAttribute]
        public string PortNumber { get; set; }
    }

    [Serializable()]
    public class Database
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlArrayItem( "Name" )]
        public List<string> Schema { get; set; }
    }

    [Serializable()]
    public class Schema
    {
        [XmlAttribute]
        public string Name { get; set; }
    }

    public enum ScriptFolderType
    {
        DiskPackage,
        DiskStatic,
        Nostromo
    }

    [Serializable()]
    public class ScriptFolder
    {
        [XmlText()]
        public string Name { get; set; }

        [XmlAttribute()]
        public string ContainerName { get; set; }

        [XmlAttribute()]
        public ScriptFolderType Type { get; set; }

        [XmlIgnore()]
        public string WorkRoot { get; set; }

        [XmlIgnore()]
        public string SourcePath { get; set; }

        public void Resolve()
        {
            switch( Type )
            {
                case ScriptFolderType.DiskPackage:
                {
                    WorkRoot = Name;
                    break;
                }
                case ScriptFolderType.DiskStatic:
                {
                    WorkRoot = Name;
                    SourcePath = Path.Combine( WorkRoot, ContainerName );
                    break;
                }
                case ScriptFolderType.Nostromo:
                {
                    string url = settings.Default.NostromoApi;
                    nostromo.NostromoApiClient apiClient = new nostromo.NostromoApiClient( url );
                    nostromo.Bucket bucket = apiClient.GetBucket( ContainerName );

                    WorkRoot = bucket.Region.Location;
                    ContainerName = bucket.BucketRoot.Replace( bucket.Region.Location, string.Empty ).Trim( '\\' );
                    ContainerName = Path.Combine( ContainerName, Name );

                    //commented by steve as not in use and confusing
                    //SourcePath = bucket.BucketRoot;
                    break;
                }
            }
        }

        public override string ToString()
        {
            return string.Format( "ScriptFolder Name: {0}, ContainerName: {1}, Type: {2}, WorkRoot: {3}, SourcePath: {4}",
                Name, ContainerName, Type, WorkRoot, SourcePath );
        }
    }
}