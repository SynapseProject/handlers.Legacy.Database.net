using System;
using System.ComponentModel;

namespace Synapse.Handlers.Legacy.Database
{
	public enum DatabaseType
	{
		Unknown,
		Oracle,
		SqlServer
	}

	public enum ConditionType
	{
		Success,
		Fail
	}

	public enum ExecutionStatus
	{
		[Description( "Not Executed" )]
		NotExecuted,
		[Description( "Executed" )]
		Executed
	}

	public enum MigrationType
	{
		[Description( "For testing on local machine" )]
		Local,
		[Description( "Non-Production Deployment" )]
		NonProd,
		[Description( "Pre-Production Deployment" )]
		PreProd,
		[Description( "Production Deployment" )]
		Prod
	}
}