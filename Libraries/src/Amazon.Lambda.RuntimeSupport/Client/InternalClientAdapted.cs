/*
 * Copyright 2019 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 * 
 *  http://aws.amazon.com/apache2.0
 * 
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */
/*
 * This file was originally generated using NSwag.
 * The original generated file is InternalClientGenerated.txt.  To see what's changed from the generated version, from the root of the repo run:
 * git diff -w -U100000 --no-index byolopensource/Amazon.Lambda.RuntimeSupport.Client/Amazon.Lambda.RuntimeSupport.Client/InternalClientGenerated.txt byolopensource/Amazon.Lambda.RuntimeSupport.Client/Amazon.Lambda.RuntimeSupport.Client/InternalClientAdapted.cs
 * This code has been adapted to:
 * 1. Use an internal copy of LitJson to avoid dependency collision with consumers.
 * 2. Allow streams to be sent directly as a response, instead of assuming the response is JSON.
 * 3. Customize based on AWS Lambda specific nuances. (See comments in runtime-api.yaml)
 */

namespace Amazon.Lambda.RuntimeSupport
{

    internal partial interface IInternalRuntimeApiClient
    {
        /// <summary>Non-recoverable initialization error. Runtime should exit after reporting the error. Error will be served in response to the first invoke.</summary>
        /// <returns>Accepted</returns>
        /// <exception cref="RuntimeApiClientException">A server side error occurred.</exception>
        System.Threading.Tasks.Task<SwaggerResponse<StatusResponse>> ErrorAsync(string lambda_Runtime_Function_Error_Type, string errorJson);

        /// <summary>Non-recoverable initialization error. Runtime should exit after reporting the error. Error will be served in response to the first invoke.</summary>
        /// <returns>Accepted</returns>
        /// <exception cref="RuntimeApiClientException">A server side error occurred.</exception>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        System.Threading.Tasks.Task<SwaggerResponse<StatusResponse>> ErrorAsync(string lambda_Runtime_Function_Error_Type, string errorJson, System.Threading.CancellationToken cancellationToken);

        /// <summary>Runtime makes this HTTP request when it is ready to receive and process a new invoke.</summary>
        /// <returns>This is an iterator-style blocking API call. Response contains event JSON document, specific to the invoking service.</returns>
        /// <exception cref="RuntimeApiClientException">A server side error occurred.</exception>
        System.Threading.Tasks.Task<SwaggerResponse<System.IO.Stream>> NextAsync();

        /// <summary>Runtime makes this HTTP request when it is ready to receive and process a new invoke.</summary>
        /// <returns>This is an iterator-style blocking API call. Response contains event JSON document, specific to the invoking service.</returns>
        /// <exception cref="RuntimeApiClientException">A server side error occurred.</exception>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        System.Threading.Tasks.Task<SwaggerResponse<System.IO.Stream>> NextAsync(System.Threading.CancellationToken cancellationToken);

        /// <summary>Runtime makes this request in order to submit a response.</summary>
        /// <returns>Accepted</returns>
        /// <exception cref="RuntimeApiClientException">A server side error occurred.</exception>
        System.Threading.Tasks.Task<SwaggerResponse<StatusResponse>> ResponseAsync(string awsRequestId, System.IO.Stream outputStream);

        /// <summary>Runtime makes this request in order to submit a response.</summary>
        /// <returns>Accepted</returns>
        /// <exception cref="RuntimeApiClientException">A server side error occurred.</exception>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        System.Threading.Tasks.Task<SwaggerResponse<StatusResponse>> ResponseAsync(string awsRequestId, System.IO.Stream outputStream, System.Threading.CancellationToken cancellationToken);

        /// <summary>Runtime makes this request in order to submit an error response. It can be either a function error, or a runtime error. Error will be served in response to the invoke.</summary>
        /// <returns>Accepted</returns>
        /// <exception cref="RuntimeApiClientException">A server side error occurred.</exception>
        System.Threading.Tasks.Task<SwaggerResponse<StatusResponse>> Error2Async(string awsRequestId, string lambda_Runtime_Function_Error_Type, string errorJson);

        /// <summary>Runtime makes this request in order to submit an error response. It can be either a function error, or a runtime error. Error will be served in response to the invoke.</summary>
        /// <returns>Accepted</returns>
        /// <exception cref="RuntimeApiClientException">A server side error occurred.</exception>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        System.Threading.Tasks.Task<SwaggerResponse<StatusResponse>> Error2Async(string awsRequestId, string lambda_Runtime_Function_Error_Type, string errorJson, System.Threading.CancellationToken cancellationToken);

    }

    internal partial class InternalRuntimeApiClient : IInternalRuntimeApiClient
    {
        private const string ErrorContentType = "application/vnd.aws.lambda.error+json";

        private string _baseUrl = "/2018-06-01";
        private System.Net.Http.HttpClient _httpClient;

        public InternalRuntimeApiClient(System.Net.Http.HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public string BaseUrl
        {
            get { return _baseUrl; }
            set { _baseUrl = value; }
        }

        partial void PrepareRequest(System.Net.Http.HttpClient client, System.Net.Http.HttpRequestMessage request, string url);
        partial void PrepareRequest(System.Net.Http.HttpClient client, System.Net.Http.HttpRequestMessage request, System.Text.StringBuilder urlBuilder);
        partial void ProcessResponse(System.Net.Http.HttpClient client, System.Net.Http.HttpResponseMessage response);

        /// <summary>Non-recoverable initialization error. Runtime should exit after reporting the error. Error will be served in response to the first invoke.</summary>
        /// <returns>Accepted</returns>
        /// <exception cref="RuntimeApiClientException">A server side error occurred.</exception>
        public System.Threading.Tasks.Task<SwaggerResponse<StatusResponse>> ErrorAsync(string lambda_Runtime_Function_Error_Type, string errorJson)
        {
            return ErrorAsync(lambda_Runtime_Function_Error_Type, errorJson, System.Threading.CancellationToken.None);
        }

        /// <summary>Non-recoverable initialization error. Runtime should exit after reporting the error. Error will be served in response to the first invoke.</summary>
        /// <returns>Accepted</returns>
        /// <exception cref="RuntimeApiClientException">A server side error occurred.</exception>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public async System.Threading.Tasks.Task<SwaggerResponse<StatusResponse>> ErrorAsync(string lambda_Runtime_Function_Error_Type, string errorJson, System.Threading.CancellationToken cancellationToken)
        {
            var urlBuilder_ = new System.Text.StringBuilder();
            urlBuilder_.Append(BaseUrl != null ? BaseUrl.TrimEnd('/') : "").Append("/runtime/init/error");

            var client_ = _httpClient;
            try
            {
                using (var request_ = new System.Net.Http.HttpRequestMessage())
                {
                    if (lambda_Runtime_Function_Error_Type != null)
                        request_.Headers.TryAddWithoutValidation("Lambda-Runtime-Function-Error-Type", ConvertToString(lambda_Runtime_Function_Error_Type, System.Globalization.CultureInfo.InvariantCulture));
                    var content_ = new System.Net.Http.StringContent(errorJson);
                    content_.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(ErrorContentType);
                    request_.Content = content_;
                    request_.Method = new System.Net.Http.HttpMethod("POST");
                    request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

                    PrepareRequest(client_, request_, urlBuilder_);
                    var url_ = urlBuilder_.ToString();
                    request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);
                    PrepareRequest(client_, request_, url_);

                    var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    try
                    {
                        var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
                        if (response_.Content != null && response_.Content.Headers != null)
                        {
                            foreach (var item_ in response_.Content.Headers)
                                headers_[item_.Key] = item_.Value;
                        }

                        ProcessResponse(client_, response_);

                        var status_ = ((int)response_.StatusCode).ToString();
                        if (status_ == "202")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var result_ = default(StatusResponse);
                            try
                            {
                                result_ = ThirdParty.Json.LitJson.JsonMapper.ToObject<StatusResponse>(responseData_);
                                return new SwaggerResponse<StatusResponse>((int)response_.StatusCode, headers_, result_);
                            }
                            catch (System.Exception exception_)
                            {
                                throw new RuntimeApiClientException("Could not deserialize the response body.", (int)response_.StatusCode, responseData_, headers_, exception_);
                            }
                        }
                        else
                        if (status_ == "403")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var result_ = default(ErrorResponse);
                            try
                            {
                                result_ = ThirdParty.Json.LitJson.JsonMapper.ToObject<ErrorResponse>(responseData_);
                            }
                            catch (System.Exception exception_)
                            {
                                throw new RuntimeApiClientException("Could not deserialize the response body.", (int)response_.StatusCode, responseData_, headers_, exception_);
                            }
                            throw new RuntimeApiClientException<ErrorResponse>("Forbidden", (int)response_.StatusCode, responseData_, headers_, result_, null);
                        }
                        else
                        if (status_ == "500")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            throw new RuntimeApiClientException("Container error. Non-recoverable state. Runtime should exit promptly.\n", (int)response_.StatusCode, responseData_, headers_, null);
                        }
                        else
                        if (status_ != "200" && status_ != "204")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            throw new RuntimeApiClientException("The HTTP status code of the response was not expected (" + (int)response_.StatusCode + ").", (int)response_.StatusCode, responseData_, headers_, null);
                        }

                        return new SwaggerResponse<StatusResponse>((int)response_.StatusCode, headers_, default(StatusResponse));
                    }
                    finally
                    {
                        if (response_ != null)
                            response_.Dispose();
                    }
                }
            }
            finally
            {
            }
        }

        /// <summary>Runtime makes this HTTP request when it is ready to receive and process a new invoke.</summary>
        /// <returns>This is an iterator-style blocking API call. Response contains event JSON document, specific to the invoking service.</returns>
        /// <exception cref="RuntimeApiClientException">A server side error occurred.</exception>
        public System.Threading.Tasks.Task<SwaggerResponse<System.IO.Stream>> NextAsync()
        {
            return NextAsync(System.Threading.CancellationToken.None);
        }

        /// <summary>Runtime makes this HTTP request when it is ready to receive and process a new invoke.</summary>
        /// <returns>This is an iterator-style blocking API call. Response contains event JSON document, specific to the invoking service.</returns>
        /// <exception cref="RuntimeApiClientException">A server side error occurred.</exception>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public async System.Threading.Tasks.Task<SwaggerResponse<System.IO.Stream>> NextAsync(System.Threading.CancellationToken cancellationToken)
        {
            var urlBuilder_ = new System.Text.StringBuilder();
            urlBuilder_.Append(BaseUrl != null ? BaseUrl.TrimEnd('/') : "").Append("/runtime/invocation/next");

            var client_ = _httpClient;
            try
            {
                using (var request_ = new System.Net.Http.HttpRequestMessage())
                {
                    request_.Method = new System.Net.Http.HttpMethod("GET");
                    request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

                    PrepareRequest(client_, request_, urlBuilder_);
                    var url_ = urlBuilder_.ToString();
                    request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);
                    PrepareRequest(client_, request_, url_);

                    var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    try
                    {
                        var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
                        if (response_.Content != null && response_.Content.Headers != null)
                        {
                            foreach (var item_ in response_.Content.Headers)
                                headers_[item_.Key] = item_.Value;
                        }

                        ProcessResponse(client_, response_);

                        var status_ = ((int)response_.StatusCode).ToString();
                        if (status_ == "200")
                        {
                            var inputBuffer = response_.Content == null ? null : await response_.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                            return new SwaggerResponse<System.IO.Stream>((int)response_.StatusCode, headers_, new System.IO.MemoryStream(inputBuffer));
                        }
                        else
                        if (status_ == "403")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var result_ = default(ErrorResponse);
                            try
                            {
                                result_ = ThirdParty.Json.LitJson.JsonMapper.ToObject<ErrorResponse>(responseData_);
                            }
                            catch (System.Exception exception_)
                            {
                                throw new RuntimeApiClientException("Could not deserialize the response body.", (int)response_.StatusCode, responseData_, headers_, exception_);
                            }
                            throw new RuntimeApiClientException<ErrorResponse>("Forbidden", (int)response_.StatusCode, responseData_, headers_, result_, null);
                        }
                        else
                        if (status_ == "500")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            throw new RuntimeApiClientException("Container error. Non-recoverable state. Runtime should exit promptly.\n", (int)response_.StatusCode, responseData_, headers_, null);
                        }
                        else
                        if (status_ != "200" && status_ != "204")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            throw new RuntimeApiClientException("The HTTP status code of the response was not expected (" + (int)response_.StatusCode + ").", (int)response_.StatusCode, responseData_, headers_, null);
                        }

                        return new SwaggerResponse<System.IO.Stream>((int)response_.StatusCode, headers_, new System.IO.MemoryStream());
                    }
                    finally
                    {
                        if (response_ != null)
                            response_.Dispose();
                    }
                }
            }
            finally
            {
            }
        }

        /// <summary>Runtime makes this request in order to submit a response.</summary>
        /// <returns>Accepted</returns>
        /// <exception cref="RuntimeApiClientException">A server side error occurred.</exception>
        public System.Threading.Tasks.Task<SwaggerResponse<StatusResponse>> ResponseAsync(string awsRequestId, System.IO.Stream outputStream)
        {
            return ResponseAsync(awsRequestId, outputStream, System.Threading.CancellationToken.None);
        }

        /// <summary>Runtime makes this request in order to submit a response.</summary>
        /// <returns>Accepted</returns>
        /// <exception cref="RuntimeApiClientException">A server side error occurred.</exception>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public async System.Threading.Tasks.Task<SwaggerResponse<StatusResponse>> ResponseAsync(string awsRequestId, System.IO.Stream outputStream, System.Threading.CancellationToken cancellationToken)
        {
            if (awsRequestId == null)
                throw new System.ArgumentNullException("awsRequestId");

            var urlBuilder_ = new System.Text.StringBuilder();
            urlBuilder_.Append(BaseUrl != null ? BaseUrl.TrimEnd('/') : "").Append("/runtime/invocation/{AwsRequestId}/response");
            urlBuilder_.Replace("{AwsRequestId}", System.Uri.EscapeDataString(ConvertToString(awsRequestId, System.Globalization.CultureInfo.InvariantCulture)));

            var client_ = _httpClient;
            try
            {
                using (var request_ = new System.Net.Http.HttpRequestMessage())
                {
                    var content_ = new System.Net.Http.StreamContent(outputStream);
                    content_.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");
                    request_.Content = content_;
                    request_.Method = new System.Net.Http.HttpMethod("POST");
                    request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

                    PrepareRequest(client_, request_, urlBuilder_);
                    var url_ = urlBuilder_.ToString();
                    request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);
                    PrepareRequest(client_, request_, url_);

                    var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    try
                    {
                        var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
                        if (response_.Content != null && response_.Content.Headers != null)
                        {
                            foreach (var item_ in response_.Content.Headers)
                                headers_[item_.Key] = item_.Value;
                        }

                        ProcessResponse(client_, response_);

                        var status_ = ((int)response_.StatusCode).ToString();
                        if (status_ == "202")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var result_ = default(StatusResponse);
                            try
                            {
                                result_ = ThirdParty.Json.LitJson.JsonMapper.ToObject<StatusResponse>(responseData_);
                                return new SwaggerResponse<StatusResponse>((int)response_.StatusCode, headers_, result_);
                            }
                            catch (System.Exception exception_)
                            {
                                throw new RuntimeApiClientException("Could not deserialize the response body.", (int)response_.StatusCode, responseData_, headers_, exception_);
                            }
                        }
                        else
                        if (status_ == "400")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var result_ = default(ErrorResponse);
                            try
                            {
                                result_ = ThirdParty.Json.LitJson.JsonMapper.ToObject<ErrorResponse>(responseData_);
                            }
                            catch (System.Exception exception_)
                            {
                                throw new RuntimeApiClientException("Could not deserialize the response body.", (int)response_.StatusCode, responseData_, headers_, exception_);
                            }
                            throw new RuntimeApiClientException<ErrorResponse>("Bad Request", (int)response_.StatusCode, responseData_, headers_, result_, null);
                        }
                        else
                        if (status_ == "403")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var result_ = default(ErrorResponse);
                            try
                            {
                                result_ = ThirdParty.Json.LitJson.JsonMapper.ToObject<ErrorResponse>(responseData_);
                            }
                            catch (System.Exception exception_)
                            {
                                throw new RuntimeApiClientException("Could not deserialize the response body.", (int)response_.StatusCode, responseData_, headers_, exception_);
                            }
                            throw new RuntimeApiClientException<ErrorResponse>("Forbidden", (int)response_.StatusCode, responseData_, headers_, result_, null);
                        }
                        else
                        if (status_ == "413")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var result_ = default(ErrorResponse);
                            try
                            {
                                result_ = ThirdParty.Json.LitJson.JsonMapper.ToObject<ErrorResponse>(responseData_);
                            }
                            catch (System.Exception exception_)
                            {
                                throw new RuntimeApiClientException("Could not deserialize the response body.", (int)response_.StatusCode, responseData_, headers_, exception_);
                            }
                            throw new RuntimeApiClientException<ErrorResponse>("Payload Too Large", (int)response_.StatusCode, responseData_, headers_, result_, null);
                        }
                        else
                        if (status_ == "500")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            throw new RuntimeApiClientException("Container error. Non-recoverable state. Runtime should exit promptly.\n", (int)response_.StatusCode, responseData_, headers_, null);
                        }
                        else
                        if (status_ != "200" && status_ != "204")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            throw new RuntimeApiClientException("The HTTP status code of the response was not expected (" + (int)response_.StatusCode + ").", (int)response_.StatusCode, responseData_, headers_, null);
                        }

                        return new SwaggerResponse<StatusResponse>((int)response_.StatusCode, headers_, default(StatusResponse));
                    }
                    finally
                    {
                        if (response_ != null)
                            response_.Dispose();
                    }
                }
            }
            finally
            {
            }
        }

        /// <summary>Runtime makes this request in order to submit an error response. It can be either a function error, or a runtime error. Error will be served in response to the invoke.</summary>
        /// <returns>Accepted</returns>
        /// <exception cref="RuntimeApiClientException">A server side error occurred.</exception>
        public System.Threading.Tasks.Task<SwaggerResponse<StatusResponse>> Error2Async(string awsRequestId, string lambda_Runtime_Function_Error_Type, string errorJson)
        {
            return Error2Async(awsRequestId, lambda_Runtime_Function_Error_Type, errorJson, System.Threading.CancellationToken.None);
        }

        /// <summary>Runtime makes this request in order to submit an error response. It can be either a function error, or a runtime error. Error will be served in response to the invoke.</summary>
        /// <returns>Accepted</returns>
        /// <exception cref="RuntimeApiClientException">A server side error occurred.</exception>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public async System.Threading.Tasks.Task<SwaggerResponse<StatusResponse>> Error2Async(string awsRequestId, string lambda_Runtime_Function_Error_Type, string errorJson, System.Threading.CancellationToken cancellationToken)
        {
            if (awsRequestId == null)
                throw new System.ArgumentNullException("awsRequestId");

            var urlBuilder_ = new System.Text.StringBuilder();
            urlBuilder_.Append(BaseUrl != null ? BaseUrl.TrimEnd('/') : "").Append("/runtime/invocation/{AwsRequestId}/error");
            urlBuilder_.Replace("{AwsRequestId}", System.Uri.EscapeDataString(ConvertToString(awsRequestId, System.Globalization.CultureInfo.InvariantCulture)));

            var client_ = _httpClient;
            try
            {
                using (var request_ = new System.Net.Http.HttpRequestMessage())
                {
                    if (lambda_Runtime_Function_Error_Type != null)
                        request_.Headers.TryAddWithoutValidation("Lambda-Runtime-Function-Error-Type", ConvertToString(lambda_Runtime_Function_Error_Type, System.Globalization.CultureInfo.InvariantCulture));
                    var content_ = new System.Net.Http.StringContent(errorJson);
                    content_.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(ErrorContentType);
                    request_.Content = content_;
                    request_.Method = new System.Net.Http.HttpMethod("POST");
                    request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

                    PrepareRequest(client_, request_, urlBuilder_);
                    var url_ = urlBuilder_.ToString();
                    request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);
                    PrepareRequest(client_, request_, url_);

                    var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    try
                    {
                        var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
                        if (response_.Content != null && response_.Content.Headers != null)
                        {
                            foreach (var item_ in response_.Content.Headers)
                                headers_[item_.Key] = item_.Value;
                        }

                        ProcessResponse(client_, response_);

                        var status_ = ((int)response_.StatusCode).ToString();
                        if (status_ == "202")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var result_ = default(StatusResponse);
                            try
                            {
                                result_ = ThirdParty.Json.LitJson.JsonMapper.ToObject<StatusResponse>(responseData_);
                                return new SwaggerResponse<StatusResponse>((int)response_.StatusCode, headers_, result_);
                            }
                            catch (System.Exception exception_)
                            {
                                throw new RuntimeApiClientException("Could not deserialize the response body.", (int)response_.StatusCode, responseData_, headers_, exception_);
                            }
                        }
                        else
                        if (status_ == "400")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var result_ = default(ErrorResponse);
                            try
                            {
                                result_ = ThirdParty.Json.LitJson.JsonMapper.ToObject<ErrorResponse>(responseData_);
                            }
                            catch (System.Exception exception_)
                            {
                                throw new RuntimeApiClientException("Could not deserialize the response body.", (int)response_.StatusCode, responseData_, headers_, exception_);
                            }
                            throw new RuntimeApiClientException<ErrorResponse>("Bad Request", (int)response_.StatusCode, responseData_, headers_, result_, null);
                        }
                        else
                        if (status_ == "403")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var result_ = default(ErrorResponse);
                            try
                            {
                                result_ = ThirdParty.Json.LitJson.JsonMapper.ToObject<ErrorResponse>(responseData_);
                            }
                            catch (System.Exception exception_)
                            {
                                throw new RuntimeApiClientException("Could not deserialize the response body.", (int)response_.StatusCode, responseData_, headers_, exception_);
                            }
                            throw new RuntimeApiClientException<ErrorResponse>("Forbidden", (int)response_.StatusCode, responseData_, headers_, result_, null);
                        }
                        else
                        if (status_ == "500")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            throw new RuntimeApiClientException("Container error. Non-recoverable state. Runtime should exit promptly.\n", (int)response_.StatusCode, responseData_, headers_, null);
                        }
                        else
                        if (status_ != "200" && status_ != "204")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            throw new RuntimeApiClientException("The HTTP status code of the response was not expected (" + (int)response_.StatusCode + ").", (int)response_.StatusCode, responseData_, headers_, null);
                        }

                        return new SwaggerResponse<StatusResponse>((int)response_.StatusCode, headers_, default(StatusResponse));
                    }
                    finally
                    {
                        if (response_ != null)
                            response_.Dispose();
                    }
                }
            }
            finally
            {
            }
        }

        private string ConvertToString(object value, System.Globalization.CultureInfo cultureInfo)
        {
            if (value is System.Enum)
            {
                string name = System.Enum.GetName(value.GetType(), value);
                if (name != null)
                {
                    var field = System.Reflection.IntrospectionExtensions.GetTypeInfo(value.GetType()).GetDeclaredField(name);
                    if (field != null)
                    {
                        var attribute = System.Reflection.CustomAttributeExtensions.GetCustomAttribute(field, typeof(System.Runtime.Serialization.EnumMemberAttribute))
                            as System.Runtime.Serialization.EnumMemberAttribute;
                        if (attribute != null)
                        {
                            return attribute.Value;
                        }
                    }
                }
            }
            else if (value is bool)
            {
                return System.Convert.ToString(value, cultureInfo).ToLowerInvariant();
            }
            else if (value is byte[])
            {
                return System.Convert.ToBase64String((byte[])value);
            }
            else if (value != null && value.GetType().IsArray)
            {
                var array = System.Linq.Enumerable.OfType<object>((System.Array)value);
                return string.Join(",", System.Linq.Enumerable.Select(array, o => ConvertToString(o, cultureInfo)));
            }

            return System.Convert.ToString(value, cultureInfo);
        }
    }

    internal partial class StatusResponse
    {
        public string status { get; set; }
    }

    internal partial class ErrorResponse
    {
        public string errorMessage { get; set; }
        public string errorType { get; set; }
    }

    internal partial class SwaggerResponse
    {
        public int StatusCode { get; private set; }

        public System.Collections.Generic.Dictionary<string, System.Collections.Generic.IEnumerable<string>> Headers { get; private set; }

        public SwaggerResponse(int statusCode, System.Collections.Generic.Dictionary<string, System.Collections.Generic.IEnumerable<string>> headers)
        {
            StatusCode = statusCode;
            Headers = headers;
        }
    }

    internal partial class SwaggerResponse<TResult> : SwaggerResponse
    {
        public TResult Result { get; private set; }

        public SwaggerResponse(int statusCode, System.Collections.Generic.Dictionary<string, System.Collections.Generic.IEnumerable<string>> headers, TResult result)
            : base(statusCode, headers)
        {
            Result = result;
        }
    }

    public partial class RuntimeApiClientException : System.Exception
    {
        public int StatusCode { get; private set; }

        public string Response { get; private set; }

        public System.Collections.Generic.Dictionary<string, System.Collections.Generic.IEnumerable<string>> Headers { get; private set; }

        public RuntimeApiClientException(string message, int statusCode, string response, System.Collections.Generic.Dictionary<string, System.Collections.Generic.IEnumerable<string>> headers, System.Exception innerException)
            : base(message + "\n\nStatus: " + statusCode + "\nResponse: \n" + response.Substring(0, response.Length >= 512 ? 512 : response.Length), innerException)
        {
            StatusCode = statusCode;
            Response = response;
            Headers = headers;
        }

        public override string ToString()
        {
            return string.Format("HTTP Response: \n\n{0}\n\n{1}", Response, base.ToString());
        }
    }

    public partial class RuntimeApiClientException<TResult> : RuntimeApiClientException
    {
        public TResult Result { get; private set; }

        public RuntimeApiClientException(string message, int statusCode, string response, System.Collections.Generic.Dictionary<string, System.Collections.Generic.IEnumerable<string>> headers, TResult result, System.Exception innerException)
            : base(message, statusCode, response, headers, innerException)
        {
            Result = result;
        }
    }

}