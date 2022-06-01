using OpenCvSharp;

namespace QrCodeWeb.Datas
{
    public class ImgRecognitionResponse
    {
        public ImgRecognitionResponse()
        {
            fullAntiFakeCodes = new List<ImgQrCodeContent>(50);
        }

        /// <summary>
        /// 错误代码，1000 成功，1100 失败
        /// </summary>
        public string imgRecognitionState { get; set; }

        /// <summary>
        /// 错误异常具体信息。
        /// </summary>
        public string imgRecognitionMessage { get; set; }

        /// <summary>
        /// 成功截图的二维码 Base64编码
        /// </summary>
        public string cutedImgContent
        {
            set; get;
        }

        public List<ImgQrCodeContent> fullAntiFakeCodes { set; get; }
    }
}