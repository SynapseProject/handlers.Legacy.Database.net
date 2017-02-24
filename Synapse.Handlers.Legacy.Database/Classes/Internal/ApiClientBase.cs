using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.ServiceModel;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Synapse.Internal.Api.Client
{
	public abstract class ApiClientBase
	{
		public string BaseUrl { get; set; }
		public WebMessageFormatType MessageFormat { get; set; }
		public int RequestTimeout { get; set; }
		public bool IsJson { get { return MessageFormat == WebMessageFormatType.Json; } }
		public string ContentType { get { return IsJson ? "application/json" : "application/xml"; } }

		protected ApiClientBase()
		{
			this.MessageFormat = WebMessageFormatType.Json;
		}

		protected ApiClientBase(string baseUrl, WebMessageFormatType messageFormat)
		{
			this.BaseUrl = baseUrl;
			this.MessageFormat = messageFormat;
		}

		#region WebRequestSync
		//ss: cleaned the needlessly overcoded abomination WebRequestSync method; putting it back to original
		public T WebRequestSync<T>(Uri url, string method = HttpMethod.Get, byte[] data = null)
		{
			T result = default( T );

			WebRequest request = WebRequest.Create( url );
			request.Timeout = this.RequestTimeout == 0 ? (1000 * 60 * 5) : RequestTimeout;
			request.ContentType = this.ContentType;
			request.Credentials = CredentialCache.DefaultCredentials;
			request.Method = method;

			if( data != null && data.Length > 0 )
			{
				using( Stream requestStream = request.GetRequestStream() )
				{
					requestStream.Write( data, 0, data.Length );
				}
			}

			WebResponse response = null;
			try
			{
				response = request.GetResponse();
			}
			catch( WebException wex )
			{
				throw wex;
			}
			catch { throw; }

			if( typeof( T ) != typeof( VoidObject ) && typeof( T ) != typeof( NullResult ) )
			{
				XmlObjectSerializer dcs = new DataContractJsonSerializer( typeof( T ) );
				if( response.ContentType.ToLower().Contains( "xml" ) )
				{
					dcs = new DataContractSerializer( typeof( T ) );
				}
				using( Stream rs = response.GetResponseStream() )
				{
					try
					{
						result = (T)dcs.ReadObject( rs );
					}
					catch( SerializationException serex )
					{
						EvalSerializationException( ref serex, url, method );
						throw;
					}
					catch { throw; }
				}
			}
			response.Close();

			return result;
		}

		void EvalSerializationException(ref SerializationException serex, Uri url, string method)
		{
			//note: when upgrading to .NET 4.5 or greater, switch from Message prop to HResult prop, value = {-2146233076}
			int hresultNullValueCode = -2146233076;
			int hresult = 0;
			System.Reflection.PropertyInfo hresultProp = serex.GetType().GetProperty( "HResult",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static );
			if( hresultProp != null )
			{
				hresult = (int)hresultProp.GetValue( serex, null );
			}

			serex.Data.Add( "SerializationException",
				string.Format( "Could not deserialize result from [{1}]: {0}.", url, method ) );
			serex.Data.Add( "HResult", hresult );

			if( hresult == hresultNullValueCode ||
				serex.Message.StartsWith( "Expecting element 'root' from namespace ''.. Encountered 'None'  with name '', namespace ''." ) )
			{
				serex.Data.Add( "Resolution", "Check unexpected void/null return type/value from method." );
			}
			else
			{
				serex.Data.Add( "Resolution", string.Format( "Unknown error.", url, method ) );
			}
		}


		#region to be deleted, this is a bunch of useless junk, all of which taught me that I must code-review much more often
		//todo: convert this back to the orginal implementation w/ generic type T
		[Obsolete( "This is an abomination", false )]
		object WebRequestSync_Deprecated(Type resultType, Uri url, string method = HttpMethod.Get, byte[] data = null)
		{
			object result = default( object );
            data = data ?? new byte[0];

            WebResponse response;

            /*
             * [ss]: This is some code I was expirimenting with to test why Puts fail from Sg and ANZ.
             *       I'm leaving it in the codebase for now in case anyone wants to try and figure it out.
             * 
            WebRequest request = WebRequest.Create( new Uri( string.Format( "{0}{1}", this.BaseUrl, "/hello" ) ) );
            request.Timeout = this.RequestTimeout == 0 ? (1000 * 60 * 5) : this.RequestTimeout;
            //request.PreAuthenticate = true;
            request.Credentials = CredentialCache.DefaultCredentials;
            request.AuthenticationLevel = System.Net.Security.AuthenticationLevel.MutualAuthRequested;
            request.Method = HttpMethod.Get;
            response = request.GetResponse();
            response.Close();

            request = WebRequest.Create( url );
            request.Timeout = this.RequestTimeout == 0 ? (1000 * 60 * 5) : RequestTimeout;
            request.ContentType = this.ContentType;
            request.PreAuthenticate = true;
            request.Credentials = CredentialCache.DefaultCredentials;
            request.AuthenticationLevel = System.Net.Security.AuthenticationLevel.MutualAuthRequested;
            request.Method = method;
             * 
            */

            WebRequest request = WebRequest.Create( url );
            request.Timeout = this.RequestTimeout == 0 ? (1000 * 60 * 5) : RequestTimeout;
            request.ContentType = this.ContentType;
            request.Credentials = CredentialCache.DefaultCredentials;
            request.Method = method;

			if( data.Length > 0 )
			{
				Stream requestStream = request.GetRequestStream();
				requestStream.Write( data, 0, data.Length );
				requestStream.Close();
			}
			else
			{
				request.ContentLength = 0;
			}

			try
			{
				response = request.GetResponse();
			}
			catch( WebException wex )
			{

				response = wex.Response;

				if( response != null )
				{
					using( Stream stream = response.GetResponseStream() )
					{
						if( response.ContentType.Equals( "application/json; charset=utf-8" ) )
						{
							/// if faultExceptionEnabled is not set or set "false"  on endpointBehaviors for service 
							/// like:<webHttp helpEnabled="true" defaultOutgoingResponseFormat="Json" automaticFormatSelectionEnabled="true"/>*/
							stream.Position = 0;
							DataContractJsonSerializer serializer = new DataContractJsonSerializer( typeof( Detail ) );
							Detail faultDetails = (Detail)serializer.ReadObject( stream );
							throw new FaultException<Detail>( faultDetails, new FaultReason( faultDetails.Message ) );
						}
						else if( response.ContentType.Equals( "application/xml; charset=utf-8" ) )
						{
							/// if faultExceptionEnabled="true"  is set on endpointBehaviors for service 
							/// like:<webHttp faultExceptionEnabled="true" helpEnabled="true" defaultOutgoingResponseFormat="Json" automaticFormatSelectionEnabled="true"/>
							string responsefault = RemoveFaultNamespaces( GetResponseString( stream ) );
							if( !VaildateResponse( responsefault ) ) throw;
							XmlSerializer serializer = new XmlSerializer( typeof( Fault ), new[] { typeof( Code ), typeof( Detail ), typeof( Reason ) } );
							Fault detail = (Fault)serializer.Deserialize( XmlReader.Create( new StringReader( responsefault ) ) );
							throw new FaultException<Fault>( detail, new FaultReason( detail.Detail.Message ) );
						}
					}
				}

				throw;
			}
			catch( Exception )
			{
				return result;
			}


			try
			{
				//if( typeof( T ) != typeof( VoidObject ) )
				if( resultType != typeof( VoidObject ) && resultType != typeof( NullResult ) )
				{
					//default to Json, switch to XML if required
					XmlObjectSerializer dcs = new DataContractJsonSerializer( resultType );
					if( response.ContentType.Contains( "xml" ) )
					{
						dcs = new DataContractSerializer( resultType );
					}
					using( Stream rs = response.GetResponseStream() )
					{
						//todo: use this and not the stupid block of code below it
						//result = (T)dcs.ReadObject( rs );
						result = dcs.ReadObject( rs );
						if( result.GetType() == resultType )
						{
							result = Convert.ChangeType( result, resultType );
						}
					}
				}
			}
			catch( SerializationException serEx )
			{
				//if block covers a null response, throw everything else
				string msg = serEx.Message;
				if( !msg.StartsWith( "Expecting element 'root' from namespace ''.. Encountered 'None'  with name '', namespace ''." ) )
				{
					throw serEx;
				}
			}
			catch( Exception ex )
			{
				throw ex;
			}
			response.Close();


			//ss05202013: this is unintelligible to me:
			//  2 complete reads of the response stream to check for an empty result,
			//  then finally a thrid to return the result??
			//  btw: the first test [rs != null] will never fail, and the second represents abject failure
			//using( Stream rs = CopyAndClose( response.GetResponseStream() ) )
			//{
			//    if( rs != null )
			//    {
			//        StreamReader reader = new StreamReader( rs );
			//        string resultstring = reader.ReadToEnd();
			//        //Debug.WriteLine(resultstring);

			//        if( resultstring == string.Empty )
			//        {
			//            byte[] emptystring = Encoding.UTF8.GetBytes( "[]" );
			//            rs.Write( emptystring, 0, emptystring.Length );
			//        }

			//        rs.Position = 0;
			//        result = dcs.ReadObject( rs );
			//        if( result.GetType() == resultType )
			//        {
			//            result = Convert.ChangeType( result, resultType );
			//        }
			//    }
			//}
			//response.Close();

			return result;
		}

		[Obsolete( "this does nothing useful", false )]
		public RequestData<T> WebRequestSync<T>(RequestData<T> requestData)
		{
			requestData.Result = this.WebRequestSync<T>( requestData.Url, requestData.Method, requestData.Data );
			return requestData;
		}

		[Obsolete( "this does nothing useful", false )]
		public T WebRequestSync<T>(Uri url, string method, XmlDocument data)
		{
			StringBuilder output = new StringBuilder();
			XmlWriter xmlWriter = XmlWriter.Create( output );
			data.WriteTo( xmlWriter );
			xmlWriter.Flush();

			byte[] xmlBytes = Encoding.UTF8.GetBytes( output.ToString() );

			return this.WebRequestSync<T>( url, method, xmlBytes );
		}

		[Obsolete( "this does nothing useful", false )]
		public RequestData WebRequestSync(object requestData)
		{
			if( requestData == null || !(requestData is RequestData) ) throw new ArgumentNullException( "requestData", new Exception( "Paramerter 'requestData'  must be covertible to type RequestData" ) );

			RequestData rd = (RequestData)requestData;
			rd.Result = this.WebRequestSync_Deprecated( rd.ResultType, rd.Url, rd.Method, rd.Data );
			return rd;
		}

		[Obsolete( "this does nothing useful", false )]
		private static Stream CopyAndClose(Stream inputStream)
		{
			const int readSize = 256;
			byte[] buffer = new byte[readSize];
			MemoryStream ms = new MemoryStream();

			int count = inputStream.Read( buffer, 0, readSize );
			while( count > 0 )
			{
				ms.Write( buffer, 0, count );
				count = inputStream.Read( buffer, 0, readSize );
			}

			ms.Position = 0;
			inputStream.Close();

			return ms;
		}

		[Obsolete( "this does nothing useful", false )]
		private static bool VaildateResponse(string fault)
		{
			return (fault.Contains( "<Detail" ) && fault.Contains( "<Fault" ));
		}

		[Obsolete( "this does nothing useful", false )]
		private static string GetResponseString(Stream stream)
		{
			stream.Position = 0;
			StreamReader reader = new StreamReader( stream, Encoding.UTF8 );
			return reader.ReadToEnd();
		}

		[Obsolete( "this does nothing useful", false )]
		private static string RemoveFaultNamespaces(string responseString)
		{
			responseString = responseString.Replace( "</Detail></Detail>", "</Detail>" );
			responseString = responseString.Replace( "<Detail>", string.Empty );
			responseString = responseString.Replace( "xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"", string.Empty );
			responseString = responseString.Replace( "xmlns=\"http://schemas.microsoft.com/ws/2005/05/envelope/none\"", string.Empty );
			responseString = responseString.Replace( "xmlns=\"\"", string.Empty );
			responseString = responseString.Replace( "i:nil=\"true\"", string.Empty );
			return responseString;
		}
		#endregion
		#endregion

		#region WebRequestAsync & RunBackgroundWorker overloads
		protected void WebRequestAsync<T>(Uri uriTemplate, object state, EventHandler<AsyncCallCompletedEventArgs<T>> handler) where T : class
		{
			this.WebRequestAsync( uriTemplate, state, new byte[0], handler );
		}
		protected void WebRequestAsync<T>(Uri uriTemplate, object state, byte[] data, EventHandler<AsyncCallCompletedEventArgs<T>> handler) where T : class
		{
			RequestData<T> rd = new RequestData<T>( uriTemplate, state, data );
			this.WebRequestAsync( rd, handler );
		}
		protected void WebRequestAsync<T>(Uri uriTemplate, object state, string method, EventHandler<AsyncCallCompletedEventArgs<T>> handler) where T : class
		{
			RequestData<T> rd = new RequestData<T>( uriTemplate, state, new byte[0], method );
			this.WebRequestAsync( rd, handler );
		}
		protected void WebRequestAsync<T>(Uri uriTemplate, object state, byte[] data, string method, EventHandler<AsyncCallCompletedEventArgs<T>> handler) where T : class
		{
			RequestData<T> rd = new RequestData<T>( uriTemplate, state, data, method );
			this.WebRequestAsync( rd, handler );
		}
		protected void WebRequestAsync<T>(RequestData<T> rd, EventHandler<AsyncCallCompletedEventArgs<T>> handler) where T : class
		{
			this.RunBackgroundWorker( rd, handler );
		}
		protected void WebRequestAsync(Uri uriTemplate, object state, byte[] data, Type resultType, string method, EventHandler<AsyncCallCompletedEventArgs> handler)
		{
			RequestData rd = new RequestData( uriTemplate, state, data, resultType, method );
			this.WebRequestAsync( rd, handler );
		}
		protected void WebRequestAsync(RequestData rd, EventHandler<AsyncCallCompletedEventArgs> handler)
		{
			this.RunBackgroundWorker( rd, handler );
		}

		protected void RunBackgroundWorker(RequestData rd, EventHandler<AsyncCallCompletedEventArgs> completedEventHandler)
		{
			BackgroundWorker w = new BackgroundWorker();

			w.DoWork += (sender, e) =>
			{
				e.Result = this.WebRequestSync( e.Argument );
			};


			if( completedEventHandler != null )
			{
				w.RunWorkerCompleted += (sender, e) =>
				{
					object result = null;
					Type resultType = null;

					if( e.Error == null )
					{
						RequestData requestData = (RequestData)e.Result;
						result = requestData.Result;
						resultType = requestData.ResultType;
					}

					completedEventHandler( this,
						new AsyncCallCompletedEventArgs( result, resultType, rd.State, e.Error, e.Cancelled ) );
				};
			}

			w.RunWorkerAsync( rd );
		}

		protected void RunBackgroundWorker<T>(RequestData<T> rd, EventHandler<AsyncCallCompletedEventArgs<T>> completedEventHandler) where T : class
		{
			BackgroundWorker w = new BackgroundWorker();

			w.DoWork += (sender, e) =>
			{
				e.Result = this.WebRequestSync( (RequestData<T>)e.Argument );
			};

			if( completedEventHandler != null )
			{
				w.RunWorkerCompleted += (sender, e) =>
				{
					T result = default( T );

					if( e.Error == null )
					{
						RequestData requestData = (RequestData)e.Result;
						result = (T)requestData.Result;
					}

					completedEventHandler( this,
						new AsyncCallCompletedEventArgs<T>( result, rd.State, e.Error, e.Cancelled ) );
				};
			}

			w.RunWorkerAsync( rd );
		}

		protected void RunBackgroundWorker<T>(RequestData<T> rd, RunWorkerCompletedEventHandler completedEventHandler) where T : class
		{
			BackgroundWorker w = new BackgroundWorker();

			w.DoWork += (sender, e) =>
			{
				e.Result = this.WebRequestSync( (RequestData<T>)e.Argument );
			};

			w.RunWorkerCompleted += (sender, e) =>
			{
				completedEventHandler( sender, e );
			};

			w.RunWorkerAsync( rd );
		}

		protected void RunBackgroundWorker(RequestData rd, RunWorkerCompletedEventHandler completedEventHandler)
		{
			BackgroundWorker w = new BackgroundWorker();

			w.DoWork += (sender, e) =>
			{
				e.Result = this.WebRequestSync( e.Argument );
			};

			w.RunWorkerCompleted += (sender, e) =>
			{
				completedEventHandler( sender, e );
			};

			w.RunWorkerAsync( rd );
		}
		#endregion
	}

	//surely there's a better way to handle this
	public class VoidObject { }
}