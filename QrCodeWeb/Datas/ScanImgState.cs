namespace QrCodeWeb.Datas
{
    public enum ScanImgState
    {
        Succeed,
        Fail,
        TooFar,
        TooNear,
        TooLeft,
        TooRight,
        TooTop,
        TooBottom,
        MultiCode,
        NotCode
    }
}