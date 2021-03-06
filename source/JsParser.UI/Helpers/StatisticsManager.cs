﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JsParser.UI.Properties;
using System.Net;
using System.Threading;
using System.IO;
using System.Diagnostics;
using fastJSON;

namespace JsParser.UI.Helpers
{
    public class StatisticsManager
    {
        private static StatisticsManager _statMan;
        public Statistics Statistics { get; private set; }

        static StatisticsManager()
        {
            _statMan = new StatisticsManager();
        }

        private StatisticsManager()
        {
            if (string.IsNullOrEmpty(Settings.Default.Statistics))
            {
                Statistics = new Statistics();
                Statistics.FirstInitialize();
            }
            else
            {
                try
                {
                    Statistics = JSON.ToObject<Statistics>(Settings.Default.Statistics);
                }
                catch
                {
                    Statistics = new Statistics();
                    Statistics.FirstInitialize();
                }
            }

            Statistics.UpdateStatisticsOnStart();
        }

        public static StatisticsManager Instance
        {
            get { return _statMan; }
        }

        public void UpdateSettingsWithStatistics()
        {
            Settings.Default.Statistics = GetSerializedStatistics();
        }

        private string GetSerializedStatistics()
        {
            return JSON.ToNiceJSON(Statistics, new JSONParameters() { UseExtensions = false, UseFastGuid = false });
        }

        public void Save()
        {
            UpdateSettingsWithStatistics();
            Settings.Default.Save();
        }

        public void SubmitStatisticsToServer(bool force = false)
        {
            var serverUrl = "https://api.backendless.com/v1/data/plugin_usage";
            var headers = new Dictionary<string, string>()
            {
                { "application-id", "7FDF78C3-51B1-3F8F-FF3B-F2F4B4287C00" },
                { "secret-key", "1F901130-6AE6-4D3C-FF65-AA4808E44F00" },
                { "application-type", "REST" }
            };
            
            if (force || Statistics.LastSubmittedTime.AddDays(1) < DateTime.UtcNow)
            {
                Save();

                ThreadPool.QueueUserWorkItem(state =>
                {
                    try
                    {
                        //This code is copy-pasted from http://msdn.microsoft.com/en-us/library/debx8sh9.aspx

                        // Create a request using a URL that can receive a post. 
                        WebRequest request = WebRequest.Create(serverUrl);
                        // Set the Method property of the request to POST.
                        request.Method = "POST";
                        // Create POST data and convert it to a byte array.
                        Statistics.CurrentTime = DateTime.UtcNow;
                        string postData = GetSerializedStatistics();

                        byte[] byteArray = Encoding.UTF8.GetBytes(postData);
                        // Set the ContentType property of the WebRequest.
                        request.ContentType = "application/json";
                        // Set the ContentLength property of the WebRequest.
                        request.ContentLength = byteArray.Length;

                        // Add more headers
                        foreach (var kv in headers)
                        {
                            request.Headers.Add(kv.Key, kv.Value);
                        }

                        // Get the request stream.
                        Stream dataStream = request.GetRequestStream();
                        // Write the data to the request stream.
                        dataStream.Write(byteArray, 0, byteArray.Length);
                        // Close the Stream object.
                        dataStream.Close();

                        // Get the response.
                        var response = (HttpWebResponse) request.GetResponse();
                        // Display the status.
                        Trace.WriteLine(response.StatusDescription);
                        // Get the stream containing content returned by the server.
                        dataStream = response.GetResponseStream();
                        // Open the stream using a StreamReader for easy access.
                        StreamReader reader = new StreamReader(dataStream);
                        // Read the content.
                        string responseFromServer = reader.ReadToEnd();
                        // Display the content.
                        Trace.WriteLine(responseFromServer);
                        // Clean up the streams.
                        reader.Close();
                        dataStream.Close();
                        response.Close();

                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            Statistics.LastSubmittedTime = DateTime.UtcNow;
                            Save();
                        }
                    }
                    catch
                    {
                    }
                });
            }
        }
    }
}
