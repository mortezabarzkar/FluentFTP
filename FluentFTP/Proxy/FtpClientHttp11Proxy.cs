using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FluentFTP.Proxy {
	/// <summary> A FTP client with a HTTP 1.1 proxy implementation. </summary>
	public class FtpClientHttp11Proxy : FtpClientProxy {
		/// <summary> A FTP client with a HTTP 1.1 proxy implementation </summary>
		/// <param name="proxy">Proxy information</param>
		public FtpClientHttp11Proxy(ProxyInfo proxy)
			: base(proxy) {
			ConnectionType = "HTTP 1.1 Proxy";
		}

		/// <summary> Redefine the first dialog: HTTP Frame for the HTTP 1.1 Proxy </summary>
		protected override void Handshake() {
			var proxyConnectionReply = GetReply();
			if (!proxyConnectionReply.Success)
				throw new FtpException("Can't connect " + Host + " via proxy " + Proxy.Host + ".\nMessage : " +
										proxyConnectionReply.ErrorMessage);
		}

	    /// <summary>
	    /// Creates a new instance of this class. Useful in FTP proxy classes.
	    /// </summary>
		protected override FtpClient Create() {
			return new FtpClientHttp11Proxy(Proxy);
		}

        /// <summary>
        /// Connects to the server using an existing <see cref="FtpSocketStream"/>
        /// </summary>
        /// <param name="stream">The existing socket stream</param>
		protected override void Connect(FtpSocketStream stream) {
			Connect(stream, Host, Port, FtpIpVersion.ANY);
		}

        /// <summary>
        /// Connects to the server using an existing <see cref="FtpSocketStream"/>
        /// </summary>
        /// <param name="stream">The existing socket stream</param>
        /// <param name="host">Host name</param>
        /// <param name="port">Port number</param>
        /// <param name="ipVersions">IP version to use</param>
		protected override void Connect(FtpSocketStream stream, string host, int port, FtpIpVersion ipVersions) {
			base.Connect(stream);

			var writer = new StreamWriter(stream);
			writer.WriteLine("CONNECT {0}:{1} HTTP/1.1", host, port);
			writer.WriteLine("Host: {0}:{1}", host, port);
			if (Proxy.Credentials != null) {
				var credentialsHash = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Proxy.Credentials.UserName + ":"+ Proxy.Credentials.Password));
				writer.WriteLine("Proxy-Authorization: Basic "+ credentialsHash);
			}
			writer.WriteLine("User-Agent: custom-ftp-client");
			writer.WriteLine();
			writer.Flush();

			ProxyHandshake(stream);
		}

		private void ProxyHandshake(FtpSocketStream stream) {
			var proxyConnectionReply = GetProxyReply(stream);
			if (!proxyConnectionReply.Success)
				throw new FtpException("Can't connect " + Host + " via proxy " + Proxy.Host + ".\nMessage : " + proxyConnectionReply.ErrorMessage);
		}
		
		private FtpReply GetProxyReply( FtpSocketStream stream ) {
			
			FtpReply reply = new FtpReply();
			string buf;

#if !CORE14
            lock ( Lock ) {
#endif
				if( !IsConnected )
					throw new InvalidOperationException( "No connection to the server has been established." );
				
				stream.ReadTimeout = ReadTimeout;
				while( ( buf = stream.ReadLine( Encoding ) ) != null ) {
					Match m;
					
					FtpTrace.WriteLine(FtpTraceLevel.Info, buf);
					
					if( ( m = Regex.Match( buf, @"^HTTP/.*\s(?<code>[0-9]{3}) (?<message>.*)$" ) ).Success ) {
						reply.Code = m.Groups[ "code" ].Value;
						reply.Message = m.Groups[ "message" ].Value;
						break;
					}
					
					reply.InfoMessages += ( buf+"\n" );
				}
				
				// fixes #84 (missing bytes when downloading/uploading files through proxy)
				while( ( buf = stream.ReadLine( Encoding ) ) != null ) {

                    FtpTrace.WriteLine(FtpTraceLevel.Info, buf);

					if (FtpExtensions.IsNullOrWhiteSpace(buf)) {
						break;
					}
					
					reply.InfoMessages += ( buf+"\n" );
				}

#if !CORE14
            }
#endif

			return reply;
		}

	}
}