using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace Synapse.Handlers.Legacy.Database
{
	public class Script
	{
		public Script()
		{
			Success = true;
			ExecutionStatus = ExecutionStatus.NotExecuted;
			FailBranchOnError = true;
			FailSectionOnError = true;
			Scripts = new List<Script>();
			SuccessScripts = new List<Script>();
			FailScripts = new List<Script>();
		}


		[XmlAttribute]
		public ConditionType Condition { get; set; }
		[XmlAttribute]
		public string Path { get; set; }
		[XmlAttribute]
		public string Schema { get; set; }
		[XmlAttribute]
		public string MetaData { get; set; }
		[XmlAttribute]
		public bool FailBranchOnError { get; set; }
		[XmlAttribute]
		public bool FailSectionOnError { get; set; }
		[XmlAttribute]
		public bool ShowSqlInOutput { get; set; }
        [XmlAttribute]
        public bool SetQuotedIdentifier { get; set; }


        [XmlElement( "Script" )]
		public List<Script> Scripts { get; set; }

		[XmlIgnore]
		internal bool Success { get; set; }
		[XmlIgnore]
		internal ExecutionStatus ExecutionStatus { get; set; }
		[XmlIgnore]
		internal bool IsValid { get; set; }
		[XmlIgnore]
		internal List<Script> SuccessScripts { get; set; }
		[XmlIgnore]
		internal List<Script> FailScripts { get; set; }

		public override string ToString()
		{
			return string.Format( "Path:{0}, Schema{1}, IsValid:{2}, Success:{3}, FailBranchOnError:{4}, FailSectionOnError:{5}",
				Path, Schema, IsValid, Success, FailBranchOnError, FailSectionOnError );
		}
	}
}