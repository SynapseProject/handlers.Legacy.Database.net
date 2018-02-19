using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Serialization;
using System.IO;

using Synapse.Handlers.Legacy.Database;

using Synapse.Core;

public class DatabaseHandler : HandlerRuntimeBase
{
    int seqNo = 0;
    override public ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        XmlSerializer ser = new XmlSerializer(typeof(WorkflowParameters));
        WorkflowParameters wfp = new WorkflowParameters();
        TextReader reader = new StringReader(startInfo.Parameters);
        wfp = (WorkflowParameters)ser.Deserialize(reader);

        Workflow wf = new Workflow( wfp )
        {
            OnLogMessage = this.OnLogMessage,
            OnProgress = this.OnProgress
        };

        seqNo = 0;
        OnProgress( "Execute", "Starting", StatusType.Running, startInfo.InstanceId, seqNo++ );
        wf.Initialize( ref wfp, startInfo );
        wf.ExecuteAction();

        return new ExecuteResult() { Status = StatusType.Complete };
    }

    public override object GetConfigInstance()
    {
        return null;
    }

    public override object GetParametersInstance()
    {
        WorkflowParameters wfp = new WorkflowParameters
        {
            DatabaseType = DatabaseType.Oracle,
            ContinueOnError = true,
            ShowSqlInOutput = true,
            SetQuotedIdentifier = true,
            IsTraditional = true,
            MigrationType = MigrationType.Prod,
            IsControlledMigration = true,
            SuppressSqlServerImpersonation = true,
            NLS_LANG = "American_America.UTF8",

            AuditProcedures = new AuditProcedure()
        };
        wfp.AuditProcedures.PostRun = @"value";
        wfp.AuditProcedures.PreRun = @"value";

        wfp.Environment = "Development";
        wfp.ApplicationName = "MyApplication";

        wfp.ScriptFolder = new ScriptFolder
        {
            ContainerName = "Container",
            Name = "Name",
            SourcePath = @"C:\Scripts\",
            Type = ScriptFolderType.DiskStatic,
            WorkRoot = "WorkRoot"
        };

        wfp.ScriptCacheFolder = @"C:\Scripts\CacheFolder";
        wfp.ScriptStagedFolder = @"C:\Scripts\Staged";
        wfp.ScriptProcessedFolder = @"C:\Scripts\Processed";
        wfp.SchemaFolder = @"C:\Scripts\Schema";

        wfp.DatabaseInstances = new DatabaseInstances
        {
            DatabaseInstance = new List<DatabaseInstance>()
        };
        DatabaseInstance instance = new DatabaseInstance
        {
            Name = "MyDatabaseInstnace",
            Database = new List<Database>()
        };
        Database database = new Database
        {
            Name = "MyDatabaseName",
            Schema = new List<string>()
        };
        database.Schema.Add( "MyDatabaseSchema1" );
        database.Schema.Add( "MyDatabaseSchema2" );
        instance.Database.Add( database );

        instance.PortNumber = "1521";
        wfp.DatabaseInstances.DatabaseInstance.Add( instance );

        wfp.RequestNumber = "12345678";
        wfp.PackageAdapterInstanceId = "87654321";

        string xml = wfp.Serialize( indented: true );
        xml = xml.Replace( "\r\n", "\n" ); //this is only to make the XML pretty, like me
        return xml;
    }
}