using Microsoft.AspNetCore.Mvc;
using QrCodeWeb.Datas;
using QrCodeWeb.Services;

namespace QrCodeWeb.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SanCutImgController : ControllerBase
    {
        private CutImageService Serice { get; set; }

        private readonly ILogger<CutImageController> Logger;
        private IWebHostEnvironment Environment { get; set; }

        public SanCutImgController(CutImageService deCode, ILogger<CutImageController> logger, IWebHostEnvironment environment)
        {
            Serice = deCode;
            Logger = logger;
            Environment = environment;
        }

        /// <summary>
        /// 上传切图
        /// </summary>
        /// <param name="imgBase64"></param>
        /// <returns></returns>
        [HttpPost(Name = "scanqrimgCut")]
        public ImgRecognitionResponse ScanCutImg([FromBody] string imgBase64)
        {
            Logger.LogInformation($"ScanCutImg接收前端已经裁切图");
            return Serice.SanCutImg(imgBase64);
        }
    }
}