using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

using Alphaleonis.Win32.Filesystem;

namespace Synapse.Handlers.Legacy.Database
{
    public class Section
    {
        public Section()
        {
            Name = string.Empty;
            Success = true;
            FailWorkflowOnError = true;
            Scripts = new List<Script>();
            IsValid = true;
        }

        [XmlAttribute]
        public bool FailWorkflowOnError { get; set; }

        [XmlElement( "Script" )]
        public List<Script> Scripts { get; set; }

        [XmlIgnore]
        internal string Name { get; set; }
        [XmlIgnore]
        internal bool Success { get; set; }
        [XmlIgnore]
        public bool IsValid { get; internal set; }
        [XmlIgnore]
        public bool HasScripts { get { return Scripts != null ? Scripts.Count > 0 : false; } }



        /// <summary>
        /// Checks for the existence of the manifest file, and loads/validates each entry.
        /// </summary>
        /// <param name="section">The section to load.</param>
        /// <param name="manifestFile">Path the manifest file to load into the section.</param>
        /// <returns>Returns true is the manifest file and every entry in it exists, false otherwise.</returns>
        public void LoadAndValidateManifestFile(string manifestFile, bool failOnError, bool showSqlInOutput, bool setQuotedIdentifier)
        {
            IsValid = true;

            if( File.Exists( manifestFile ) )
            {
                string folderPath = Path.GetDirectoryName( manifestFile );
                string[] paths = File.ReadAllLines( manifestFile );
                foreach( string path in paths )
                {
                    bool exists = false;
                    string schema = string.Empty;
                    string metadata = string.Empty;
                    string file = path.Trim();

                    if( !string.IsNullOrWhiteSpace( file ) )
                    {
                        Script s = new Script()
                        {
                            Path = Utils.FormatPath( folderPath, path, out schema, out metadata, out exists ),
                            IsValid = exists,
                            Schema = schema,
                            MetaData = metadata,
                            ShowSqlInOutput = showSqlInOutput,
                            FailBranchOnError = failOnError,
                            FailSectionOnError = failOnError,
                            SetQuotedIdentifier = setQuotedIdentifier
                        };
                        Scripts.Add( s );

                        if( !s.IsValid ) { IsValid = false; }
                    }
                }
            }
        }

        public override string ToString()
        {
            return string.Format( "IsValid:{0}, Success:{1}, FailWorkflowOnError:{2}",
                IsValid, Success, FailWorkflowOnError );
        }
    }
}