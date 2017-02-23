using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Text.RegularExpressions;

using System.Security.Cryptography.Utility;

using fs = Alphaleonis.Win32.Filesystem;

using config = Synapse.Handlers.Legacy.Database.Properties.Settings;
using System.Reflection;

namespace Synapse.Handlers.Legacy.Database
{
	internal static class Utils
	{
		/// <summary>
		/// Checks for the existence of the file and returns the full path to file.
		/// </summary>
		/// <param name="file">Path to file to validate.</param>
		/// <param name="exists">Returns reslt from File.Exists( path )</param>
		/// <returns>Returns  if it exists.</returns>
		public static string FormatPath(string folderPath, string file, out string schema, out string metadata, out bool exists)
        {
            schema = string.Empty;
            metadata = string.Empty;
            if (file.Contains("*"))
            {
                string[] parts = file.Split('*');
                schema = parts[0];
                file = parts[1];
            }
            if (file.Contains("|"))
            {
                string[] parts = file.Split('|');
                file = parts[0];
                metadata = parts[1];
            }

            file = Path.Combine(folderPath, file);
            exists = fs.File.Exists(file);
            if (exists)
            {
                //path = new FileInfo( path ).FullName;
                file = fs.Path.GetFullPath(file);
            }
            return file;
        }

		public static void MergeImpersonationScript(string impersonationFilePath, string scriptFilePath)
		{
            FileAttributes attributes = File.GetAttributes( scriptFilePath );
            if( (attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly )
            {
                // Make the file RW
                File.SetAttributes( scriptFilePath, attributes & ~FileAttributes.ReadOnly );
            }

            // this block concatenates the impersonationFile onto the top of scriptFile as follows:
            // 1. ren scriptFile --> scriptFile.tmp
            // 2. create new, empty scriptFile
            // 3. copy contents of impersonationFile & scriptFile.tmp --> scriptFile
            // 4. del scriptFile.tmp
            string tempScriptFile = Path.Combine( Path.GetDirectoryName( scriptFilePath ), Path.GetFileName( Path.GetTempFileName() ) );
            File.Move( scriptFilePath, tempScriptFile );
            FileStream fs = new FileStream( scriptFilePath, FileMode.CreateNew ); ;
            using( StreamWriter destination = new StreamWriter( fs, Encoding.Unicode ) )
            {
                using( StreamReader s = new StreamReader( impersonationFilePath ) )
                    s.CopyToUnicode( destination );
                using( StreamReader s = new StreamReader( tempScriptFile ) )
                    s.CopyToUnicode( destination );
            }
            File.Delete( tempScriptFile );
        }

        public static void CopyToUnicode(this StreamReader source, StreamWriter destination, int bufferSize = 32768)
        {
            string buf = null;
            char[] buffer = new char[bufferSize];
            int read;
            while( (read = source.ReadBlock( buffer, 0, buffer.Length )) != 0 )
            {
                buf = new string( buffer, 0, read );
                destination.Write( buf.Normalize() );
            }
        }

        const string _lines = "--------------------------";
		public static double ElapsedSeconds(this Stopwatch stopwatch)
		{
			return TimeSpan.FromMilliseconds( stopwatch.ElapsedMilliseconds ).TotalSeconds;
		}

		public static string GetMessagePadLeft(string header, object message, int width)
		{
			return string.Format( "{0}: {1}", header.PadLeft( width, '.' ), message );
		}

		public static string GetMessagePadRight(string header, object message, int width)
		{
			return string.Format( "{0}: {1}", header.PadRight( width, '.' ), message );
		}

		public static string GetHeaderMessage(string header)
		{
			return string.Format( "{1}  {0}  {1}", header, _lines );
		}

        public static string CompressXml(string xml)
        {
            string str = Regex.Replace(xml, @"(>\s*<)", @"><");
            return str;
        }

        public static string Decrypt(string value)
		{
			Cipher c = new Cipher( config.Default.PassPhrase, config.Default.SaltValue, config.Default.InitVector );
			return c.Decrypt( value );
		}


		//http://stackoverflow.com/questions/1600962/displaying-the-build-date
		//note: [assembly: AssemblyVersion("1.0.*")] // important: use wildcard for build and revision numbers!
		public static string GetBuildDateVersion()
		{
			Assembly assm = Assembly.GetExecutingAssembly();
			Version version = assm.GetName().Version;
			DateTime buildDateTime = new FileInfo( assm.Location ).LastWriteTime;

			return string.Format( "Version: {0}, Build DateTime: {1}", version, buildDateTime );

			//ToString( "yyMMdd.HHmm" )
//			return string.Format( "{0}.{1}.{2}.{3}", version.Major, version.Minor, buildDateTime.ToString( "yy" ), buildDateTime.DayOfYear.ToString( "D3" ) );
		}


		#region serialize/deserialize
		//stolen from Suplex.General.XmlUtils
		public static void Serialize<T>(object data, string filePath)
		{
			XmlSerializer s = new XmlSerializer( typeof( T ) );
			XmlTextWriter w = new XmlTextWriter( filePath, Encoding.Unicode );
			w.Formatting = Formatting.Indented;
			s.Serialize( w, data );
			w.Close();
		}

		public static string Serialize<T>(object data, bool indented = true, string filePath = null, bool omitXmlDeclaration = true, bool omitXmlNamespace = true)
		{
			XmlWriterSettings settings = new XmlWriterSettings();
			settings.OmitXmlDeclaration = omitXmlDeclaration;
			settings.ConformanceLevel = ConformanceLevel.Auto;
			settings.CloseOutput = true;
			settings.Encoding = Encoding.Unicode;
			settings.Indent = indented;

			MemoryStream ms = new MemoryStream();
			XmlSerializer s = new XmlSerializer( typeof( T ) );
			XmlWriter w = XmlWriter.Create( ms, settings );
			if( omitXmlNamespace )
			{
				XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
				ns.Add( "", "" );
				s.Serialize( w, data, ns );
			}
			else
			{
				s.Serialize( w, data );
			}
			string result = Encoding.Unicode.GetString( ms.GetBuffer(), 0, (int)ms.Length );
			w.Close();

			if( !string.IsNullOrWhiteSpace( filePath ) )
			{
				using( StreamWriter file = new StreamWriter( filePath, false ) )
				{
					file.Write( result );
				}
			}

			return result;
		}

		public static T DeserializeFile<T>(string filePath)
		{
			using( FileStream fs = new FileStream( filePath, FileMode.Open, FileAccess.Read ) )
			{
				XmlSerializer s = new XmlSerializer( typeof( T ) );
				return (T)s.Deserialize( fs );
			}
		}

		public static T DeserializeString<T>(string script)
		{
			XmlSerializer s = new XmlSerializer( typeof( T ) );
			return (T)s.Deserialize( new StringReader( script ) );
		}
		#endregion
	}
}


#region bunk, to be deleted
//File.Copy( impersonationfilePath, scriptFilePath );

//string[] imperFile = File.ReadAllLines(impersonationfilePath);
//string[] scriptFile = File.ReadAllLines(scriptFilePath);

//using (Stream scriptStream = File.Open(scriptFilePath, FileMode.Append))
//{
//    using (Stream tempStream = File.OpenRead(tempScriptFile))
//    {
//        tempStream.CopyTo(scriptStream);
//    }
//}

//File.WriteAllLines(scriptFilePath, imperFile, Encoding.Unicode);         // Overwrites ScriptFile
//File.AppendAllLines(scriptFilePath, scriptFile, Encoding.Unicode);              // Appends To ScriptFile

//string scriptfiletemp = scriptFilePath + "temp";
//File.Move(scriptFilePath, scriptfiletemp);

//using( TextWriter textWriter = new StreamWriter( scriptFilePath, true ) )
//{
//    for( int i = 0; i < imperFile.Length; i++ )
//    {
//        textWriter.WriteLine( imperFile[i] );
//    }
//    for( int j = 0; j < scriptFile.Length; j++ )
//    {
//        textWriter.WriteLine( scriptFile[j] );
//    }
//}

//using( Stream input = File.OpenRead( impersonationfilePath ) )
//{
//	using( Stream output =
//		new FileStream( scriptFilePath, FileMode.Open, FileAccess.Write, FileShare.None ) )
//	{
//		input.CopyTo( output );
//	}
//}
#endregion