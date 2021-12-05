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

using System.Text.Json;

namespace Amazon.Lambda.RuntimeSupport
{
    internal partial interface IInternalRuntimeApiClient
    {
        /// <summary>
        /// This is a copy of the generated Error2Async method but adds support for the unmodeled header `Lambda-Runtime-Function-XRay-Error-Cause`.
        /// </summary>
        /// <param name="awsRequestId"></param>
        /// <param name="lambda_Runtime_Function_Error_Type"></param>
        /// <param name="errorJson"></param>
        /// <param name="xrayCause"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        System.Threading.Tasks.Task<SwaggerResponse<StatusResponse>> ErrorWithXRayCauseAsync(string awsRequestId, string lambda_Runtime_Function_Error_Type, string errorJson, string xrayCause, System.Threading.CancellationToken cancellationToken);
    }

    internal partial class InternalRuntimeApiClient : IInternalRuntimeApiClient
    {
        const int MAX_HEADER_SIZE_BYTES = 1024 * 1024;

        /// <summary>
        /// This is a copy of the generated Error2Async method but adds support for the unmodeled header `Lambda-Runtime-Function-XRay-Error-Cause`.
        /// </summary>
        /// <param name="awsRequestId"></param>
        /// <param name="lambda_Runtime_Function_Error_Type"></param>
        /// <param name="errorJson"></param>
        /// <param name="xrayCause"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async System.Threading.Tasks.Task<SwaggerResponse<StatusResponse>> ErrorWithXRayCauseAsync(string awsRequestId, string lambda_Runtime_Function_Error_Type, string errorJson, string xrayCause, System.Threading.CancellationToken cancellationToken)
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

                    // This is the unmodeled X-Ray header to report back the cause of errors.
                    if (xrayCause != null && System.Text.Encoding.UTF8.GetByteCount(xrayCause) < MAX_HEADER_SIZE_BYTES)
                    {
                        // Headers can not have newlines. The X-Ray JSON writer should not have put any in but do a final check of newlines.
                        xrayCause = xrayCause.Replace("\r\n", "").Replace("\n", "");

                        try
                        {
                            request_.Headers.Add("Lambda-Runtime-Function-XRay-Error-Cause", xrayCause);
                        }
                        catch
                        {
                            // Don't prevent reporting errors to Lambda if there are any issues adding the X-Ray cause JSON as a header.
                        }
                    }

                    using (var content_ = new System.Net.Http.StringContent(errorJson))
                    {
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
                                    result_ = JsonSerializer.Deserialize<StatusResponse>(responseData_);
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
                                    result_ = JsonSerializer.Deserialize<ErrorResponse>(responseData_);
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
                                    result_ = JsonSerializer.Deserialize <ErrorResponse >(responseData_);
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
            }
            finally
            {
            }
        }
    }
}