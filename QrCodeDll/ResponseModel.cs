﻿namespace QrCodeDll
{
    public class ResponseModel
    {
        public string? Id { get; set; }

        public string? Code { set; get; }

        public string? Message { get; set; }

        public string? MarkImgData { get; set; }
        //public bool? IsDeCode { get; set; }

        // public string?  DeCodeContent { get; set; }
        public string? DeQRcodeContent { get; set; }
    }
}