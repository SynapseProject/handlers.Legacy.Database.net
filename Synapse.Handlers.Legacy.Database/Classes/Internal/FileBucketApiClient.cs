using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

using Synapse.Internal.Api.Client;

namespace Synapse.Internal.Nostromo
{
	[DataContract]
	public class Bucket
	{
		[DataMember]
		public string Name { get; set; }
		[DataMember]
		public BucketRegion Region { get; set; }
		//[DataMember]
		//public string InternalName { get; set; }
        [DataMember]
        public string BucketRoot { get; set; }
    }

    public class BucketRegion
	{
		[DataMember]
		public string Name { get; set; }
		public string Type { get; set; }
		public string Location { get; set; }
	}

	public class NostromoApiClient : ApiClientBase
	{
		public NostromoApiClient(string baseUrl) : base( baseUrl, WebMessageFormatType.Json ) { }
		public NostromoApiClient(string baseUrl, WebMessageFormatType webMessageFormatType) : base( baseUrl, webMessageFormatType ) { }

		public event EventHandler<AsyncCallCompletedEventArgs<Bucket>> GetBucketAsyncCompleted;

		public Bucket GetBucket(string id)
		{
			Uri url = new Uri( string.Format( "{0}/buckets/{1}", BaseUrl, id ) );
			return this.WebRequestSync<Bucket>( url, HttpMethod.Get );
		}

		public void GetBucketAsync(string id, object state)
		{
			Uri url = new Uri( string.Format( "{0}/buckets/{1}", BaseUrl, id ) );
			base.WebRequestAsync( url, state, GetBucketAsyncCompleted );
		}
	}
}