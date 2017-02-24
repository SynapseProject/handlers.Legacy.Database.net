using System;
using System.Runtime.Serialization;

// *** Stop ***
// Do not, under any circumstances, add or modify anything in this file without
// consulting Steve.  No more inventing junk solutions.
// If I find an unauthorized commit, I'll roll-back the code and push the broken binaries to Prod.
// Consider yourself warned.

namespace Synapse.Internal.Api.Client
{
	public enum WebMessageFormatType
	{
		Json,
		Xml
	}

	public struct HttpMethod
	{
		public const string Create = "POST";
		public const string Update = "PUT";
		public const string Delete = "DELETE";
		public const string Select = "GET";
		public const string Post = "POST";
		public const string Put = "PUT";
		public const string Get = "GET";
		public const string Head = "HEAD";
		public const string Trace = "TRACE";
		public const string Options = "OPTIONS";
	}

}