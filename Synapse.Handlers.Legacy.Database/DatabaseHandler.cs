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

        Workflow wf = new Workflow(wfp);

        wf.OnLogMessage = this.OnLogMessage;
        wf.OnProgress = this.OnProgress;

        seqNo = 0;
        OnProgress("Execute", "Starting", StatusType.Running, startInfo.InstanceId, seqNo++);
        wf.ExecuteAction();

        return new ExecuteResult() { Status = StatusType.Complete };
    }

    public override object GetConfigInstance()
    {
        return null;
    }

    public override object GetParametersInstance()
    {
        WorkflowParameters wfp = new WorkflowParameters();

        wfp.DatabaseType = DatabaseType.Oracle;
        wfp.ContinueOnError = true;
        wfp.ShowSqlInOutput = true;
        wfp.SetQuotedIdentifier = true;
        wfp.IsTraditional = true;
        wfp.MigrationType = MigrationType.Prod;
        wfp.IsControlledMigration = true;
        wfp.SuppressSqlServerImpersonation = true;
        wfp.NLS_LANG = "American_America.UTF8";

        wfp.AuditProcedures = new AuditProcedure();
        wfp.AuditProcedures.PostRun = @"value";
        wfp.AuditProcedures.PreRun = @"value";

        wfp.Environment = "Development";
        wfp.ApplicationName = "MyApplication";

        wfp.ScriptFolder = new ScriptFolder();
        wfp.ScriptFolder.ContainerName = "Container";
        wfp.ScriptFolder.Name = "Name";
        wfp.ScriptFolder.SourcePath = @"C:\Scripts\";
        wfp.ScriptFolder.Type = ScriptFolderType.DiskStatic;
        wfp.ScriptFolder.WorkRoot = "WorkRoot";

        wfp.ScriptCacheFolder = @"C:\Scripts\CacheFolder";
        wfp.ScriptStagedFolder = @"C:\Scripts\Staged";
        wfp.ScriptProcessedFolder = @"C:\Scripts\Processed";
        wfp.SchemaFolder = @"C:\Scripts\Schema";

        wfp.DatabaseInstances = new DatabaseInstances();
        wfp.DatabaseInstances.DatabaseInstance = new List<DatabaseInstance>();
        DatabaseInstance instance = new DatabaseInstance();
        instance.Name = "MyDatabaseInstnace";
        instance.Database = new List<Database>();
        Database database = new Database();
        database.Name = "MyDatabaseName";
        database.Schema = new List<string>();
        database.Schema.Add( "MyDatabaseSchema1" );
        database.Schema.Add( "MyDatabaseSchema2" );
        instance.Database.Add( database );

        instance.PortNumber = "1521";
        wfp.DatabaseInstances.DatabaseInstance.Add( instance );

        wfp.RequestNumber = "12345678";
        wfp.PackageAdapterInstanceId = "87654321";

        String xml = wfp.Serialize( false );
        xml = xml.Substring( xml.IndexOf( "<" ) );
        return xml;
    }
}
