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

    [LocalizedCategory(nameof(Resources.DbrainOCR))]
    [LocalizedDisplayName(nameof(Resources.DocrName))]
    [LocalizedDescription(nameof(Resources.DocrDescription))]
    public class Docr : CodeActivity
    {
        // Inputs
        [LocalizedCategory(nameof(Resources.Input))]
        [LocalizedDisplayName(nameof(Resources.ImageName))]
        [LocalizedDescription(nameof(Resources.ImageDescription))]
        [RequiredArgument]
        public InArgument<FileStream> Image { get; set; }

        [LocalizedCategory(nameof(Resources.Input))]
        [LocalizedDisplayName(nameof(Resources.AllowedClassesName))]
        [LocalizedDescription(nameof(Resources.AllowedClassesDescription))]
        public InArgument<List<String>> AllowedClasses { get; set; }

        // Outputs
        [LocalizedCategory(nameof(Resources.Output))]
        [LocalizedDisplayName(nameof(Resources.JsonName))]
        [LocalizedDescription(nameof(Resources.JsonDescription))]
        public OutArgument<string> Json { get; set; }

        [LocalizedCategory(nameof(Resources.Output))]
        [LocalizedDisplayName(nameof(Resources.ErrorName))]
        [LocalizedDescription(nameof(Resources.ErrorDescription))]
        public OutArgument<Dictionary<string, dynamic>> Error { get; set; }

        // Options
        [LocalizedCategory(nameof(Resources.Options))]
        [LocalizedDisplayName(nameof(Resources.ActionName))]
        [LocalizedDescription(nameof(Resources.ActionDescription))]
        [RequiredArgument]
        public Actions Action { get; set; }

        [LocalizedCategory(nameof(Resources.Options))]
        [LocalizedDisplayName(nameof(Resources.ApiGatewayName))]
        [LocalizedDescription(nameof(Resources.ApiGatewayDescription))]
        public InArgument<String> ApiGateway { get; set; }

        [LocalizedCategory(nameof(Resources.Options))]
        [LocalizedDisplayName(nameof(Resources.ApiTokenName))]
        [LocalizedDescription(nameof(Resources.ApiTokenDescription))]
        [RequiredArgument]
        public InArgument<String> ApiToken { get; set; }

        private string BuildURL(CodeActivityContext context)
        {
            string cloud_gateway = "https://recognition.latest.dbrain.io";
            string method;
            switch (Action)
            {
                case Actions.Classify:
                    {
                        method = "predict";
                        cloud_gateway = "https://classification.latest.dbrain.io";
                        break;
                    }
                case Actions.Recognize:
                    {
                        method = "predict";
                        break;
                    }
                case Actions.ClassifyRecognize:
                    {
                        method = "predict/classify/recognize";
                        break;
                    }
                case Actions.ClassifyRecognizeHitl:
                    {
                        method = "predict/classify/recognize/hitl";
                        break;
                    }
                default:
                    {
                        method = "predict";
                        break;
                    }
            }

            string gateway = ApiGateway.Get(context);
            if (gateway == null || !gateway.StartsWith("http"))
            {
                gateway = cloud_gateway;
            }

            return String.Format("{0}/{1}", gateway, method);            
        }

        protected override void Execute(CodeActivityContext context)
        {
            string url = BuildURL(context);
            string api_token = ApiToken.Get(context);

            FileStream image = Image.Get(context);

            List<string> allowed_classes = AllowedClasses.Get(context);
            var ac = "";
            if (allowed_classes != null)
            {
                ac = String.Join(",", allowed_classes.ToArray());
            }

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Token " + api_token);
            MultipartFormDataContent form = new MultipartFormDataContent();

            form.Add(new StreamContent(image), "image", image.Name);
            form.Add(new StringContent(ac), "text");

            HttpResponseMessage response = client.PostAsync(url, form).Result;
            var json = response.Content.ReadAsStringAsync().Result;
            Json.Set(context, json);

            if (!response.IsSuccessStatusCode)
            {
                Error.Set(
                    context,
                    JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(json)
                );
            }

            client.Dispose();
        }
    }
}
