using Dbrain.UiPath.Docr.Activities.Properties;
using System;
using System.Activities;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Net.Http;
using Newtonsoft.Json;

namespace Dbrain.UiPath.Docr.Activities
{
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

    public struct ErrorResponse
    {
        public int Code;
        public string Message;
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
        public InArgument<Image> ImagePayload { get; set; }

        [LocalizedCategory(nameof(Resources.Input))]
        [LocalizedDisplayName(nameof(Resources.AllowedDocsName))]
        [LocalizedDescription(nameof(Resources.AllowedDocsDescription))]
        public InArgument<string> AllowedDocs { get; set; }

        // Outputs
        [LocalizedCategory(nameof(Resources.Output))]
        [LocalizedDisplayName(nameof(Resources.ResultName))]
        [LocalizedDescription(nameof(Resources.ResultDescription))]
        public OutArgument<string> Result { get; set; }

        [LocalizedCategory(nameof(Resources.Output))]
        [LocalizedDisplayName(nameof(Resources.HtmlName))]
        [LocalizedDescription(nameof(Resources.HtmlDescription))]
        public OutArgument<string> Html { get; set; }

        [LocalizedCategory(nameof(Resources.Output))]
        [LocalizedDisplayName(nameof(Resources.ErrorName))]
        [LocalizedDescription(nameof(Resources.ErrorDescription))]
        public OutArgument<int> Error { get; set; }

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

        private MemoryStream ImageToStream(Image image)
        {
            MemoryStream ms = new MemoryStream();
            image.Save(ms, image.RawFormat);
            _ = ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        private Image ImageFromBase64(string base64String)
        {
            byte[] imageBytes = Convert.FromBase64String(base64String.Substring(base64String.LastIndexOf(',') + 1));
            MemoryStream ms = new MemoryStream(imageBytes, 0, imageBytes.Length);
            Image image = Image.FromStream(ms, true);
            return image;
        }


        private (bool Success, string Body) MakeRequest(HttpClient client, string url, Image image)
        {
            MultipartFormDataContent form = new MultipartFormDataContent
            {
                { new StreamContent(ImageToStream(image)), "image", "doc.jpeg" }
            };

            HttpResponseMessage response = client.PostAsync(url, form).Result;
            string json = response.Content.ReadAsStringAsync().Result;
            return (response.IsSuccessStatusCode, json);
        }
        private (bool Success, string Crop, string DocType, int Err, string Message) Classify(HttpClient client, string gateway, Image image)
        {
            string url = gateway + "/classify";

            string crop = null;
            string docType = null;
            int err = 0;
            string message = null;

            (bool Success, string Body) = MakeRequest(client, url, image);
            if (Success)
            {
                ClassifyResponse body = JsonConvert.DeserializeObject<ClassifyResponse>(Body);
                crop = body.Items[0].Crop;
                docType = body.Items[0].Document.Type;
            }
            else
            {
                ErrorResponse body = JsonConvert.DeserializeObject<ErrorResponse>(Body);
                err = body.Code;
                message = body.Message;
            }
            return (Success, crop, docType, err, message);
        }

        private (bool Success, Dictionary<string, FieldInfo> Fields, int Err, string Message) Recognize(HttpClient client, string gateway, Image image, string docType, bool hitl = false)
        {
            string url = string.Format("{0}/{1}?doc_type={2}&with_hitl={3}", gateway, "recognize", docType, hitl);
            int err = 0;
            string message = null;
            Dictionary<string, FieldInfo> fields = null;

            (bool Success, string Body) = MakeRequest(client, url, image);
            if (Success)
            {
                RecognizeResponse body = JsonConvert.DeserializeObject<RecognizeResponse>(Body);
                fields = body.Items[0].Fields;
            }
            else
            {
                ErrorResponse body = JsonConvert.DeserializeObject<ErrorResponse>(Body);
                err = body.Code;
                message = body.Message;
            }
            return (Success, fields, err, message);
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
            string apiToken = ApiToken.Get(context);
            string allowedDocs = AllowedDocs.Get(context);
            Image image = ImagePayload.Get(context);

            if (gateway == null || !gateway.StartsWith("http"))
            {
                gateway = BaseCloudGateWay;
            }

            HttpClient client = BuildClient(apiToken);
            string html = null;
            int err = 0;

            var ClassifyResult = Classify(client, gateway, image);

            string result;
            if (!ClassifyResult.Success)
            {
                err = ClassifyResult.Err;
                result = JsonConvert.SerializeObject(new Dictionary<string, dynamic>()
                {
                    ["message"] = ClassifyResult.Message,
                    ["error"] = err
                });
            }
            else
            {
                if (allowedDocs == null || allowedDocs.Contains(ClassifyResult.DocType))
                {
                    image = ImageFromBase64(ClassifyResult.Crop);
                    var RecognizeResult = Recognize(client, gateway, image, ClassifyResult.DocType, false);
                    if (!RecognizeResult.Success)
                    {
                        err = RecognizeResult.Err;
                        result = JsonConvert.SerializeObject(new Dictionary<string, dynamic>()
                        {
                            ["message"] = RecognizeResult.Message,
                            ["error"] = err,
                            ["document_type"] = ClassifyResult.DocType
                        });
                    }
                    else
                    {
                        result = JsonConvert.SerializeObject(new Dictionary<string, dynamic>()
                        {
                            ["document_type"] = ClassifyResult.DocType,
                            ["fields"] = RecognizeResult.Fields
                        });
                        html = BuildHTML(ClassifyResult.Crop, ClassifyResult.DocType, RecognizeResult.Fields);
                    }
                }
                else
                {
                    err = 500;
                    result = JsonConvert.SerializeObject(new Dictionary<string, dynamic>()
                    {
                        ["message"] = string.Format("Document type '{0}' not allowed", ClassifyResult.DocType),
                        ["allowed_docs"] = allowedDocs
                    });
                }
            }

            Result.Set(context, result);
            Html.Set(context, html);
            Error.Set(context, err);
            client.Dispose();
        }
    }
}
