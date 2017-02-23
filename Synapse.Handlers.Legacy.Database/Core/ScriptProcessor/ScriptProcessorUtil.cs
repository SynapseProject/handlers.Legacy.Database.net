using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Synapse.Handlers.Legacy.Database
{
	public class ScriptProcessorUtil
	{
		/// <summary>
		/// Initializes a new instance of a script processor.
		/// </summary>
		/// <param name="databaseType">The type for which to create a script processor</param>
		/// <returns>A new instance of a script processor.</returns>
		public static IScriptProcessor GetProcessorInstance(DatabaseType databaseType)
		{
			IScriptProcessor sp = null;

			switch(databaseType)
			{
				case DatabaseType.Oracle:
				{
					sp = new OracleScriptProcessor();
					break;
				}
				case DatabaseType.SqlServer:
				{
					sp = new SqlServerScriptProcessor();
					break;
				}
			}

			return sp;
		}

		/// <summary>
		/// Initializes a new System.Diagnostics.Process instance with standard setteings.
		/// </summary>
		/// <param name="filename">Populates StartInfo.FileName</param>
		/// <param name="arguments">Populates StartInfo.Arguments</param>
		/// <returns>System.Diagnostics.Process instance</returns>
		public static Process CreateProcess(string filename, string arguments)
		{
			Process p = new Process();
			p.StartInfo.FileName = filename;
			p.StartInfo.Arguments = arguments;
			p.StartInfo.RedirectStandardInput = true;
			p.StartInfo.RedirectStandardOutput = true;
			p.StartInfo.RedirectStandardError = true;
			p.StartInfo.UseShellExecute = false;
			p.StartInfo.CreateNoWindow = true;
			p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.EnableRaisingEvents = true;

			return p;
		}

		/// <summary>
		/// Replaces all global variables with values from dictionary.
		/// </summary>
		/// <param name="path">Path to the input file.</param>
		/// <param name="variables">Dictionary of global variables.</param>
		/// <returns>Path to output file (temp file).</returns>
		public static string ReplaceVariablesToTempFile(string path, Dictionary<string,string> variables)
		{
			string file = File.ReadAllText( path );

			foreach( string key in variables.Keys )
			{
				//Regex.Replace( file, key, variables[key] );
				file = file.Replace( key, variables[key] );
			}

			string fileName = Path.GetFileNameWithoutExtension( path );
			string newfile = Path.Combine( Path.GetDirectoryName( path ),
				string.Format( "{0}_{1}", fileName, Path.GetRandomFileName() ) );
			File.WriteAllText( newfile, file );
			return newfile;
		}

		/// <summary>
		/// Replaces all global variables with values from dictionary.
		/// </summary>
		/// <param name="input">String for value replacement.</param>
		/// <param name="variables">Dictionary of global variables.</param>
		/// <returns>Path to output file (temp file).</returns>
		public static string ReplaceVariablesToString(string input, Dictionary<string, string> variables)
		{
			string s = input;
			foreach( string key in variables.Keys )
			{
				//Regex.Replace( file, key, variables[key] );
				s = s.Replace( key, variables[key] );
			}

			return s;
		}
	}
}