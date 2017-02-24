using System;
using System.Xml.Serialization;
using System.Runtime.Serialization;

namespace Synapse.Internal.Api.Client
{
	[Obsolete( "This should never have been done in the first place.", false )]
	[DataContract( Namespace = "" )]
    [KnownType(typeof(Code))]
    [KnownType(typeof(Reason))]
    [KnownType(typeof(Detail))]
    public class Fault
    {

        [DataMember(Order = 1)]
        [XmlElement(Order = 1)]
        public Code Code { get; set; }

        [DataMember(Order = 2)]
        [XmlElement(Order = 2)]
        public string Message { get; set; }

        [DataMember(Order = 3)]
        [XmlElement(Order = 3)]
        public Reason Reason { get; set; }

        [DataMember(Order = 4)]
        [XmlElement(Order = 4)]
        public Detail Detail { get; set; }
    }

	[Obsolete( "This should never have been done in the first place.", false )]
	[DataContract]
    public class Reason
    {
        public string Text { get; set; }
    }

	[Obsolete( "This should never have been done in the first place.", false )]
	[DataContract]
    public class Code
    {
        [DataMember]
        public string Value { get; set; }

        [DataMember]
        public Code SubCode { get; set; }
    }

	[Obsolete( "This should never have been done in the first place.", false )]
	[DataContract( Namespace = "" )]
    public class Detail
    {
        [DataMember]
        public string ContentType { get; set; }

        [DataMember]
        public string Url { get; set; }

        [DataMember]
        public string ErrorCode { get; set; }

        [DataMember]
        public string Message { get; set; }

        public string MergeInnerExceptionMessage(Exception ex)
        {
            if (ex.InnerException == null)
                return Message;

            this.Message += string.Format("| {{ Inner Exception:{0} }}", ex.InnerException.Message);
            return MergeInnerExceptionMessage(ex.InnerException);
        }

    }
}