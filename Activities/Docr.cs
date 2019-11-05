using Dbrain.UiPath.Docr.Activities.Properties;
using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json;

namespace Dbrain.UiPath.Docr.Activities
{
    public enum Actions
    {
        Classify = 1,
        Recognize = 2,
        ClassifyRecognize = 3,
        ClassifyRecognizeHitl = 4,
    }

    public struct ClassifyDocInfo
    {
        public string Type { get; set; }
        public string Rotation { get; set; }
    }
    public struct ClassifyItem
    {
        public ClassifyDocInfo Document { get; set; }
        public string Crop { get; set; }
    }
    public struct ClassifyResponse
    {
        public ClassifyItem[] Items;
    }

    public struct FieldInfo
    {
        public string Text { get; set; }
        public float Confidence { get; set; }
    }

    public struct RecognizeItem
    {
        public Dictionary<string,FieldInfo> Fields { get; set; }
        public string DocType { get; set; }
    }
    public struct RecognizeResponse
    {
        public RecognizeItem[] Items;
    }

    [LocalizedCategory(nameof(Resources.DbrainOCR))]
    [LocalizedDisplayName(nameof(Resources.DocrName))]
    [LocalizedDescription(nameof(Resources.DocrDescription))]
    public class Docr : CodeActivity
    {
        protected static string BaseCloudGateWay = "https://latest.dbrain.io";

        // Inputs
        [LocalizedCategory(nameof(Resources.Input))]
        [LocalizedDisplayName(nameof(Resources.ImageName))]
        [LocalizedDescription(nameof(Resources.ImageDescription))]
        [RequiredArgument]
        public InArgument<String> ImagePath { get; set; }

        // Outputs
        [LocalizedCategory(nameof(Resources.Output))]
        [LocalizedDisplayName(nameof(Resources.JsonName))]
        [LocalizedDescription(nameof(Resources.JsonDescription))]
        public OutArgument<string> Json { get; set; }

        [LocalizedCategory(nameof(Resources.Output))]
        [LocalizedDisplayName(nameof(Resources.HtmlName))]
        [LocalizedDescription(nameof(Resources.HtmlDescription))]
        public OutArgument<string> Html { get; set; }

        [LocalizedCategory(nameof(Resources.Output))]
        [LocalizedDisplayName(nameof(Resources.ErrorName))]
        [LocalizedDescription(nameof(Resources.ErrorDescription))]
        public OutArgument<String> Error { get; set; }

        [LocalizedCategory(nameof(Resources.Options))]
        [LocalizedDisplayName(nameof(Resources.ApiGatewayName))]
        [LocalizedDescription(nameof(Resources.ApiGatewayDescription))]
        public InArgument<string> ApiGateway { get; set; }

        [LocalizedCategory(nameof(Resources.Options))]
        [LocalizedDisplayName(nameof(Resources.ApiTokenName))]
        [LocalizedDescription(nameof(Resources.ApiTokenDescription))]
        [RequiredArgument]
        public InArgument<string> ApiToken { get; set; }


        private HttpClient BuildClient(string apiToken)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Token " + apiToken);
            return client;
        }

        private (bool Success, string Body) MakeRequest(HttpClient client, string url, FileStream image)
        {
            _ = image.Seek(0, SeekOrigin.Begin);
            MultipartFormDataContent form = new MultipartFormDataContent
            {
                { new StreamContent(image), "image", image.Name }
            };

            HttpResponseMessage response = client.PostAsync(url, form).Result;
            string json = response.Content.ReadAsStringAsync().Result;
            return (response.IsSuccessStatusCode, json);
        }

        private (bool Success, string Crop, string DocType, string Err) Classify(HttpClient client, string gateway, FileStream image)
        {
            string url = gateway + "/classify";
            (bool Success, string Body) = MakeRequest(client, url, image);
            if (Success)
            {
                ClassifyResponse body = JsonConvert.DeserializeObject<ClassifyResponse>(Body);

               // byte[] crop = null; Convert.FromBase64String(body.Items[0].Crop);
                string docType = body.Items[0].Document.Type;
                return (true, body.Items[0].Crop, docType, Body);
            }
            return (false, null, "", Body);
        }

        private (bool Success, Dictionary<string, FieldInfo> Fields, string Err) Recognize(HttpClient client, string gateway, FileStream image, string docType, bool hitl = false)
        {
            string url = string.Format("{0}/{1}?doc_type={2}&with_hitl={3}", gateway, "recognize", docType, hitl);
            (bool Success, string Body) = MakeRequest(client, url, image);
            if (Success)
            {
                RecognizeResponse body = JsonConvert.DeserializeObject<RecognizeResponse>(Body);
                var fields = body.Items[0].Fields;
                return (true, fields, "");
            }
            return (false, null, Body);
        }
        private string BuildHTML(string crop, string docType, Dictionary<string, FieldInfo> fields)
        {
            string htmlBase = @"<!DOCTYPE html>
                                <html lang=""en"">
                                <head>
                                    <meta charset=""utf-8""/>
                                    <title></title>
                                </head>
                                <body style=""width:800px;"">
                                    <table style=""width:800px; height: 100%;""><tr>
                                    <td style=""width:400px;""><img src=""{0}"" style=""width:400px""/></td>
                                    <td style=""width:400px""><table>
                                        <th>Field</th>
                                        <th>Value</th>
                                        {1}
                                    </table></td>
                                    </tr></table>
                                </body>
                                </html>";
            string docTypeRow = string.Format("<tr><td>{0}</td><td>{1}</td></tr>", "Document Type", docType);
            foreach (KeyValuePair<string, FieldInfo> entry in fields)
            {
                if (entry.Key.StartsWith("mrz"))
                {
                    continue;
                }
                docTypeRow += string.Format("<tr><td>{0}</td><td>{1}</td></tr>", entry.Key, entry.Value.Text);
            }

            return string.Format(htmlBase, crop, docTypeRow);
        }

        protected override void Execute(CodeActivityContext context)
        {
            string gateway = ApiGateway.Get(context);
            if (gateway == null || !gateway.StartsWith("http"))
            {
                gateway = BaseCloudGateWay;
            }

            string apiToken = ApiToken.Get(context);
            FileStream image = File.Open(ImagePath.Get(context), FileMode.Open);

            HttpClient client = BuildClient(apiToken);

            string result = "";
            string error = null;

            var classRes = Classify(client, gateway, image);
            Json.Set(context, classRes.Err);
            if (!classRes.Success)
            {
                error = classRes.Err;
                result = classRes.Err;
                Json.Set(context, result);
                Error.Set(context, error);
                client.Dispose();
                return;
            }
            string crop = classRes.Crop;
            string docType = classRes.DocType;
            result = docType;

            var recRes = Recognize(client, gateway, image, docType, false);
            if (!recRes.Success)
            {
                error = recRes.Err;
                result = recRes.Err;
                Json.Set(context, result);
                Error.Set(context, error);
                client.Dispose();
                return;
            }
            result = JsonConvert.SerializeObject(new Dictionary<string, dynamic>()
            {
                ["document_type"] = docType,
                ["fields"] = recRes.Fields
            });

            Html.Set(context, BuildHTML(crop, docType, recRes.Fields));
            Json.Set(context, result);
            Error.Set(context, error);
            client.Dispose();
        }
    }
}
