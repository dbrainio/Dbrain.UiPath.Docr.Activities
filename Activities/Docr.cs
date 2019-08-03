using Dbrain.UiPath.Docr.Activities.Properties;
using Newtonsoft.Json.Linq;
using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Http;

namespace Dbrain.UiPath.Docr.Activities
{
    [LocalizedCategory(nameof(Resources.DbrainOCR))]
    [LocalizedDisplayName(nameof(Resources.DocrName))]
    [LocalizedDescription(nameof(Resources.DocrDescription))]
    public class Docr : CodeActivity
    {
        [LocalizedCategory(nameof(Resources.Input))]
        [LocalizedDisplayName(nameof(Resources.ApiUrlName))]
        [LocalizedDescription(nameof(Resources.ApiUrlDescription))]
        [DefaultValue(Helper.URL)]
        [RequiredArgument]
        public InArgument<String> ApiUrl { get; set; }

        [LocalizedCategory(nameof(Resources.Input))]
        [LocalizedDisplayName(nameof(Resources.ApiTokenName))]
        [LocalizedDescription(nameof(Resources.ApiTokenDescription))]
        [RequiredArgument]
        public InArgument<String> ApiToken { get; set; }

        [LocalizedCategory(nameof(Resources.Input))]
        [LocalizedDisplayName(nameof(Resources.AllowedClassesName))]
        [LocalizedDescription(nameof(Resources.AllowedClassesDescription))]
        [RequiredArgument]
        public InArgument<List<String>> AllowedClasses { get; set; }

        [LocalizedCategory(nameof(Resources.Input))]
        [LocalizedDisplayName(nameof(Resources.ImageName))]
        [LocalizedDescription(nameof(Resources.ImageDescription))]
        [RequiredArgument]
        public InArgument<String> Image { get; set; }

        [LocalizedCategory(nameof(Resources.Output))]
        [LocalizedDisplayName(nameof(Resources.DocumentsName))]
        [LocalizedDescription(nameof(Resources.DocumentsDescription))]
        public OutArgument<JArray> Documents { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            string api_url = ApiUrl.Get(context);
            string api_token = ApiToken.Get(context);
            List<string> allowed_classes = AllowedClasses.Get(context);
            string image = Image.Get(context);

            var ac = String.Join(",", allowed_classes.ToArray());

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Token " + api_token);
            MultipartFormDataContent form = new MultipartFormDataContent();

            FileStream fs = File.Open(image, FileMode.Open);
            StreamContent sc = new StreamContent(fs);
            form.Add(sc, "image", Path.GetFileName(image));
            form.Add(new StringContent(ac), "text");

            HttpResponseMessage response = client.PostAsync(api_url, form).Result;

            JObject obj;
            if (response.IsSuccessStatusCode)
            {
                var json = response.Content.ReadAsStringAsync().Result;
                obj = JObject.Parse(json);
                Documents.Set(context, obj);
            }
            else
            {
                obj = JObject.Parse("null");
            }

            Documents.Set(context, obj);

            client.Dispose();
        }
    }
}
