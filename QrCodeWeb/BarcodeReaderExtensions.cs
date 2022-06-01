using OpenCvSharp;

namespace QrCodeWeb
{
    //public static class BarcodeReaderExtensions
    //{
    //    /// <summary>
    //    /// uses the IBarcodeReaderGeneric implementation and the <see cref="MatLuminanceSource"/> class for decoding
    //    /// </summary>
    //    /// <param name="reader"></param>
    //    /// <param name="image"></param>
    //    /// <returns></returns>
    //    public static Result Decode(this IBarcodeReaderGeneric reader, Mat image)
    //    {
    //        var luminanceSource = new MatLuminanceSource(image);
    //        return reader.Decode(luminanceSource);
    //    }

    //    /// <summary>
    //    /// uses the IBarcodeReaderGeneric implementation and the <see cref="MatLuminanceSource"/> class for decoding
    //    /// </summary>
    //    /// <param name="reader"></param>
    //    /// <param name="image"></param>
    //    /// <returns></returns>
    //    public static Result[] DecodeMultiple(this IBarcodeReaderGeneric reader, Mat image)
    //    {
    //        var luminanceSource = new MatLuminanceSource(image);
    //        return reader.DecodeMultiple(luminanceSource);
    //    }
    //}
}

#if NET20
namespace System.Runtime.CompilerServices
{
	/// <summary>
	/// for compatibility to .net4.0, needed for extension methods
	/// </summary>
	[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
	public sealed class ExtensionAttribute : Attribute { }
}
#endif