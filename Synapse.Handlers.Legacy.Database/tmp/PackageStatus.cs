using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sentosa.CommandCenter.Api.Client
{
	public enum PackageStatus
	{
		Running,
		Failed,
		Complete
	}

	public enum CommandCenterAction
	{
		Start,
		Cancel
	}
}