using FOAEA3.Model;
using FOAEA3.Model.Constants;
using FOAEA3.Model.Enums;
using FOAEA3.Model.Interfaces;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Net;
using System.Text;

namespace FOAEA3.Common.Helpers
{
    public class APIBrokerHelper : IAPIBrokerHelper
    {
        private static readonly TimeSpan DEFAULT_TIMEOUT = new(0, 20, 30);

        private string _APIroot;
        public string CurrentSubmitter { get; set; }
        public string CurrentUser { get; set; }
        public string CurrentLanguage { get; set; }

        public Func<string> GetToken { get; set; }
        public Func<Task<string>> GetRefreshedToken { get; set; }

        public MessageDataList ErrorData { get; set; }

        public string APIroot
        {
            get => _APIroot;
            set => _APIroot = value + (value.EndsWith('/') ? "" : "/");
        }

        public APIBrokerHelper(string apiRoot = "", string currentSubmitter = "", string currentUser = "",
                               Func<string> getToken = null, Func<Task<string>> getRefreshedToken = null)
        {
            APIroot = apiRoot;
            CurrentSubmitter = currentSubmitter;
            CurrentUser = currentUser;
            GetToken = getToken;
            GetRefreshedToken = getRefreshedToken;
            ErrorData = new MessageDataList();
        }

        public static async Task<T> GetDataFromRequestBody<T>(HttpRequest request)
        {
            string bodyDataAsJSON;
            using (var reader = new StreamReader(request.Body, Encoding.UTF8))
            {
                bodyDataAsJSON = await reader.ReadToEndAsync();
            }

            return JsonConvert.DeserializeObject<T>(bodyDataAsJSON);
        }

        public async Task<string> GetString(string api, string root = "",
                                                 int maxAttempts = GlobalConfiguration.MAX_API_ATTEMPTS,
                                                 string token = null)
        {
            if ((token is null) && (GetToken is not null))
                token = GetToken();

            string result = string.Empty;

            if (root == "")
                root = APIroot;

            int attemptCount = 0;
            bool completed = false;

            using var httpClient = CreateHttpClient(token);

            while ((attemptCount < maxAttempts) && (!completed))
            {
                try
                {
                    var callResult = await httpClient.GetAsync(root + api);

                    if (callResult.IsSuccessStatusCode)
                    {
                        result = await callResult.Content.ReadAsStringAsync();
                        completed = true;
                    }
                    else if (callResult.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        if (GetRefreshedToken is null)
                            completed = true;
                        else
                        {
                            token = await GetRefreshedToken();
                            if (string.IsNullOrEmpty(token))
                                completed = true; // API failed and refreshed token expired
                            else
                                attemptCount++;
                        }
                    }
                    else
                        completed = true;
                }
                catch (Exception e)
                {
                    attemptCount++;
                    Thread.Sleep(GlobalConfiguration.TIME_BETWEEN_RETRIES); // wait half a second between each attempt
                    if (attemptCount == maxAttempts)
                    {
                        var errorMessageData = new MessageData(EventCode.UNDEFINED, "Error", api + ":" + e.Message, MessageType.Error);
                        ErrorData.Add(errorMessageData);
                    }
                }
            }

            return result;

        }

        public async Task<T> GetData<T>(string api, string root = "", string token = null)
            where T : new()
        {
            if ((token is null) && (GetToken is not null))
                token = GetToken();

            T result = default;

            if (root == "")
                root = APIroot;

            using var httpClient = CreateHttpClient(token);

            int attemptCount = 0;
            bool completed = false;

            while ((attemptCount < GlobalConfiguration.MAX_API_ATTEMPTS) && (!completed))
            {
                try
                {
                    var callResult = await httpClient.GetAsync(root + api);

                    if (callResult.IsSuccessStatusCode)
                    {
                        string content = await callResult.Content.ReadAsStringAsync();

                        result = JsonConvert.DeserializeObject<T>(content);
                    }
                    else if (callResult.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        string content = await callResult.Content.ReadAsStringAsync();
                        try
                        {
                            var errorData = JsonConvert.DeserializeObject<MessageDataList>(content);
                            for (int i = 0; i < errorData?.Count; i++)
                            {
                                var thisError = errorData[i];
                                var error = new MessageData(thisError.Code, thisError.Field, api + ":" + thisError.Description, thisError.Severity, thisError.IsSystemMessage, thisError.URL);
                                errorData[i] = error;
                            }
                            if (errorData is not null)
                                ErrorData = errorData;
                        }
                        catch
                        {
                            var errorMessageData = new MessageData(EventCode.UNDEFINED, "Error", api + ":" + content, MessageType.Error);
                            ErrorData.Add(errorMessageData);
                        }
                    }
                    else if (callResult.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        if (GetRefreshedToken is null)
                            completed = true;
                        else
                        {
                            token = await GetRefreshedToken();
                            if (string.IsNullOrEmpty(token))
                                completed = true; // API failed and refreshed token expired
                            else
                                attemptCount++;
                        }
                    }
                    completed = true;
                }
                catch (Exception e)
                {
                    attemptCount++;
                    Thread.Sleep(GlobalConfiguration.TIME_BETWEEN_RETRIES); // wait half a second between each attempt
                    if (attemptCount == GlobalConfiguration.MAX_API_ATTEMPTS)
                    {
                        var errorMessageData = new MessageData(EventCode.UNDEFINED, "Error", api + ":" + e.Message, MessageType.Error);
                        ErrorData.Add(errorMessageData);
                    }
                }
            }

            result ??= new T();

            return result;

        }

        public async Task<T> GetData<T, P>(string api, P data, string root = "", string token = null) where T : new()
        {
            if ((token is null) && (GetToken is not null))
                token = GetToken();

            T result = default;

            if (root == "")
                root = APIroot;

            using var httpClient = CreateHttpClient(token);

            int attemptCount = 0;
            bool completed = false;

            string keyData = JsonConvert.SerializeObject(data);

            while ((attemptCount < GlobalConfiguration.MAX_API_ATTEMPTS) && (!completed))
            {
                try
                {
                    HttpResponseMessage callResult;
                    using (var content = new StringContent(keyData, Encoding.UTF8, "application/json"))
                    {
                        var request = new HttpRequestMessage
                        {
                            Method = HttpMethod.Get,
                            RequestUri = new Uri(root + api),
                            Content = content
                        };
                        callResult = await httpClient.SendAsync(request).ConfigureAwait(false);
                    }

                    if (callResult.IsSuccessStatusCode)
                    {
                        string content = await callResult.Content.ReadAsStringAsync();
                        result = JsonConvert.DeserializeObject<T>(content);
                    }
                    else if (callResult.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        string content = await callResult.Content.ReadAsStringAsync();
                        try
                        {
                            var errorData = JsonConvert.DeserializeObject<MessageDataList>(content);
                            for (int i = 0; i < errorData?.Count; i++)
                            {
                                var thisError = errorData[i];
                                var error = new MessageData(thisError.Code, thisError.Field, api + ":" + thisError.Description, thisError.Severity, thisError.IsSystemMessage, thisError.URL);
                                errorData[i] = error;
                            }
                            if (errorData is not null)
                                ErrorData = errorData;
                        }
                        catch
                        {
                            var errorMessageData = new MessageData(EventCode.UNDEFINED, "Error", api + ":" + content, MessageType.Error);
                            ErrorData.Add(errorMessageData);
                        }
                    }
                    else if (callResult.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        if (GetRefreshedToken is null)
                            completed = true;
                        else
                        {
                            token = await GetRefreshedToken();
                            if (string.IsNullOrEmpty(token))
                                completed = true; // API failed and refreshed token expired
                            else
                                attemptCount++;
                        }
                    }
                    completed = true;
                }
                catch (Exception e)
                {
                    attemptCount++;
                    Thread.Sleep(GlobalConfiguration.TIME_BETWEEN_RETRIES); // wait half a second between each attempt
                    if (attemptCount == GlobalConfiguration.MAX_API_ATTEMPTS)
                    {
                        var errorMessageData = new MessageData(EventCode.UNDEFINED, "Error", api + ":" + e.Message, MessageType.Error);
                        ErrorData.Add(errorMessageData);
                    }
                }
            }

            result ??= new T();

            return result;
        }

        public async Task<T> PostData<T, P>(string api, P data, string root = "", string token = null)
            where T : class, new()
            where P : class
        {
            return await SendDataAsync<T, P>(api, data, "POST", root, token);
        }

        public async Task<T> PutData<T, P>(string api, P data, string root = "", string token = null)
            where T : class, new()
            where P : class
        {
            return await SendDataAsync<T, P>(api, data, "PUT", root, token);
        }

        public async Task<string> PostDataGetString<P>(string api, P data, string root = "", string token = null) where P : class
        {
            return await SendDataGetStringAsync<P>(api, data, "POST", root, token);
        }

        public async Task<string> PutDataGetString<P>(string api, P data, string root = "", string token = null) where P : class
        {
            return await SendDataGetStringAsync<P>(api, data, "PUT", root, token);
        }

        public async Task SendCommand(string api, string root = "", string token = null)
        {
            await SendCommandAsync(api, "PUT", root, token);
        }

        private async Task<T> SendDataAsync<T, P>(string api, P data, string method, string root = "",
                                                  string token = null)
            where T : class, new()
            where P : class
        {
            if ((token is null) && (GetToken is not null))
                token = GetToken();

            T result = null;

            if (root == "")
                root = APIroot;

            int attemptCount = 0;
            bool completed = false;

            using var httpClient = CreateHttpClient(token);

            string keyData = JsonConvert.SerializeObject(data);

            while ((attemptCount < GlobalConfiguration.MAX_API_ATTEMPTS) && (!completed))
            {
                try
                {
                    HttpResponseMessage callResult;
                    using (var content = new StringContent(keyData, Encoding.UTF8, "application/json"))
                        callResult = method switch
                        {
                            "POST" => await httpClient.PostAsync(root + api, content),
                            "PUT" => await httpClient.PutAsync(root + api, content),
                            _ => null,
                        };

                    if (callResult != null)
                    {
                        if (callResult.IsSuccessStatusCode)
                        {
                            string content = await callResult.Content.ReadAsStringAsync();
                            result = JsonConvert.DeserializeObject<T>(content);
                            completed = true;
                        }
                        else if (callResult.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            if (GetRefreshedToken is null)
                                completed = true;
                            else
                            {
                                token = await GetRefreshedToken();
                                if (string.IsNullOrEmpty(token))
                                    completed = true; // API failed and refreshed token expired
                                else
                                    attemptCount++;
                            }
                        }
                        else
                        {
                            string content = string.Empty;
                            try
                            {
                                content = await callResult.Content.ReadAsStringAsync();
                                result = JsonConvert.DeserializeObject<T>(content);
                            }
                            catch
                            {
                                // content could not be converted, so just return as is
                            }

                            string message = $"API return code: {callResult.StatusCode} for call [{method} {root + api}] Content: [{content}]";
                            var errorMessageData = new MessageData(EventCode.UNDEFINED, "Error", api + ":" + message, MessageType.Error);
                            ErrorData.Add(errorMessageData);

                            completed = true;
                        }
                    }
                    else
                        completed = true;
                }
                catch (Exception e)
                {
                    attemptCount++;
                    Thread.Sleep(GlobalConfiguration.TIME_BETWEEN_RETRIES); // wait half a second between each attempt
                    if (attemptCount == GlobalConfiguration.MAX_API_ATTEMPTS)
                    {
                        var errorMessageData = new MessageData(EventCode.UNDEFINED, "Error", api + ":" + e.Message, MessageType.Error);
                        ErrorData.Add(errorMessageData);
                    }
                }
            }

            result ??= new T();

            return result;

        }

        private async Task<string> SendDataGetStringAsync<P>(string api, P data, string method,
                                                             string root = "", string token = null) where P : class
        {
            if ((token is null) && (GetToken is not null))
                token = GetToken();

            string result = string.Empty;

            if (root == "")
                root = APIroot;

            int attemptCount = 0;
            bool completed = false;

            using var httpClient = CreateHttpClient(token);

            string keyData = JsonConvert.SerializeObject(data);

            while ((attemptCount < GlobalConfiguration.MAX_API_ATTEMPTS) && (!completed))
            {
                try
                {
                    HttpResponseMessage callResult;
                    using (var content = new StringContent(keyData, Encoding.UTF8, "application/json"))
                        callResult = method switch
                        {
                            "POST" => await httpClient.PostAsync(root + api, content),
                            "PUT" => await httpClient.PutAsync(root + api, content),
                            _ => null,
                        };

                    if (callResult != null)
                    {
                        if (callResult.IsSuccessStatusCode)
                        {
                            result = await callResult.Content.ReadAsStringAsync();
                            completed = true;
                        }
                        else if (callResult.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            if (GetRefreshedToken is null)
                                completed = true;
                            else
                            {
                                token = await GetRefreshedToken();
                                if (string.IsNullOrEmpty(token))
                                    completed = true;
                                else
                                    attemptCount++;
                            }
                        }
                        else
                            completed = true;
                    }
                    else
                        completed = true;

                }
                catch (Exception e)
                {
                    attemptCount++;
                    Thread.Sleep(GlobalConfiguration.TIME_BETWEEN_RETRIES); // wait half a second between each attempt
                    if (attemptCount == GlobalConfiguration.MAX_API_ATTEMPTS)
                    {
                        // log error
                        var errorMessageData = new MessageData(EventCode.UNDEFINED, "Error", api + ":" + e.Message, MessageType.Error);
                        ErrorData.Add(errorMessageData);
                    }
                }
            }

            return result;

        }

        private async Task SendCommandAsync(string api, string method, string root = "", string token = null)
        {
            if ((token is null) && (GetToken is not null))
                token = GetToken();

            if (root == "")
                root = APIroot;

            using var httpClient = CreateHttpClient(token);

            HttpResponseMessage callResult;
            callResult = method switch
            {
                "POST" => await httpClient.PostAsync(root + api, null),
                "PUT" => await httpClient.PutAsync(root + api, null),
                _ => null,
            };

        }

        public async Task<HttpResponseMessage> PostJsonFile(string api, string jsonData,
                                                                 string rootAPI = null, string token = null)
        {
            if ((token is null) && (GetToken is not null))
                token = GetToken();

            using var httpClient = CreateHttpClient(token);

            using var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

            string root = rootAPI ?? APIroot;
            return await httpClient.PostAsync(root + api, content);
        }

        public async Task<HttpResponseMessage> PostFlatFile(string api, string flatFileData,
                                                                 string rootAPI = null, string token = null)
        {
            if ((token is null) && (GetToken is not null))
                token = GetToken();

            using var httpClient = CreateHttpClient(token);

            using var content = new StringContent(flatFileData, Encoding.UTF8, "text/plain");

            string root = rootAPI ?? APIroot;
            return await httpClient.PostAsync(root + api, content);
        }

        private HttpClient CreateHttpClient(string token)
        {
            var httpClient = new HttpClient
            {
                Timeout = DEFAULT_TIMEOUT
            };

            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Add("CurrentSubmitter", CurrentSubmitter);
            httpClient.DefaultRequestHeaders.Add("CurrentSubject", CurrentUser);
            if (token is not null && !string.IsNullOrEmpty(token))
                httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
            if (!string.IsNullOrEmpty(CurrentLanguage))
                httpClient.DefaultRequestHeaders.Add("Accept-Language", CurrentLanguage);

            return httpClient;
        }

    }

}
