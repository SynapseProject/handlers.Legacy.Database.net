using System;
using System.Runtime.Serialization;

namespace Synapse.Internal.Api.Client
{
	[Obsolete( "Do not use this class further: replace with VoidObject.", false )]
    [DataContract(Namespace = "")]
    public class NullResult
    {
    }
}