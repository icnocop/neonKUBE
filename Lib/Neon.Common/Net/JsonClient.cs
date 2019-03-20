﻿//-----------------------------------------------------------------------------
// FILE:	    JsonClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Collections;
using Neon.Diagnostics;
using Neon.Retry;

namespace Neon.Net
{
    /// <summary>
    /// Implements a light-weight JSON oriented HTTP client.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use <see cref="GetAsync(string, ArgDictionary, CancellationToken, LogActivity)"/>, 
    /// <see cref="PutAsync(string, dynamic, ArgDictionary, CancellationToken, LogActivity)"/>, 
    /// <see cref="PostAsync(string, dynamic, ArgDictionary, CancellationToken, LogActivity)"/>, and 
    /// <see cref="DeleteAsync(string, ArgDictionary, CancellationToken, LogActivity)"/>
    /// to perform HTTP operations that ensure that a non-error HTTP status code is returned by the servers.
    /// </para>
    /// <para>
    /// Use <see cref="GetUnsafeAsync(string, ArgDictionary, CancellationToken, LogActivity)"/>, 
    /// <see cref="PutUnsafeAsync(string, dynamic, ArgDictionary, CancellationToken, LogActivity)"/>, 
    /// <see cref="PostUnsafeAsync(string, dynamic, ArgDictionary, CancellationToken, LogActivity)"/>, and 
    /// <see cref="DeleteUnsafeAsync(string, ArgDictionary, CancellationToken, LogActivity)"/>
    /// to perform an HTTP without ensuring a non-error HTTP status code.
    /// </para>
    /// <para>
    /// This class can also handle retrying operations when transient errors are detected.  Customize 
    /// <see cref="SafeRetryPolicy"/> and/or <see cref="UnsafeRetryPolicy"/> by setting a <see cref="IRetryPolicy"/> 
    /// implementation such as <see cref="LinearRetryPolicy"/> or <see cref="ExponentialRetryPolicy"/>.
    /// </para>
    /// <note>
    /// This class initializes <see cref="SafeRetryPolicy"/> to a reasonable <see cref="ExponentialRetryPolicy"/> by default
    /// and <see cref="UnsafeRetryPolicy"/> to <see cref="NoRetryPolicy"/>.  You can override the default
    /// retry policy for specific requests using the methods that take an <see cref="IRetryPolicy"/> as 
    /// their first parameter.
    /// </note>
    /// <note>
    /// <para>
    /// The <see cref="JsonClientPayload"/> class can be used to customize both the <b>Content-Type</b> header
    /// and the actual payload uploaded with <b>POST</b> and <b>PUT</b> requests.  This can be used for those
    /// <i>special</i> REST APIs that don't accept JSON payloads.
    /// </para>
    /// <para>
    /// All you need to do is construct a <see cref="JsonClientPayload"/> instance, specifying the value to
    /// be used as the <b>Content-Type</b> header and the payload data as text or a byte array and then pass
    /// this as the <b>document</b> parameter to the methods that upload content.  The methods will recognize
    /// this special type and just send the specified data rather than attempting to serailize the document
    /// to JSON.
    /// </para>
    /// </note>
    /// </remarks>
    public class JsonClient : IDisposable
    {
        //---------------------------------------------------------------------
        // Instance members

        private object          syncLock          = new object();
        private IRetryPolicy    safeRetryPolicy   = new ExponentialRetryPolicy(TransientDetector.NetworkOrHttp);
        private IRetryPolicy    unsafeRetryPolicy = new NoRetryPolicy();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="handler">The optional message handler.</param>
        /// <param name="disposeHandler">Indicates whether the handler passed will be disposed automatically (defaults to <c>false</c>).</param>
        public JsonClient(HttpMessageHandler handler = null, bool disposeHandler = false)
        {
            if (handler == null)
            {
                handler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
                };

                disposeHandler = true;  // Always dispose handlers created by the constructor.
            }

            HttpClient = new HttpClient(handler, disposeHandler);

            HttpClient.DefaultRequestHeaders.Add("Accept", DocumentType);
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~JsonClient()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (syncLock)
                {
                    if (HttpClient != null)
                    {
                        HttpClient.Dispose();
                        HttpClient = null;
                    }
                }

                GC.SuppressFinalize(this);
            }

            HttpClient = null;
        }

        /// <summary>
        /// The default base <see cref="Uri"/> the client will use when relative
        /// URIs are specified for requests.
        /// </summary>
        public Uri BaseAddress
        {
            get { return HttpClient.BaseAddress; }
            set { HttpClient.BaseAddress = value; }
        }

        /// <summary>
        /// The default base <see cref="Uri"/> the client will use when relative
        /// URIs are specified for requests.
        /// </summary>
        public TimeSpan Timeout
        {
            get { return HttpClient.Timeout; }
            set { HttpClient.Timeout = value; }
        }

        /// <summary>
        /// Returns the base client's default request headers property to make it easy
        /// to customize request headers.
        /// </summary>
        public HttpRequestHeaders DefaultRequestHeaders => HttpClient.DefaultRequestHeaders;

        /// <summary>
        /// Specifies the MIME type to use posting or putting documents to the endpoint.
        /// This defaults to the standard <b>application/json</b> but some services
        /// may require custom values.
        /// </summary>
        public string DocumentType { get; set; } = "application/json";

        /// <summary>
        /// Returns the underlying <see cref="System.Net.Http.HttpClient"/>.
        /// </summary>
        public HttpClient HttpClient { get; private set; }

        /// <summary>
        /// <para>
        /// The <see cref="IRetryPolicy"/> to be used to detect and retry transient network and HTTP
        /// errors for the <b>safe</b> methods.  This defaults to <see cref="ExponentialRetryPolicy"/> with 
        /// the transient detector function set to <see cref="TransientDetector.NetworkOrHttp(Exception)"/>.
        /// </para>
        /// <note>
        /// You may set this to <c>null</c> to disable safe transient error retry.
        /// </note>
        /// </summary>
        public IRetryPolicy SafeRetryPolicy
        {
            get { return safeRetryPolicy; }
            set { safeRetryPolicy = value ?? NoRetryPolicy.Instance; }
        }

        /// <summary>
        /// <para>
        /// The <see cref="IRetryPolicy"/> to be used to detect and retry transient network errors for the
        /// <b>unsafe</b> methods.  This defaults to <see cref="NoRetryPolicy"/>.
        /// </para>
        /// <note>
        /// You may set this to <c>null</c> to disable unsafe transient error retry.
        /// </note>
        /// </summary>
        public IRetryPolicy UnsafeRetryPolicy
        {
            get { return unsafeRetryPolicy; }
            set { unsafeRetryPolicy = value ?? NoRetryPolicy.Instance; }
        }

        /// <summary>
        /// Converts a relative URI into an absolute URI if necessary.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns>The absolute URI.</returns>
        private string AbsoluteUri(string uri)
        {
            if (string.IsNullOrEmpty(uri) || uri[0] == '/')
            {
                return new Uri(BaseAddress, uri).ToString();
            }
            else
            {
                return uri;
            }
        }

        /// <summary>
        /// Formats the URI by appending query arguments as required.
        /// </summary>
        /// <param name="uri">The base URI.</param>
        /// <param name="args">The query arguments.</param>
        /// <returns>The formatted URI.</returns>
        private string FormatUri(string uri, ArgDictionary args)
        {
            if (args == null || args.Count == 0)
            {
                return AbsoluteUri(uri);
            }

            var sb    = new StringBuilder(uri);
            var first = true;

            foreach (var arg in args)
            {
                if (first)
                {
                    sb.Append('?');
                    first = false;
                }
                else
                {
                    sb.Append('&');
                }

                string value;

                if (arg.Value == null)
                {
                    value = "null";
                }
                else if (arg.Value is bool)
                {
                    value = NeonHelper.ToBoolString((bool)arg.Value);
                }
                else
                {
                    value = arg.Value.ToString();
                }

                sb.Append($"{arg.Key}={Uri.EscapeDataString(value)}");
            }

            return AbsoluteUri(sb.ToString());
        }

        /// <summary>
        /// <para>
        /// Converts the object passed into JSON content suitable for transmitting in
        /// an HTTP request.
        /// </para>
        /// <note>
        /// This method handles <see cref="JsonClientPayload"/> documents specially 
        /// as described in the <see cref="JsonClient"/> remarks.
        /// </note>
        /// </summary>
        /// <param name="document">The document object or JSON text.</param>
        /// <returns>Tne <see cref="HttpContent"/>.</returns>
        private HttpContent CreateContent(object document)
        {
            if (document == null)
            {
                return null;
            }

            var custom = document as JsonClientPayload;

            if (custom != null)
            {
                var content = new ByteArrayContent(custom.ContentBytes);

                content.Headers.ContentType = new MediaTypeHeaderValue(custom.ContentType);

                return content;
            }
            else
            {
                var json = document as string;

                if (json == null)
                {
                    var jObject = document as JObject;

                    if (jObject != null)
                    {
                        json = jObject.ToString(Formatting.None);
                    }
                }

                return new StringContent(json ?? NeonHelper.JsonSerialize(document), Encoding.UTF8, DocumentType);
            }
        }

        /// <summary>
        /// Performs an HTTP <b>GET</b> ensuring that a success code was returned.
        /// </summary>
        /// <param name="uri">The URI</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        /// <exception cref="HttpException">Thrown when the server responds with an HTTP error status code.</exception>
        public async Task<JsonResponse> GetAsync(
            string              uri, 
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default,
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            return await safeRetryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.GetAsync(requestUri, cancellationToken, logActivity);
                        var jsonResponse = new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());

                        jsonResponse.EnsureSuccess();

                        return jsonResponse;
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }

        /// <summary>
        /// Performs an HTTP <b>GET</b> returning a specific type and ensuring that a success code was returned.
        /// </summary>
        /// <typeparam name="TResult">The desired result type.</typeparam>
        /// <param name="uri">The URI</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        /// <exception cref="HttpException">Thrown when the server responds with an HTTP error status code.</exception>
        public async Task<TResult> GetAsync<TResult>(
            string              uri, 
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            var result = await safeRetryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.GetAsync(requestUri, cancellationToken, logActivity);
                        var jsonResponse = new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());

                        jsonResponse.EnsureSuccess();

                        return jsonResponse;
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });

            return result.As<TResult>();
        }

        /// <summary>
        /// Performs an HTTP <b>GET</b> using a specific <see cref="IRetryPolicy"/>" and ensuring
        /// that a success code was returned.
        /// </summary>
        /// <param name="retryPolicy">The retry policy or <c>null</c> to disable retries.</param>
        /// <param name="uri">The URI</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        /// <exception cref="HttpException">Thrown when the server responds with an HTTP error status code.</exception>
        public async Task<JsonResponse> GetAsync(
            IRetryPolicy        retryPolicy,
            string              uri, 
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            retryPolicy = retryPolicy ?? NoRetryPolicy.Instance;

            return await retryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.GetAsync(requestUri, cancellationToken, logActivity);
                        var jsonResponse = new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());

                        jsonResponse.EnsureSuccess();

                        return jsonResponse;
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }

        /// <summary>
        /// Performs an HTTP <b>GET</b> without ensuring that a success code was returned.
        /// </summary>
        /// <param name="uri">The URI</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        public async Task<JsonResponse> GetUnsafeAsync(
            string              uri, 
            ArgDictionary       args = null,
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            return await unsafeRetryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.GetAsync(requestUri, cancellationToken, logActivity);

                        return new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }

        /// <summary>
        /// Performs an HTTP <b>GET</b> using a specific <see cref="IRetryPolicy"/> and 
        /// without ensuring that a success code was returned.
        /// </summary>
        /// <param name="retryPolicy">The retry policy or <c>null</c> to disable retries.</param>
        /// <param name="uri">The URI</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        public async Task<JsonResponse> GetUnsafeAsync(
            IRetryPolicy        retryPolicy, 
            string              uri, 
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            retryPolicy = retryPolicy ?? NoRetryPolicy.Instance;

            return await retryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.GetAsync(requestUri, cancellationToken, logActivity);

                        return new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }

        /// <summary>
        /// Performs an HTTP <b>PUT</b> ensuring that a success code was returned.
        /// </summary>
        /// <param name="uri">The URI</param>
        /// <param name="document">The optional object to be uploaded as the request payload.</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        /// <exception cref="HttpException">Thrown when the server responds with an HTTP error status code.</exception>
        public async Task<JsonResponse> PutAsync(
            string              uri, 
            object              document = null, 
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            return await safeRetryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.PutAsync(requestUri, CreateContent(document), cancellationToken, logActivity);
                        var jsonResponse = new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());

                        jsonResponse.EnsureSuccess();

                        return jsonResponse;
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }

        /// <summary>
        /// Performs an HTTP <b>PUT</b> returning a specific type and ensuring that a success code was returned.
        /// </summary>
        /// <typeparam name="TResult">The desired result type.</typeparam>
        /// <param name="uri">The URI</param>
        /// <param name="document">The optional object to be uploaded as the request payload.</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        /// <exception cref="HttpException">Thrown when the server responds with an HTTP error status code.</exception>
        public async Task<TResult> PutAsync<TResult>(
            string              uri, 
            object              document = null,
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            var result = await safeRetryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.PutAsync(requestUri, CreateContent(document), cancellationToken, logActivity);
                        var jsonResponse = new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());

                        jsonResponse.EnsureSuccess();

                        return jsonResponse;
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });

            return result.As<TResult>();
        }

        /// <summary>
        /// Performs an HTTP <b>PUT</b> using a specific <see cref="IRetryPolicy"/>" and ensuring that a 
        /// success code was returned.
        /// </summary>
        /// <param name="retryPolicy">The retry policy or <c>null</c> to disable retries.</param>
        /// <param name="uri">The URI</param>
        /// <param name="document">The optional object to be uploaded as the request payload.</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        /// <exception cref="HttpException">Thrown when the server responds with an HTTP error status code.</exception>
        public async Task<JsonResponse> PutAsync(
            IRetryPolicy        retryPolicy,
            string              uri, 
            object              document = null, 
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            retryPolicy = retryPolicy ?? NoRetryPolicy.Instance;

            return await retryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.PutAsync(requestUri, CreateContent(document), cancellationToken, logActivity);
                        var jsonResponse = new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());

                        jsonResponse.EnsureSuccess();

                        return jsonResponse;
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }

        /// <summary>
        /// Performs an HTTP <b>PUT</b> without ensuring that a success code was returned.
        /// </summary>
        /// <param name="uri">The URI</param>
        /// <param name="document">The optional object to be uploaded as the request payload.</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        public async Task<JsonResponse> PutUnsafeAsync(
            string              uri, 
            object              document = null, 
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            return await unsafeRetryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.PutAsync(FormatUri(uri, args), CreateContent(document), cancellationToken, logActivity);

                        return new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }

        /// <summary>
        /// Performs an HTTP <b>PUT</b> using a specific <see cref="IRetryPolicy"/>" and without 
        /// ensuring that a success code was returned.
        /// </summary>
        /// <param name="retryPolicy">The retry policy or <c>null</c> to disable retries.</param>
        /// <param name="uri">The URI</param>
        /// <param name="document">The optional object to be uploaded as the request payload.</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        public async Task<JsonResponse> PutUnsafeAsync(
            IRetryPolicy        retryPolicy,
            string              uri,
            object              document = null,
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            retryPolicy = retryPolicy ?? NoRetryPolicy.Instance;

            return await retryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.PutAsync(requestUri, CreateContent(document), cancellationToken, logActivity);

                        return new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }

        /// <summary>
        /// Performs an HTTP <b>POST</b> ensuring that a success code was returned.
        /// </summary>
        /// <param name="uri">The URI</param>
        /// <param name="document">The optional object to be uploaded as the request payload.</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        /// <exception cref="HttpException">Thrown when the server responds with an HTTP error status code.</exception>
        public async Task<JsonResponse> PostAsync(
            string              uri, 
            object              document = null, 
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            return await safeRetryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.PostAsync(requestUri, CreateContent(document), cancellationToken, logActivity);
                        var jsonResponse = new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());

                        jsonResponse.EnsureSuccess();

                        return jsonResponse;
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }

        /// <summary>
        /// Performs an HTTP <b>POST</b> returning a specific type and ensuring that a success code was returned.
        /// </summary>
        /// <typeparam name="TResult">The desired result type.</typeparam>
        /// <param name="uri">The URI</param>
        /// <param name="document">Optional object to be uploaded as the request payload.</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        /// <exception cref="HttpException">Thrown when the server responds with an HTTP error status code.</exception>
        public async Task<TResult> PostAsync<TResult>(
            string              uri, 
            object              document = null, 
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            var result = await safeRetryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.PostAsync(requestUri, CreateContent(document), cancellationToken, logActivity);
                        var jsonResponse = new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());

                        jsonResponse.EnsureSuccess();

                        return jsonResponse;
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });

            return result.As<TResult>();
        }

        /// <summary>
        /// Performs an HTTP <b>POST</b> using a specific <see cref="IRetryPolicy"/> and ensuring that
        /// a success code was returned.
        /// </summary>
        /// <param name="retryPolicy">The retry policy or <c>null</c> to disable retries.</param>
        /// <param name="uri">The URI</param>
        /// <param name="document">The optional object to be uploaded as the request payload.</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        /// <exception cref="HttpException">Thrown when the server responds with an HTTP error status code.</exception>
        public async Task<JsonResponse> PostAsync(
            IRetryPolicy        retryPolicy, 
            string              uri,
            object              document = null, 
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            retryPolicy = retryPolicy ?? NoRetryPolicy.Instance;

            return await retryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.PostAsync(requestUri, CreateContent(document), cancellationToken, logActivity);
                        var jsonResponse = new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());

                        jsonResponse.EnsureSuccess();

                        return jsonResponse;
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }

        /// <summary>
        /// Performs an HTTP <b>POST</b> without ensuring that a success code was returned.
        /// </summary>
        /// <param name="uri">The URI</param>
        /// <param name="document">The optional object to be uploaded as the request payload.</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        public async Task<JsonResponse> PostUnsafeAsync(
            string              uri,
            object              document = null, 
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            return await unsafeRetryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.PostAsync(requestUri, CreateContent(document), cancellationToken, logActivity);

                        return new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }

        /// <summary>
        /// Performs an HTTP <b>POST</b> using a specific <see cref="IRetryPolicy"/> and without ensuring
        /// that a success code was returned.
        /// </summary>
        /// <param name="retryPolicy">The retry policy or <c>null</c> to disable retries.</param>
        /// <param name="uri">The URI</param>
        /// <param name="document">The optional object to be uploaded as the request payload.</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        public async Task<JsonResponse> PostUnsafeAsync(
            IRetryPolicy        retryPolicy, 
            string              uri, 
            object              document = null,
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            retryPolicy = retryPolicy ?? NoRetryPolicy.Instance;

            return await retryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.PostAsync(requestUri, CreateContent(document), cancellationToken, logActivity);

                        return new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }

        /// <summary>
        /// Performs an HTTP <b>DELETE</b> ensuring that a success code was returned.
        /// </summary>
        /// <param name="uri">The URI</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        /// <exception cref="HttpException">Thrown when the server responds with an HTTP error status code.</exception>
        public async Task<JsonResponse> DeleteAsync(
            string              uri, 
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default,
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            return await safeRetryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.DeleteAsync(requestUri, cancellationToken, logActivity);
                        var jsonResponse = new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());

                        jsonResponse.EnsureSuccess();

                        return jsonResponse;
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }

        /// <summary>
        /// Performs an HTTP <b>DELETE</b> returning a specific type and ensuring that a success cxode was returned.
        /// </summary>
        /// <typeparam name="TResult">The desired result type.</typeparam>
        /// <param name="uri">The URI</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        /// <exception cref="HttpException">Thrown when the server responds with an HTTP error status code.</exception>
        public async Task<TResult> DeleteAsync<TResult>(
            string              uri, 
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            var result = await safeRetryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.DeleteAsync(requestUri, cancellationToken, logActivity);
                        var jsonResponse = new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());

                        jsonResponse.EnsureSuccess();

                        return jsonResponse;
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });

            return result.As<TResult>();
        }

        /// <summary>
        /// Performs an HTTP <b>DELETE</b> using a specific <see cref="IRetryPolicy"/> and ensuring 
        /// that a success code was returned.
        /// </summary>
        /// <param name="retryPolicy">The retry policy or <c>null</c> to disable retries.</param>
        /// <param name="uri">The URI</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        /// <exception cref="HttpException">Thrown when the server responds with an HTTP error status code.</exception>
        public async Task<JsonResponse> DeleteAsync(
            IRetryPolicy        retryPolicy,
            string              uri, 
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            retryPolicy = retryPolicy ?? NoRetryPolicy.Instance;

            return await retryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.DeleteAsync(requestUri, cancellationToken, logActivity);
                        var jsonResponse = new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());

                        jsonResponse.EnsureSuccess();

                        return jsonResponse;
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }

        /// <summary>
        /// Performs an HTTP <b>DELETE</b> without ensuring that a success code was returned.
        /// </summary>
        /// <param name="uri">The URI</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        public async Task<JsonResponse> DeleteUnsafeAsync(
            string              uri, 
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            return await unsafeRetryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.DeleteAsync(requestUri, cancellationToken, logActivity);

                        return new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }

        /// <summary>
        /// Performs an HTTP <b>DELETE</b> using a specific <see cref="IRetryPolicy"/> and without ensuring 
        /// that a success code was returned.
        /// </summary>
        /// <param name="retryPolicy">The retry policy or <c>null</c> to disable retries.</param>
        /// <param name="uri">The URI</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        public async Task<JsonResponse> DeleteUnsafeAsync(
            IRetryPolicy        retryPolicy, 
            string              uri, 
            ArgDictionary       args = null, 
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            retryPolicy = retryPolicy ?? NoRetryPolicy.Instance;

            return await retryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.DeleteAsync(requestUri, cancellationToken, logActivity);

                        return new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }
    }
}
