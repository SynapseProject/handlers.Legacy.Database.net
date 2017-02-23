using System;
using System.Collections.Generic;

namespace Synapse.Handlers.Legacy.Database
{
	public interface IConnectionSettings
	{
		string Instance { get; set; }
		string Database { get; set; }

		bool IsValid { get; }
		string ErrorMessage { get; set; }

		void Validate();
	}

	public class ConnectionSettingsCollection : List<IConnectionSettings>
	{
		public bool IsValid { get; internal set; }
	}


	public class OracleConnectionSettings : IConnectionSettings
	{
        public OracleConnectionSettings()
        {
            Schema = new List<string>();
        }

		public string Instance { get; set; }
		public string Database { get; set; }
		public List<string> Schema { get; set; }
		public string Port { get; set; }

		public bool IsValid { get; internal set; }
		public string ErrorMessage { get; set; }

		public void Validate()
		{
			ConnectionValidationResult r =
				OracleScriptProcessor.ValidateConnectionSettings( this );
			IsValid = r.IsValid;
			ErrorMessage = r.Message;
		}

		public override string ToString()
		{
			return string.Format( "Instance:{0}, Database:{1}, Schema:{2}, Port:{3}, IsValid:{4}, Message:{5}", Instance, Database, string.Join( ";", Schema ), Port, IsValid, ErrorMessage );
		}
	}

	public class SqlServerConnectionSettings : IConnectionSettings
	{
		public string Instance { get; set; }
		public string Database { get; set; }

		public bool IsValid { get; internal set; }
		public string ErrorMessage { get; set; }

		public void Validate()
		{
			ConnectionValidationResult r =
				SqlServerScriptProcessor.ValidateConnectionSettings( this );
			IsValid = r.IsValid;
			ErrorMessage = r.Message;
		}

		public override string ToString()
		{
			return string.Format( "Instance:{0}, Database:{1}, IsValid:{2}, Message:{3}", Instance, Database, IsValid, ErrorMessage );
		}
	}
}