namespace QrCodeWeb.Datas
{
    public class ResponseModel
    {
        public string? Id { get; set; }

        public string? Code { set; get; }

        public string? Message { get; set; }

        public bool? IsSuccess { get; set; }

        public object? Data { get; set; }

        public string? DeQRcodeContent { get; set; }
    }
}