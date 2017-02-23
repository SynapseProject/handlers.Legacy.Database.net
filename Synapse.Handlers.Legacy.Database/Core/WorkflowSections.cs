using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using settings = Synapse.Handlers.Legacy.Database.Properties.Settings;
using Alphaleonis.Win32.Filesystem;

namespace Synapse.Handlers.Legacy.Database
{
	public class WorkflowSections
	{
		public WorkflowSections()
		{
			PreWork = new Section();
			RollIn = new Section();
			RollBack = new Section();
			PostWork = new Section();
		}

		internal Section PreWork { get; set; }
		public Section RollIn { get; set; }
		public Section RollBack { get; set; }
		internal Section PostWork { get; set; }

		[XmlIgnore]
		public bool IsValid
        {
            get
            {
                return PreWork.IsValid && RollIn.IsValid && RollBack.IsValid && PostWork.IsValid &&
                    (PreWork.HasScripts || RollIn.HasScripts || RollBack.HasScripts || PostWork.HasScripts);
            }
        }

		[XmlIgnore]
		internal bool WorkflowSuccess { get; set; }


		internal List<Section> Sections { get { return new List<Section>() { PreWork, RollIn, RollBack, PostWork }; } }

		/// <summary>
		/// Sets the Name property of the sections to facilitate messages
		/// </summary>
		internal void NameSections()
		{
			PreWork.Name = "PreWork";
			RollIn.Name = "RollIn";
			RollBack.Name = "RollBack";
			PostWork.Name = "PostWork";
		}

		/// <summary>
		/// Loads a WorkflowSections object one file at a time (PreWork, RollIn, RollBack, and PostWork)
		/// </summary>
		/// <param name="preworkFilePath">Path to the PreWork file to load</param>
		/// <param name="rollinFilePath">Path to the RollIn file to load</param>
		/// <param name="rollbackFilePath">Path to the RollBack file to load</param>
		/// <param name="postworkFilePath">Path to the PostWork file to load</param>
		/// <returns>A validated WorkflowSections object</returns>
		public static WorkflowSections LoadAndValidateManifestFile(string preworkFilePath,
			string rollinFilePath, string rollbackFilePath, string postworkFilePath,
			bool continueOnError, bool showSqlInOutput, bool setQuotedIdentifier)
		{
			WorkflowSections ws = new WorkflowSections();
			bool failOnError = !continueOnError;

			if( !string.IsNullOrWhiteSpace( preworkFilePath ) )
			{
				ws.PreWork.LoadAndValidateManifestFile( preworkFilePath, failOnError, showSqlInOutput, setQuotedIdentifier );
			}
			if( !string.IsNullOrWhiteSpace( rollinFilePath ) )
			{
				ws.RollIn.LoadAndValidateManifestFile( rollinFilePath, failOnError, showSqlInOutput, setQuotedIdentifier);
			}
			if( !string.IsNullOrWhiteSpace( rollbackFilePath ) )
			{
				ws.RollBack.LoadAndValidateManifestFile( rollbackFilePath, failOnError, showSqlInOutput, setQuotedIdentifier);
			}
			if( !string.IsNullOrWhiteSpace( postworkFilePath ) )
			{
				ws.PostWork.LoadAndValidateManifestFile( postworkFilePath, failOnError, showSqlInOutput, setQuotedIdentifier);
			}


            //note: at least some file should exist, and the file should have content
            //optimistic initialization immed following section load, assuming ws.IsValid = true
            ws.WorkflowSuccess = ws.IsValid;

			return ws;
		}

		/// <summary>
		/// Loads the Xml manifest that contains the PreWork, RollIn, RollBack, and PostWork sections
		/// </summary>
		/// <param name="filePath">Path to the file to load</param>
		/// <returns>A validated WorkflowSections object</returns>
		public static WorkflowSections LoadAndValidateManifestFile(string filePath)
		{
			WorkflowSections ws = new WorkflowSections();

            //note: ws.WorkflowSuccess will equal false if !File.Exists, thus failing workflow as desired
            if( File.Exists( filePath ) )
            {
                string folderPath = Path.GetDirectoryName( filePath );
                ws = Utils.DeserializeFile<WorkflowSections>( filePath );
                foreach( Section section in ws.Sections )
                {
                    ValidateSection( section, folderPath );
                }

                //optimistic initialization immed following section load
                ws.WorkflowSuccess = true;
            }
            else
            {
                //workflow sections have optimistic initialization, invalidate them if manifest does not exist
                ws.PreWork.IsValid =
                    ws.RollIn.IsValid =
                    ws.RollBack.IsValid =
                    ws.PostWork.IsValid = false;
            }

            return ws;
		}

		/// <summary>
		/// Supports recusively validating File.Exists(script.Path) and loading SuccessScripts/FailScripts
		/// </summary>
		/// <param name="section">The section to validate.</param>
		static void ValidateSection(Section section, string folderPath)
		{
			bool isValid = true;
			if( section.Scripts.Count != 0 )
			{
				foreach( Script s in section.Scripts )
				{
					bool exists = false;
					//pass in schema & metadata parms, but don't assign, they should come from xml
					string schema = string.Empty;
					string metadata = string.Empty;
					s.Path = Utils.FormatPath( folderPath, s.Path, out schema, out metadata, out exists );
					s.IsValid = exists;
					if( !s.IsValid ) { isValid = false; }

					RecurseScriptsForLoad( s, folderPath, ref isValid );
				}
			}

			section.IsValid = isValid;
		}

		/// <summary>
		/// Recurses script.Scripts to load SuccessScripts/FailScripts
		/// </summary>
		/// <param name="parent">The script for which to iterate the Scripts list</param>
		/// <param name="isValid">Floating tracker of overall Section.success</param>
		static void RecurseScriptsForLoad(Script parent, string folderPath, ref bool isValid)
		{
			foreach( Script s in parent.Scripts )
			{
				bool exists = false;
				//pass in schema & metadata parms, but don't assign, they should come from xml
				string schema = string.Empty;
				string metadata = string.Empty;
				s.Path = Utils.FormatPath( folderPath, s.Path, out schema, out metadata, out exists );
				s.IsValid = exists;
				if( !s.IsValid ) { isValid = false; }

				if( s.Condition == ConditionType.Success )
				{
					parent.SuccessScripts.Add( s );
				}
				else
				{
					parent.FailScripts.Add( s );
				}

				RecurseScriptsForLoad( s, folderPath, ref isValid );
			}
		}



		/// <summary>
		/// Supports recusively adding the SqlServer impersonation sql
		/// </summary>
		public void RewriteScripts()
		{
            string resPath = settings.Default.ImpersonationFilePath;
			foreach( Section section in this.Sections )
			{
				foreach( Script s in section.Scripts )
				{
					Utils.MergeImpersonationScript( resPath, s.Path );
					RecurseScriptsForRewrite( s, resPath );
				}
			}
		}

		/// <summary>
		/// Recurses script.Scripts to load SuccessScripts/FailScripts
		/// </summary>
		/// <param name="parent">The script for which to iterate the Scripts list</param>
		static void RecurseScriptsForRewrite(Script parent, string resPath)
		{
			foreach( Script s in parent.Scripts )
			{
				Utils.MergeImpersonationScript( resPath, s.Path );
				RecurseScriptsForRewrite( s, resPath );
			}
		}

		public void Serialize(string filePath)
		{
			Utils.Serialize<WorkflowSections>( this, true, filePath );
		}

		public static WorkflowSections Deserialize(string filePath)
		{
			return Utils.DeserializeFile<WorkflowSections>( filePath );
		}

		public string ToXml(bool indent)
		{
			return Utils.Serialize<WorkflowSections>( this, indent );
		}

		public override string ToString()
		{
			//return string.Format( "IsValid: PreWork:{0}, RollIn:{1}, RollBack:{2}, PostWork:{3}",
			//	PreWork.IsValid, RollIn.IsValid, RollBack.IsValid, PostWork.IsValid );
			return string.Format( "IsValid: RollIn:{0}, RollBack:{1}",
				RollIn.IsValid, RollBack.IsValid );
		}
	}
}