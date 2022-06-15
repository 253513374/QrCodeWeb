namespace QrCodeWeb.Datas
{
    public class ImgQrCodeContent
    {
        /// <summary>
        /// 二维码解析内容
        /// </summary>
        public string QrCodeContent { get; set; }

        /// <summary>
        /// 范围内顶点坐标X
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// 范围内顶点坐标Y
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// 范围宽
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 范围高
        /// </summary>

        public int Height { get; set; }

        /// <summary>
        /// 二维码中心点x坐标
        /// </summary>
        public int CenterX { get; set; }

        /// <summary>
        /// 二维码中心点y坐标
        /// </summary>

        public int CenterY { get; set; }

        /// <summary>
        /// 识别出来的二维码宽度
        /// </summary>
        public int QrCodeWidth { get; set; }

        /// <summary>
        /// 需要切图的图片base
        /// </summary>
        public string CutedImgBase64Content { get; set; }


        /// <summary>
        /// 档案图的路径
        /// </summary>
        string? url { get; set; } = "";
    }
}