using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using QrCodeWeb.Datas;
using QrCodeWeb.Services;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace QrCodeWeb.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CutImageController : ControllerBase
    {
        //public IActionResult Index()
        //{
        //    return View();
        //}
        private CutImageService Serice { get; set; }

        private readonly ILogger<CutImageController> Logger;
        private IWebHostEnvironment Environment { get; set; }
        private readonly HttpClient httpClient;

        public CutImageController(CutImageService deCode, ILogger<CutImageController> logger,
            HttpClient httpClientFactory ,IWebHostEnvironment environment)
        {
            Serice = deCode;
            Logger = logger;
            Environment = environment;
            httpClient = httpClientFactory;
        }

        [HttpPost(Name = "CutImage")]
        public ImgRecognitionResponse CutImage([FromBody] ImgQrCodeContent str)
        {
            if (str == null)
            {
                return new ImgRecognitionResponse()
                {
                    imgRecognitionState = "1101",
                    imgRecognitionMessage = "参数为空"
                };
            }

            if(str.url is not "")
            {
                Stream stream = GetImage(str.url);
            }
            
            var result = Serice.CutImage(str);

            return result;
        }


        private Stream GetImage(string url)
        {

            //var httpRequestMessage = new HttpRequestMessage(
            //    HttpMethod.Get,
            //    "https://api.github.com/repos/dotnet/AspNetCore.Docs/branches")
            //     {
            //        Headers =
            //        {
            //            { HeaderNames.Accept, "application/vnd.github.v3+json" },
            //            { HeaderNames.UserAgent, "HttpRequestsSample" }
            //        }
            //    };
            //var httpClient = HttpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(url);

            // using Microsoft.Net.Http.Headers;
            // The GitHub API requires two headers.
            httpClient.DefaultRequestHeaders.Add(
                HeaderNames.Accept, "application/vnd.github.v3+json");
            httpClient.DefaultRequestHeaders.Add(
                HeaderNames.UserAgent, "HttpRequestsSample");

            var streamresponse = httpClient.GetStreamAsync(url).Result;


            if(streamresponse is not null)
            {
                return streamresponse;
            }
            return null;
            //      public async Task<IEnumerable<GitHubBranch>?> GetAspNetCoreDocsBranchesAsync() =>
            //await _httpClient.GetFromJsonAsync<IEnumerable<GitHubBranch>>(
            //    "repos/dotnet/AspNetCore.Docs/branches");
            //var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);

            //if (httpResponseMessage.IsSuccessStatusCode)
            //{
            //    using var contentStream =
            //        await httpResponseMessage.Content.ReadAsStreamAsync();

            //    GitHubBranches = await JsonSerializer.DeserializeAsync
            //        <IEnumerable<GitHubBranch>>(contentStream);
            //}
        }
    }
}